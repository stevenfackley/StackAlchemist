using System.Globalization;
using System.Text;
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
/// </summary>
public sealed partial class DotNetBuildStrategy(ILogger<DotNetBuildStrategy> logger)
    : BuildStrategyBase(logger)
{
    public override ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

    public override async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        var transcript = new StringBuilder();

        var dotnetResult = await BuildDotNetAsync(projectDirectory, transcript, ct);
        if (!dotnetResult.IsSuccess)
            return dotnetResult;

        var nextResult = await BuildNextJsAsync(projectDirectory, transcript, ct);
        if (!nextResult.IsSuccess)
            return nextResult;

        return new BuildResult
        {
            ExitCode = 0,
            StandardOutput = transcript.ToString(),
            ErrorOutput = string.Empty,
        };
    }

    private async Task<BuildResult> BuildDotNetAsync(
        string projectDirectory,
        StringBuilder transcript,
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
        var restoreResult = await RunProcessAsync("dotnet", "restore", dotnetDir, ct);
        Append(transcript, "dotnet restore", restoreResult);
        if (!restoreResult.IsSuccess)
            return Fail(restoreResult, transcript);

        LogRunningBuild(Logger, dotnetDir);
        var buildResult = await RunProcessAsync("dotnet", "build --no-restore", dotnetDir, ct);
        Append(transcript, "dotnet build --no-restore", buildResult);
        return buildResult.IsSuccess ? buildResult : Fail(buildResult, transcript);
    }

    private async Task<BuildResult> BuildNextJsAsync(
        string projectDirectory,
        StringBuilder transcript,
        CancellationToken ct)
    {
        var nextDir = Path.Combine(projectDirectory, "nextjs");
        if (!File.Exists(Path.Combine(nextDir, "package.json")))
        {
            // A .NET-only archive (or a caller pointing straight at the dotnet dir) is
            // still a legitimate success — there is simply no frontend to verify.
            transcript.AppendLine("[nextjs] no nextjs/package.json — skipping frontend build.");
            return new BuildResult { ExitCode = 0, StandardOutput = string.Empty, ErrorOutput = string.Empty };
        }

        LogRunningNextJs(Logger, nextDir);

        // `npm ci` is the reproducible install and is tried first. It hard-fails when
        // package.json and package-lock.json disagree, which is exactly what happens when
        // the LLM pass legitimately adds a dependency to a generated project — so fall back
        // to `npm install`, which re-resolves and rewrites the lockfile.
        var installResult = await RunProcessAsync(
            NpmExecutable, "ci --no-audit --no-fund", nextDir, ct);
        Append(transcript, "npm ci", installResult);

        if (!installResult.IsSuccess)
        {
            LogNpmCiFallback(Logger, nextDir);
            installResult = await RunProcessAsync(
                NpmExecutable, "install --no-audit --no-fund", nextDir, ct);
            Append(transcript, "npm install (fallback)", installResult);
            if (!installResult.IsSuccess)
                return Fail(installResult, transcript);
        }

        var buildResult = await RunProcessAsync(NpmExecutable, "run build", nextDir, ct);
        Append(transcript, "npm run build", buildResult);
        return buildResult.IsSuccess ? buildResult : Fail(buildResult, transcript);
    }

    /// <summary>
    /// Carries the failing step's exit code and stderr while replacing stdout with the
    /// whole-run transcript, so the repair loop and the persisted build_log show every
    /// command that ran, not just the one that blew up.
    /// </summary>
    private static BuildResult Fail(BuildResult failed, StringBuilder transcript) => new()
    {
        ExitCode = failed.ExitCode,
        StandardOutput = transcript.ToString(),
        ErrorOutput = failed.ErrorOutput,
    };

    private static void Append(StringBuilder transcript, string step, BuildResult result)
    {
        transcript.AppendLine(CultureInfo.InvariantCulture, $"$ {step}  (exit {result.ExitCode})");
        transcript.AppendLine(result.StandardOutput);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorOutput))
            transcript.AppendLine(result.ErrorOutput);
        transcript.AppendLine();
    }

    [LoggerMessage(EventId = 1099, Level = LogLevel.Information, Message = "Running dotnet restore in {Dir}")]
    private static partial void LogRunningRestore(ILogger logger, string dir);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Running dotnet build in {Dir}")]
    private static partial void LogRunningBuild(ILogger logger, string dir);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Running npm install + next build in {Dir}")]
    private static partial void LogRunningNextJs(ILogger logger, string dir);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "npm ci failed in {Dir}; falling back to npm install")]
    private static partial void LogNpmCiFallback(ILogger logger, string dir);

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
}
