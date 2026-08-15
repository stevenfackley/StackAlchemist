using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Compile Guarantee verification for the V1 .NET 10 + Next.js 15 deliverable.
///
/// Both halves of the archive are built, because both halves are what the customer
/// paid for: docs/advanced-docs/compile-guarantee.md and docs/user/tiers-and-pricing.md
/// promise <c>dotnet build</c> AND <c>npm run build</c>. Until this class grew the
/// nextjs/ leg it only ever ran <c>dotnet</c>, so a Boilerplate archive whose frontend
/// did not compile still shipped stamped "Compile Verified".
///
/// Every command is recorded as a <see cref="BuildStepResult"/> on the returned
/// <see cref="BuildResult"/>. The flat transcript is what the LLM repair loop reads; the
/// step list is what <c>build-report.json</c> and the delivery UI's per-half badge are
/// built from, because "the build passed" and "the Next.js half compiled" are different
/// claims and only the second one is the thing being sold.
/// </summary>
/// <remarks>
/// Not sealed solely so tests can override <see cref="BuildStrategyBase.RunProcessAsync"/>
/// and drive the dual-build orchestration without a real toolchain. Nothing in production
/// subclasses it.
/// </remarks>
public partial class DotNetBuildStrategy(ILogger<DotNetBuildStrategy> logger)
    : BuildStrategyBase(logger)
{
    /// <summary>
    /// npm script the frontend is type-checked with when the template defines it. Optional
    /// on purpose: `next build` already type-checks, so a generated project that dropped the
    /// script is not thereby unverified — it just gets one signal instead of two.
    /// </summary>
    private const string TypecheckScript = "typecheck";

    public override ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

    public override async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        var transcript = new StringBuilder();
        var steps = new List<BuildStepResult>();

        var dotnetResult = await BuildDotNetAsync(projectDirectory, transcript, steps, ct);
        if (!dotnetResult.IsSuccess)
            return Fail(dotnetResult, transcript, steps);

        var nextResult = await BuildNextJsAsync(projectDirectory, transcript, steps, ct);
        if (!nextResult.IsSuccess)
            return Fail(nextResult, transcript, steps);

        return Success(transcript, steps);
    }

    private async Task<BuildResult> BuildDotNetAsync(
        string projectDirectory,
        StringBuilder transcript,
        List<BuildStepResult> steps,
        CancellationToken ct)
    {
        var dotnetDir = Path.Combine(projectDirectory, "dotnet");
        if (!Directory.Exists(dotnetDir))
            dotnetDir = projectDirectory;

        // Restore as its own process invocation (not `dotnet build` implicitly restoring)
        // so a restore failure (missing feed, NU1101, etc.) is distinguishable in the log
        // from a compile failure, and so `build --no-restore` below never runs against a
        // freshly-written temp dir that was never restored (was NETSDK1004 every time).
        LogRunningRestore(Logger, dotnetDir);
        var restoreResult = await RunStepAsync(
            BuildHalf.DotNet, "dotnet restore", "dotnet", "restore", dotnetDir, transcript, steps, ct);
        if (!restoreResult.IsSuccess)
            return restoreResult;

        LogRunningBuild(Logger, dotnetDir);
        return await RunStepAsync(
            BuildHalf.DotNet, "dotnet build --no-restore", "dotnet", "build --no-restore",
            dotnetDir, transcript, steps, ct);
    }

    private async Task<BuildResult> BuildNextJsAsync(
        string projectDirectory,
        StringBuilder transcript,
        List<BuildStepResult> steps,
        CancellationToken ct)
    {
        var nextDir = Path.Combine(projectDirectory, "nextjs");
        var packageJsonPath = Path.Combine(nextDir, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            // A .NET-only archive (or a caller pointing straight at the dotnet dir) is
            // still a legitimate success — there is simply no frontend to verify. It is
            // recorded as a SKIPPED step, not omitted, so the report and the badge say
            // "not built" rather than silently implying "built and passed".
            transcript.AppendLine("[nextjs] no nextjs/package.json — skipping frontend build.");
            steps.Add(Skipped(BuildHalf.NextJs, "npm run build"));
            return Success(transcript, steps);
        }

        LogRunningNextJs(Logger, nextDir);

        // `npm ci` is the reproducible install and is tried first. It hard-fails when
        // package.json and package-lock.json disagree, which is exactly what happens when
        // the LLM pass legitimately adds a dependency to a generated project — so fall back
        // to `npm install`, which re-resolves and rewrites the lockfile.
        var ciStepIndex = steps.Count;
        var installResult = await RunStepAsync(
            BuildHalf.NextJs, "npm ci", NpmExecutable, "ci --no-audit --no-fund",
            nextDir, transcript, steps, ct);

        if (!installResult.IsSuccess)
        {
            LogNpmCiFallback(Logger, nextDir);

            // Keep the failed `npm ci` as evidence, but mark it superseded: the install
            // below redoes its work, so it decided nothing. This is the DOCUMENTED common
            // path, and counting it stamped `halves[nextjs] = "failed"` on archives whose
            // frontend compiled — inside a report whose own status read "verified".
            Supersede(steps, ciStepIndex);

            installResult = await RunStepAsync(
                BuildHalf.NextJs, "npm install", NpmExecutable, "install --no-audit --no-fund",
                nextDir, transcript, steps, ct);
            if (!installResult.IsSuccess)
                return installResult;
        }

        // Mirrors PythonReactBuildStrategy's `tsc --noEmit` leg. Runs before `next build`
        // because tsc's diagnostics name every offending file at once, whereas `next build`
        // aborts on the first type error — one round-trip of repair context instead of N.
        if (HasScript(packageJsonPath, TypecheckScript))
        {
            var typecheckResult = await RunStepAsync(
                BuildHalf.NextJs, $"npm run {TypecheckScript}", NpmExecutable, $"run {TypecheckScript}",
                nextDir, transcript, steps, ct);
            if (!typecheckResult.IsSuccess)
                return typecheckResult;
        }
        else
        {
            LogNoTypecheckScript(Logger, nextDir);
            steps.Add(Skipped(BuildHalf.NextJs, $"npm run {TypecheckScript}"));
        }

        return await RunStepAsync(
            BuildHalf.NextJs, "npm run build", NpmExecutable, "run build",
            nextDir, transcript, steps, ct);
    }

    /// <summary>
    /// True when <c>package.json</c> defines the named npm script. A malformed package.json
    /// is treated as "no script" rather than thrown: the build itself is about to fail on it
    /// with a far better message than a JSON parse exception from the verifier.
    /// </summary>
    private bool HasScript(string packageJsonPath, string scriptName)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            return document.RootElement.TryGetProperty("scripts", out var scripts)
                && scripts.ValueKind == JsonValueKind.Object
                && scripts.TryGetProperty(scriptName, out var script)
                && script.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(script.GetString());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogPackageJsonUnreadable(Logger, ex, packageJsonPath);
            return false;
        }
    }

    [LoggerMessage(EventId = 1099, Level = LogLevel.Information, Message = "Running dotnet restore in {Dir}")]
    private static partial void LogRunningRestore(ILogger logger, string dir);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Running dotnet build in {Dir}")]
    private static partial void LogRunningBuild(ILogger logger, string dir);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Running npm install + next build in {Dir}")]
    private static partial void LogRunningNextJs(ILogger logger, string dir);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "npm ci failed in {Dir}; falling back to npm install")]
    private static partial void LogNpmCiFallback(ILogger logger, string dir);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "No 'typecheck' script in {Dir}/package.json; relying on next build's type checking")]
    private static partial void LogNoTypecheckScript(ILogger logger, string dir);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "Could not read {Path} to look for npm scripts")]
    private static partial void LogPackageJsonUnreadable(ILogger logger, Exception ex, string path);

    public override List<string> ExtractBuildErrors(string buildOutput)
    {
        return DotNetErrorRegex()
            .Matches(buildOutput)
            .Select(match => match.Value.Trim())
            .Concat(ExtractFrontendErrors(buildOutput))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // ── Frontend error reassembly ─────────────────────────────────────────────
    //
    // `next build` puts the location on the line ABOVE the message and the offending
    // source in a code frame BELOW it:
    //
    //     ./src/app/page.tsx:5:9
    //     Type error: Type 'string' is not assignable to type 'number'.
    //
    //       3 | export default function Home() {
    //     > 4 |   const total: number = "12";
    //         |         ^
    //
    // Matching only the message line — which is all FrontendErrorRegex can do on its own —
    // hands the repair loop "Type error: Type 'string' is not assignable to type 'number'."
    // with no file and no line. The LLM cannot fix that, so all three retries burn and
    // TryIssueCompileGuaranteeRefundAsync refunds a paying customer. The surrounding
    // context is therefore stitched back on here.

    /// <summary>Cap on reassembled frontend errors, so one bad file cannot crowd the retry prompt out.</summary>
    private const int MaxFrontendErrors = 20;

    /// <summary>How far above a message line to look for its <c>./path:line:col</c> header.</summary>
    private const int MaxLocationLookbackLines = 25;

    /// <summary>Cap on code-frame lines kept per error.</summary>
    private const int MaxCodeFrameLines = 12;

    /// <summary>Blank lines tolerated between the message and the start of its code frame.</summary>
    private const int MaxBlankLinesBeforeFrame = 2;

    private static List<string> ExtractFrontendErrors(string buildOutput)
    {
        var lines = buildOutput.Split('\n');

        // Keyed by message line: a failing step contributes its output to the transcript
        // twice (stdout, then stderr), and the copies do not always carry the same amount
        // of surrounding context. Keep the richest block for each distinct message.
        var byMessage = new Dictionary<string, int>(StringComparer.Ordinal);
        var blocks = new List<string>();

        for (var i = 0; i < lines.Length && blocks.Count < MaxFrontendErrors; i++)
        {
            var message = lines[i].Trim();
            if (message.Length == 0 || !FrontendErrorRegex().IsMatch(message))
                continue;

            var block = new List<string>();
            if (FindLocationAbove(lines, i) is { } location)
                block.Add(location);
            block.Add(message);
            block.AddRange(CodeFrameBelow(lines, i));

            var text = string.Join('\n', block);
            if (byMessage.TryGetValue(message, out var existing))
            {
                if (text.Length > blocks[existing].Length)
                    blocks[existing] = text;
            }
            else
            {
                byMessage[message] = blocks.Count;
                blocks.Add(text);
            }
        }

        return blocks;
    }

    /// <summary>
    /// The nearest preceding <c>./src/app/page.tsx:5:9</c>-shaped line. Blank lines and
    /// sibling error lines are stepped over (ESLint groups several errors under one file
    /// header); anything else stops the walk, so an unrelated path is never attached.
    /// </summary>
    private static string? FindLocationAbove(string[] lines, int messageIndex)
    {
        var stop = Math.Max(0, messageIndex - MaxLocationLookbackLines);
        for (var i = messageIndex - 1; i >= stop; i--)
        {
            var candidate = lines[i].Trim();
            if (candidate.Length == 0)
                continue;
            if (FrontendLocationRegex().IsMatch(candidate))
                return candidate;
            if (FrontendErrorRegex().IsMatch(candidate))
                continue;
            return null;
        }

        return null;
    }

    private static List<string> CodeFrameBelow(string[] lines, int messageIndex)
    {
        var frame = new List<string>();
        var blanksSkipped = 0;

        for (var i = messageIndex + 1; i < lines.Length && frame.Count < MaxCodeFrameLines; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.Trim().Length == 0)
            {
                if (frame.Count > 0 || ++blanksSkipped > MaxBlankLinesBeforeFrame)
                    break;
                continue;
            }

            if (!CodeFrameLineRegex().IsMatch(line))
                break;

            frame.Add(line);
        }

        return frame;
    }

    // Covers compiler errors (CS), SDK resolution failures (NETSDK, e.g. missing
    // restore), MSBuild engine errors (MSB), and NuGet restore errors (NU) — the four
    // families `dotnet restore`/`dotnet build` actually emit for this pipeline.
    [GeneratedRegex(@"^(?:.+:\s*)?error\s+(?:CS|NETSDK|MSB|NU)\d+:.+$", RegexOptions.Multiline)]
    private static partial Regex DotNetErrorRegex();

    // The frontend half. `next build` surfaces type failures as a "Type error:" line,
    // unresolvable imports as "Module not found:", ESLint as "<line>:<col>  error  …",
    // and npm install failures as "npm ERR! …".
    [GeneratedRegex(
        @"^\s*Type error:.+$|^\s*Module not found:.+$|^.+:\s*error\s+TS\d+:.+$|^\s*\d+:\d+\s+error\s+.+$|^npm ERR!.+$",
        RegexOptions.Multiline)]
    private static partial Regex FrontendErrorRegex();

    // A location header on its own line: "./src/app/page.tsx:5:9" (next build) or
    // "./src/app/page.tsx" (the ESLint group header). Anchored end-to-end and
    // extension-qualified so ordinary log prose can never be mistaken for a path.
    [GeneratedRegex(
        @"^(?:\.{1,2}[\\/])?[^\s:*?""<>|]+\.(?:tsx?|jsx?|mjs|cjs|mts|cts|css|scss|json)(?::\d+(?::\d+)?)?$")]
    private static partial Regex FrontendLocationRegex();

    // A code-frame line: "  3 | const x = 1", "> 4 |   …", "    |       ^".
    [GeneratedRegex(@"^\s*(?:>\s*)?\d*\s*\|")]
    private static partial Regex CodeFrameLineRegex();

    protected override int CountWarnings(string output) => WarningRegex().Count(output);

    // Warnings are counted, never failed on — they are report detail, not a refund trigger.
    // Both halves: MSBuild/Roslyn "warning CS0168:", ESLint "<line>:<col>  warning  …",
    // and npm's "npm WARN …".
    [GeneratedRegex(
        @"^(?:.+:\s*)?warning\s+[A-Z]+\d+:.+$|^\s*\d+:\d+\s+warning\s+.+$|^npm WARN\b.+$",
        RegexOptions.Multiline)]
    private static partial Regex WarningRegex();
}
