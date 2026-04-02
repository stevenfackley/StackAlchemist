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

            result[path] = updated;
        }

        // Add any LLM blocks that don't map to injection zones as standalone files
        foreach (var (path, content) in llmBlocks)
        {
            if (!IsInjectionZoneContent(path))
            {
                result[path] = content;
            }
        }

        return result;
    }

    /// <summary>
    /// Maps zone names to LLM output blocks.
    /// Zone "Controllers" matches blocks under src/Controllers/ etc.
    /// </summary>
    private static string? FindContentForZone(string zoneName, Dictionary<string, string> llmBlocks)
    {
        // Direct zone-to-directory mapping
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Controllers"] = "Controllers/",
            ["Repositories"] = "Repositories/",
            ["Models"] = "Models/",
            ["SqlSchema"] = "Migrations/",
            ["RepositoryRegistrations"] = "__zone__/RepositoryRegistrations",
            ["RouteRegistrations"] = "__zone__/RouteRegistrations",
            ["HomePageContent"] = "__zone__/HomePageContent",
            ["ApiRouteHandlers"] = "__zone__/ApiRouteHandlers",
            ["TypeDefinitions"] = "__zone__/TypeDefinitions",
        };

        // Collect all blocks that match this zone's directory
        if (mapping.TryGetValue(zoneName, out var prefix))
        {
            var matchingBlocks = llmBlocks
                .Where(b => b.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(b => b.Value)
                .ToList();

            return matchingBlocks.Count > 0 ? string.Join("\n\n", matchingBlocks) : null;
        }

        return null;
    }

    private static bool IsInjectionZoneContent(string path)
    {
        return path.StartsWith("__zone__/", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\[\[FILE:\s*(.+?)\s*\]\]\n(.*?)\[\[END_FILE\]\]", RegexOptions.Singleline)]
    private static partial Regex FileBlockRegex();

    [GeneratedRegex(@"\[\[FILE:\s*(.+?)\s*\]\]")]
    private static partial Regex FileStartRegex();

    [GeneratedRegex(@"```\w*\n?|```\n?")]
    private static partial Regex MarkdownFenceRegex();
}
