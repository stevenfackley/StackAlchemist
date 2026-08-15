using System.Diagnostics;
using System.Globalization;
using System.Text;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public abstract class BuildStrategyBase(ILogger logger) : IBuildStrategy
{
    public abstract ProjectType SupportedProjectType { get; }

    public abstract Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default);

    public abstract List<string> ExtractBuildErrors(string buildOutput);

    /// <summary>
    /// Warnings parsed out of one command's combined stdout+stderr, for the report's
    /// per-step counts. Warnings are counted, never failed on — they are report detail, not a
    /// refund trigger — but the toolchains spell them differently enough that each strategy
    /// owns its own pattern.
    /// </summary>
    protected abstract int CountWarnings(string output);

    protected ILogger Logger { get; } = logger;

    /// <inheritdoc cref="ProcessCommandResolver"/>
    protected static string NpmExecutable => ProcessCommandResolver.Npm;

    /// <inheritdoc cref="ProcessCommandResolver"/>
    protected static string NpxExecutable => ProcessCommandResolver.Npx;

    /// <summary>
    /// Runs one external build command and captures its output.
    ///
    /// Virtual purely as a test seam: the dual-build orchestration in
    /// <see cref="DotNetBuildStrategy"/> — which half runs first, what a failure in either
    /// half does, whether an optional step is skipped — is logic worth unit-testing, and it
    /// cannot be tested by shelling out to a real toolchain. Overrides stub this; nothing in
    /// production replaces it.
    /// </summary>
    protected virtual async Task<BuildResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new BuildResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            ErrorOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
        };
    }

    // ── Transcript + per-step recording ───────────────────────────────────────
    //
    // Shared by every strategy, because build-report.json is one published contract and two
    // hand-rolled implementations of it would drift. `StandardOutput` is a flat transcript —
    // fine for the LLM repair prompt, useless for "did the frontend half actually compile?",
    // which is the question the Compile Guarantee sells an answer to.

    /// <summary>
    /// Runs one command, appends it to <paramref name="transcript"/>, and records a
    /// <see cref="BuildStepResult"/> in <paramref name="steps"/>.
    ///
    /// <paramref name="displayCommand"/> is what lands in the report and the log — never
    /// <paramref name="fileName"/>, which on Windows is an absolute path to npm.cmd and would
    /// leak the build host's filesystem layout into a customer-facing artifact.
    /// </summary>
    protected async Task<BuildResult> RunStepAsync(
        BuildHalf half,
        string displayCommand,
        string fileName,
        string arguments,
        string workingDirectory,
        StringBuilder transcript,
        List<BuildStepResult> steps,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await RunProcessAsync(fileName, arguments, workingDirectory, ct);
        stopwatch.Stop();

        Append(transcript, displayCommand, result);

        // Count over stdout AND stderr: `dotnet` writes diagnostics to stdout while `next
        // build` and npm write most of theirs to stderr, and RunProcessAsync only mirrors
        // stdout into ErrorOutput when stderr came back empty.
        var combined = result.StandardOutput + "\n" + result.ErrorOutput;
        steps.Add(new BuildStepResult
        {
            Half = half,
            Command = displayCommand,
            ExitCode = result.ExitCode,
            DurationMs = stopwatch.ElapsedMilliseconds,
            ErrorCount = ExtractBuildErrors(combined).Count,
            WarningCount = CountWarnings(combined),
        });

        return result;
    }

    /// <summary>
    /// Marks the step recorded at <paramref name="index"/> as superseded by a retry, so it
    /// stays in the report as evidence without condemning its half.
    /// </summary>
    protected static void Supersede(List<BuildStepResult> steps, int index) =>
        steps[index] = steps[index] with { Superseded = true };

    /// <summary>A command that was deliberately not run. Recorded, never omitted.</summary>
    protected static BuildStepResult Skipped(BuildHalf half, string command) => new()
    {
        Half = half,
        Command = command,
        ExitCode = 0,
        DurationMs = 0,
        Skipped = true,
    };

    protected static BuildResult Success(StringBuilder transcript, List<BuildStepResult> steps) => new()
    {
        ExitCode = 0,
        StandardOutput = transcript.ToString(),
        ErrorOutput = string.Empty,
        Steps = steps,
    };

    /// <summary>
    /// Carries the failing step's exit code and stderr while replacing stdout with the
    /// whole-run transcript, so the repair loop and the persisted build_log show every
    /// command that ran, not just the one that blew up.
    /// </summary>
    protected static BuildResult Fail(BuildResult failed, StringBuilder transcript, List<BuildStepResult> steps) => new()
    {
        ExitCode = failed.ExitCode,
        StandardOutput = transcript.ToString(),
        ErrorOutput = failed.ErrorOutput,
        Steps = steps,
    };

    private static void Append(StringBuilder transcript, string step, BuildResult result)
    {
        transcript.AppendLine(CultureInfo.InvariantCulture, $"$ {step}  (exit {result.ExitCode})");
        transcript.AppendLine(result.StandardOutput);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorOutput))
            transcript.AppendLine(result.ErrorOutput);
        transcript.AppendLine();
    }
}
