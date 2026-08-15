using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public sealed partial class ReconstructionService : IReconstructionService
{
    public Dictionary<string, string> Parse(string llmOutput)
    {
        // Normalize line endings
        var normalized = llmOutput.Replace("\r\n", "\n").Replace("\r", "\n");

        var blocks = new Dictionary<string, string>();
        var matches = FileBlockRegex().Matches(normalized);

        if (matches.Count == 0)
        {
            // Check if there are any unclosed FILE blocks
            var startMatches = FileStartRegex().Matches(normalized);
            if (startMatches.Count > 1)
            {
                // Multiple [[FILE:...]] markers with no [[END_FILE]] — malformed, not just truncated
                throw new MalformedLlmOutputException(
                    $"Multiple [[FILE:...]] markers found without [[END_FILE]] delimiters. Response appears malformed.");
            }

            if (startMatches.Count == 1)
            {
                // Single unclosed FILE block — response appears truncated
                throw new TruncatedLlmResponseException(
                    "LLM response contains [[FILE:...]] marker but no [[END_FILE]] — response appears truncated.");
            }

            return blocks;
        }

        // Check for unclosed blocks after last match
        var lastMatch = matches[^1];
        var afterLast = normalized[(lastMatch.Index + lastMatch.Length)..];
        var orphanStart = FileStartRegex().Match(afterLast);
        if (orphanStart.Success)
        {
            throw new MalformedLlmOutputException(
                $"Missing [[END_FILE]] for file: {orphanStart.Groups[1].Value.Trim()}");
        }

        foreach (Match match in matches)
        {
            var filePath = match.Groups[1].Value.Trim();
            var content = match.Groups[2].Value;

            // Strip BOM
            content = content.TrimStart('\uFEFF');

            // Strip markdown fencing
            content = MarkdownFenceRegex().Replace(content, "");

            // Trim leading/trailing blank lines but preserve internal whitespace
            content = content.Trim('\n');

            // Last-write-wins for duplicate paths
            blocks[filePath] = content;
        }

        return blocks;
    }

    /// <summary>
    /// Pseudo-path prefix an LLM block uses to address an injection zone that owns no file of
    /// its own — a fragment spliced into an existing template file (the Program.cs registration
    /// blocks, a JSX island inside page.tsx, …). Blocks addressed this way are NEVER written to
    /// disk as files; the zone they name is the only place their content can go.
    /// </summary>
    public const string ZonePathPrefix = "__zone__/";

    public Dictionary<string, string> Reconstruct(
        Dictionary<string, string> renderedTemplates,
        Dictionary<string, string> llmBlocks,
        ITemplateProvider templateProvider)
    {
        var result = new Dictionary<string, string>(renderedTemplates);

        // For each template file, check if it has injection zones
        // and inject matching LLM content
        foreach (var (path, content) in renderedTemplates)
        {
            var zones = templateProvider.FindInjectionZones(content);
            if (zones.Count == 0) continue;

            var updated = content;
            foreach (var zone in zones)
            {
                // Find LLM blocks that map to this zone
                var zoneContent = FindContentForZone(zone, llmBlocks);
                if (zoneContent is not null)
                {
                    updated = templateProvider.InjectIntoZone(updated, zone, zoneContent);
                }
            }

            // The [[LLM_INJECTION_*]] markers are scaffolding for this pipeline, not
            // source code: `[[LLM_INJECTION_START: RouteRegistrations]]` sitting in
            // Program.cs is a CS1525 syntax error and in page.tsx it renders as literal
            // text. InjectIntoZone deliberately preserves them (a zone may be filled
            // more than once), so they are stripped here, once, after every zone in the
            // file has been resolved — matching what the Swiss-Cheese path already does
            // in InjectionEngine. Unfilled zones collapse to nothing, which is what makes
            // the bare template compile on its own.
            result[path] = templateProvider.StripInjectionMarkers(updated);
        }

        // Every remaining block is a real file. It has to land somewhere the generated
        // project actually reads from — which means inside one of the top-level directories
        // the rendered template produced (dotnet/, nextjs/, …) or, for a bare file name,
        // beside the template's own root files.
        //
        // Anything else used to be written verbatim at the archive root, where nothing
        // compiles it and nothing serves it. That is how a prompt asking for
        // `src/types/index.ts` shipped paying customers a frontend with no types, no API
        // helpers and an empty stub page: the files were in the zip, just not in the app.
        // Silent misplacement is the one outcome this must never produce again.
        var treeRoots = TopLevelDirectories(renderedTemplates);
        var unmapped = new List<string>();

        foreach (var (path, content) in llmBlocks)
        {
            if (IsInjectionZoneContent(path))
                continue;

            if (BelongsToRenderedTree(path, treeRoots))
                result[path] = content;
            else
                unmapped.Add(path);
        }

        if (unmapped.Count > 0)
            throw new UnmappedLlmFileException(unmapped, treeRoots);

        return result;
    }

    /// <summary>
    /// Routes the file blocks of a BUILD-REPAIR response — the second, later write site, where
    /// the model is handed compiler errors and asked for corrected files.
    ///
    /// Reconstruct's guarantee only ever covered the first write. The repair loop parsed the
    /// response and wrote every block straight into the output directory, so all three failures
    /// Reconstruct exists to prevent were still reachable from here: <c>src/lib/api.ts</c>
    /// orphaned at the archive root, a <c>__zone__/RouteRegistrations</c> fragment written as a
    /// literal file into the customer's zip, and a <c>..</c> segment escaping the directory
    /// entirely. This applies the same <see cref="BelongsToRenderedTree"/> rule to that write,
    /// against the tree as it actually exists on disk.
    ///
    /// It reports rather than throws, and the difference is the customer's money. The repair
    /// loop already has a defined failure mode — retries exhaust, the build stays red, and the
    /// Compile Guarantee refunds a paid generation. Throwing here would abort the job on the
    /// spot through the worker's generic catch, which marks it Failed WITHOUT ever reaching the
    /// refund branch: the customer would lose both the app and the money. Dropping the block and
    /// naming it in the log leaves that path intact, and the retry prompt states the tree roots
    /// up front so the next attempt has what it needs to get the path right.
    ///
    /// Zone pseudo-paths are rejected here even though Reconstruct honours them: by repair time
    /// the <c>[[LLM_INJECTION_*]]</c> markers have been stripped, so there is no zone left to
    /// fill and the only thing writing one could do is drop scaffolding into the archive.
    /// </summary>
    /// <param name="llmBlocks">Parsed blocks from the repair response.</param>
    /// <param name="outputDirectory">The generated project directory the fixes are written into.</param>
    public RepairWriteSet ResolveRepairWrites(Dictionary<string, string> llmBlocks, string outputDirectory)
    {
        // The tree as built, not as planned: the customer's archive is what it is on disk by now.
        var treeRoots = new HashSet<string>(
            Directory.EnumerateDirectories(outputDirectory).Select(d => Path.GetFileName(d)!),
            StringComparer.OrdinalIgnoreCase);

        var writes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rejected = new List<string>();

        foreach (var (path, content) in llmBlocks)
        {
            if (IsInjectionZoneContent(path) || !BelongsToRenderedTree(path, treeRoots))
                rejected.Add(path);
            else
                writes[path] = content;
        }

        return new RepairWriteSet(
            writes,
            rejected,
            rejected.Count == 0
                ? null
                : UnmappedLlmFileException.DescribeUnmappedPaths(rejected, treeRoots));
    }

    /// <summary>
    /// Resolves the LLM block that fills <paramref name="zoneName"/>.
    ///
    /// One rule, no heuristics: zone <c>X</c> is filled by the block at
    /// <c>__zone__/X</c> and by nothing else. The previous directory-substring
    /// matching ("any path containing <c>Models/</c> fills the Models zone") silently
    /// routed real files into fragment zones AND left a duplicate copy at the archive
    /// root, so the same code could end up in two places, one of which compiled.
    /// </summary>
    private static string? FindContentForZone(string zoneName, Dictionary<string, string> llmBlocks)
    {
        var zonePath = ZonePathPrefix + zoneName;

        foreach (var (path, content) in llmBlocks)
        {
            if (path.Equals(zonePath, StringComparison.OrdinalIgnoreCase))
                return content;
        }

        return null;
    }

    private static bool IsInjectionZoneContent(string path)
    {
        return path.StartsWith(ZonePathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The top-level directory names the rendered template set produced — the only roots an
    /// emitted file path is allowed to start with.
    /// </summary>
    private static HashSet<string> TopLevelDirectories(Dictionary<string, string> renderedTemplates)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in renderedTemplates.Keys)
        {
            var normalized = path.Replace('\\', '/');
            var slash = normalized.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0)
                roots.Add(normalized[..slash]);
        }

        return roots;
    }

    /// <summary>
    /// True when <paramref name="path"/> would land somewhere the generated project reads
    /// from. A bare file name is fine (it sits beside the template's own root files, e.g. a
    /// README); anything deeper must start with a directory the template actually rendered.
    ///
    /// A <c>..</c> segment anywhere is rejected outright: these paths come from model output
    /// and are combined with the output directory before being written, so a traversal would
    /// escape the archive entirely.
    ///
    /// So is any rooted path, and for a subtler reason: validation normalizes, the write does
    /// not. The caller stores the block under its ORIGINAL path and later does
    /// <c>Path.Combine(outputDirectory, path)</c>, and Path.Combine discards its first argument
    /// whenever the second is rooted — so <c>/nextjs/src/x.ts</c> would pass a check performed
    /// on the trimmed <c>nextjs/src/x.ts</c> and then be written to the root of the current
    /// drive. The drive-relative form <c>C:evil.cs</c> is the same hole with no separator at
    /// all: it has one segment, so the bare-file-name allowance would wave it through.
    /// <see cref="Path.IsPathRooted(string)"/> covers all three shapes (rooted, drive-relative,
    /// UNC), so the path that is validated is always the path that gets written.
    /// </summary>
    private static bool BelongsToRenderedTree(string path, HashSet<string> treeRoots)
    {
        if (Path.IsPathRooted(path))
            return false;

        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0)
            return false;

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s.Equals("..", StringComparison.Ordinal)))
            return false;

        return segments.Length < 2 || treeRoots.Contains(segments[0]);
    }

    [GeneratedRegex(@"\[\[FILE:\s*(.+?)\s*\]\]\n(.*?)\[\[END_FILE\]\]", RegexOptions.Singleline)]
    private static partial Regex FileBlockRegex();

    [GeneratedRegex(@"\[\[FILE:\s*(.+?)\s*\]\]")]
    private static partial Regex FileStartRegex();

    [GeneratedRegex(@"```\w*\n?|```\n?")]
    private static partial Regex MarkdownFenceRegex();
}
