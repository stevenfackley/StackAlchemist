using System.Diagnostics;
using System.Globalization;
using System.Text;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public abstract partial class BuildStrategyBase(ILogger logger, BuildStrategyOptions? options = null) : IBuildStrategy
{
    /// <summary>
    /// Exit code recorded for a step killed at its deadline: the one <c>timeout(1)</c> uses, so a
    /// reader of the transcript recognises it.
    /// </summary>
    public const int StepTimedOutExitCode = 124;

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

    /// <summary>
    /// How long one build command may run (StackAlchemist#455). The host's stopping token used to
    /// be the only cancellation a step ever saw, so a hung install held the in-process compile
    /// worker, and every generation queued behind it, until the next deploy.
    /// </summary>
    internal TimeSpan StepTimeout { get; } = (options ?? new BuildStrategyOptions()).StepTimeout;

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
        // Children run LLM-generated code: they get a toolchain allowlist, never the Engine's
        // secrets (DATABASE_URL, Stripe/Anthropic/R2 keys, …). See BuildProcessEnvironment.
        BuildProcessEnvironment.Apply(psi.Environment);

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Drain both pipes at once. Reading stdout to EOF before touching stderr deadlocks the
        // moment the child fills the stderr pipe buffer (a few KB on Windows, 64 KB on Linux): the
        // child blocks on its write, so stdout never closes. `npm ci` on the V2-DotNet-NextJs
        // tree writes ~10 KB of peer-dependency warnings to stderr and hung here indefinitely.
        // The reads are not cancellable on purpose: they end when the child (or the kill below)
        // closes the pipes, so output is never torn mid-stream.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(ct);
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must END the child, not just stop waiting for it: otherwise a hung
            // toolchain (an install waiting on the network, a build that never exits) keeps
            // running, and keeps the job directory, after the worker has moved on. Killed here,
            // explicitly, rather than from a `ct.Register` callback: callbacks run newest-first,
            // so the wait's own cancellation could complete, unwind this method and dispose that
            // registration before it ever fired — which is exactly what happened under load.
            // Whole tree: npm, dotnet and pip spawn children that hold the pipes open.
            KillProcessTree(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new BuildResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            ErrorOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
        };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (AggregateException)
        {
            // Some descendant could not be killed (gone, or not ours); the root was attempted.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The OS refused or the process is exiting; nothing more to do.
        }
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
        var result = await RunWithDeadlineAsync(displayCommand, fileName, arguments, workingDirectory, ct);
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
    /// <see cref="RunProcessAsync"/> under the step deadline. The deadline cancels the same token
    /// the host's shutdown does, so <see cref="RunProcessAsync"/> kills the process tree either
    /// way; only the cause decides what happens next. A deadline is the build's failure, recorded
    /// as a failed step the repair loop and the refund logic handle like any other. A shutdown is
    /// not the generated code's fault, so it still propagates as cancellation.
    /// </summary>
    private async Task<BuildResult> RunWithDeadlineAsync(
        string displayCommand,
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(StepTimeout);
        try
        {
            return await RunProcessAsync(fileName, arguments, workingDirectory, deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            var limit = Describe(StepTimeout);
            LogStepTimedOut(Logger, displayCommand, limit);
            return new BuildResult
            {
                ExitCode = StepTimedOutExitCode,
                StandardOutput = string.Empty,
                ErrorOutput =
                    $"`{displayCommand}` timed out after {limit} and was stopped. A dependency install "
                    + "waiting on the network, or a command that never exits, is the usual cause.",
                TimedOut = true,
            };
        }
    }

    /// <summary>"10 minutes", "1 minute", "0.3 seconds": whole minutes when it is whole minutes.</summary>
    private static string Describe(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1) || span.Ticks % TimeSpan.TicksPerMinute != 0)
            return string.Create(CultureInfo.InvariantCulture, $"{span.TotalSeconds:0.###} seconds");

        var minutes = span.Ticks / TimeSpan.TicksPerMinute;
        return minutes == 1 ? "1 minute" : string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes");
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
        TimedOut = failed.TimedOut,
    };

    private static void Append(StringBuilder transcript, string step, BuildResult result)
    {
        transcript.AppendLine(CultureInfo.InvariantCulture, $"$ {step}  (exit {result.ExitCode})");
        transcript.AppendLine(result.StandardOutput);
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorOutput))
            transcript.AppendLine(result.ErrorOutput);
        transcript.AppendLine();
    }

    [LoggerMessage(EventId = 1050, Level = LogLevel.Warning, Message = "Build step `{Command}` timed out after {Limit}; its process tree was killed")]
    private static partial void LogStepTimedOut(ILogger logger, string command, string limit);
}

/// <summary>Limits applied to every Compile Guarantee build step.</summary>
public sealed class BuildStrategyOptions
{
    public static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Per-command deadline (<c>Generation:BuildStepTimeoutMinutes</c>, default 10). Generous for a
    /// cold <c>npm ci</c> or <c>next build</c> of a generated app, short enough that a stalled
    /// registry fails the attempt instead of holding the compile worker indefinitely.
    /// </summary>
    public TimeSpan StepTimeout { get; init; } = DefaultStepTimeout;
}
