using System.Text;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Compile Guarantee verification for the V1 FastAPI + React/Vite deliverable — a live,
/// user-selectable Tier-2 option on the platform step, not a dev-only path.
///
/// Every command is recorded as a <see cref="BuildStepResult"/> against the half it verifies,
/// for the same reason the .NET strategy does it: <c>build-report.json</c> and the delivery
/// page's per-half badge are built from those steps. While this strategy recorded none, a
/// FastAPI + React customer received a report claiming a ".NET" and a "Next.js" half — both
/// "not_run", under a top-level <c>"status": "verified"</c>.
/// </summary>
/// <remarks>
/// Not sealed solely so tests can override <see cref="BuildStrategyBase.RunProcessAsync"/> and
/// drive the orchestration without a real Python/Node toolchain. Nothing in production
/// subclasses it.
/// </remarks>
public partial class PythonReactBuildStrategy(ILogger<PythonReactBuildStrategy> logger)
    : BuildStrategyBase(logger)
{
    public override ProjectType SupportedProjectType => ProjectType.PythonReact;

    public override async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        var transcript = new StringBuilder();
        var steps = new List<BuildStepResult>();

        var backendResult = await BuildBackendAsync(projectDirectory, transcript, steps, ct);
        if (!backendResult.IsSuccess)
            return Fail(backendResult, transcript, steps);

        var frontendResult = await BuildFrontendAsync(projectDirectory, transcript, steps, ct);
        if (!frontendResult.IsSuccess)
            return Fail(frontendResult, transcript, steps);

        return Success(transcript, steps);
    }

    private async Task<BuildResult> BuildBackendAsync(
        string projectDirectory,
        StringBuilder transcript,
        List<BuildStepResult> steps,
        CancellationToken ct)
    {
        var pythonDir = Path.Combine(projectDirectory, "backend");
        if (!Directory.Exists(pythonDir))
        {
            // Recorded as SKIPPED rather than omitted, so the report and the badge say "not
            // included" instead of silently implying "built and passed".
            transcript.AppendLine("[backend] no backend/ directory — skipping Python checks.");
            steps.Add(Skipped(BuildHalf.Python, "python -m flake8 ."));
            return Success(transcript, steps);
        }

        LogRunningPython(Logger, pythonDir);

        var pipResult = await RunStepAsync(
            BuildHalf.Python, "python -m pip install -r requirements.txt", "python",
            "-m pip install -r requirements.txt --quiet", pythonDir, transcript, steps, ct);
        if (!pipResult.IsSuccess)
            return pipResult;

        var flake8Result = await RunStepAsync(
            BuildHalf.Python, "python -m flake8 .", "python", "-m flake8 .",
            pythonDir, transcript, steps, ct);
        if (!flake8Result.IsSuccess)
            return flake8Result;

        return await RunStepAsync(
            BuildHalf.Python, "python -m pytest --collect-only", "python", "-m pytest --collect-only -q",
            pythonDir, transcript, steps, ct);
    }

    private async Task<BuildResult> BuildFrontendAsync(
        string projectDirectory,
        StringBuilder transcript,
        List<BuildStepResult> steps,
        CancellationToken ct)
    {
        var reactDir = Path.Combine(projectDirectory, "frontend");
        if (!Directory.Exists(reactDir))
        {
            transcript.AppendLine("[frontend] no frontend/ directory — skipping React checks.");
            steps.Add(Skipped(BuildHalf.React, "npx tsc --noEmit"));
            return Success(transcript, steps);
        }

        LogRunningReact(Logger, reactDir);

        var npmInstallResult = await RunStepAsync(
            BuildHalf.React, "npm install", NpmExecutable, "install --silent",
            reactDir, transcript, steps, ct);
        if (!npmInstallResult.IsSuccess)
            return npmInstallResult;

        var eslintResult = await RunStepAsync(
            BuildHalf.React, "npm run lint", NpmExecutable, "run lint -- --max-warnings=0",
            reactDir, transcript, steps, ct);
        if (!eslintResult.IsSuccess)
            return eslintResult;

        return await RunStepAsync(
            BuildHalf.React, "npx tsc --noEmit", NpxExecutable, "tsc --noEmit",
            reactDir, transcript, steps, ct);
    }

    /// <summary>
    /// De-duplicated, as in <see cref="DotNetBuildStrategy"/>: the transcript carries a failing
    /// command's diagnostics on both stdout and stderr, and the repair prompt is built from
    /// this list — three copies of one flake8 line is three copies of wasted context.
    /// </summary>
    public override List<string> ExtractBuildErrors(string buildOutput)
    {
        return PythonAndFrontendErrorRegex()
            .Matches(buildOutput)
            .Select(match => match.Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    protected override int CountWarnings(string output) => WarningRegex().Count(output);

    [GeneratedRegex(
        @"^.+:\d+:\d+:\s*(?:E|W|F|C)\d+\s+.+$|^\s*\d+:\d+\s+error\s+.+$|^.+:\s*error\s+TS\d+:.+$|^ERROR\s+.+$|^FAILED\s+.+$",
        RegexOptions.Multiline)]
    private static partial Regex PythonAndFrontendErrorRegex();

    // Warnings are report detail, never a failure. ESLint "<line>:<col>  warning  …", npm's
    // "npm WARN …", and pip's "WARNING: …" are the three this pipeline actually emits.
    [GeneratedRegex(
        @"^\s*\d+:\d+\s+warning\s+.+$|^npm WARN\b.+$|^WARNING:.+$",
        RegexOptions.Multiline)]
    private static partial Regex WarningRegex();

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Running Python validation in {Dir}")]
    private static partial void LogRunningPython(ILogger logger, string dir);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Running React validation in {Dir}")]
    private static partial void LogRunningReact(ILogger logger, string dir);
}
