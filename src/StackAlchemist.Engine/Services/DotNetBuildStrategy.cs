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
            .Concat(FrontendErrorRegex()
                .Matches(buildOutput)
                .Select(match => match.Value.Trim()))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
}
