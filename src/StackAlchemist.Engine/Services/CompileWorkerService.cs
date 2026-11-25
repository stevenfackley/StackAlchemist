using System.Diagnostics;
using System.Threading.Channels;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Telemetry;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Background service that consumes <see cref="GenerationContext"/> jobs from the in-process
/// channel, runs <c>dotnet build</c>, zips and uploads to Cloudflare R2 on success, and
/// writes the generation's status to the store after every state transition.
/// </summary>
public sealed partial class CompileWorkerService(
    ChannelReader<GenerationContext> jobQueue,
    ICompileService compileService,
    ILlmClient llmClient,
    IReconstructionService reconstructionService,
    IR2UploadService r2UploadService,
    IDeliveryService deliveryService,
    IEmailService emailService,
    IFailedBuildRefunder refunder,
    IInFlightGenerationRegistry inFlight,
    ILogger<CompileWorkerService> logger) : BackgroundService
{
    /// <summary>How much raw transcript to fall back on when no error matcher fired.</summary>
    private const int RawLogTailChars = 4_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger);

        await foreach (var job in jobQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The host is stopping mid-job. That is not a failure of the build: no failed
                // status, and above all no refund. The row keeps its in-progress status and the
                // reconciler settles it after the restart.
                LogJobInterruptedByShutdown(logger, job.GenerationId, job.State);
            }
            catch (Exception ex) when (job.State == GenerationState.Success)
            {
                // Delivered: the archive is uploaded and the row says success. What threw came
                // after that (the owner lookup or the completion email). Failing the row here
                // flipped a delivered build to failed, and a refund would pay back a customer
                // who has their download.
                LogPostDeliveryStepFailed(logger, ex, job.GenerationId);
            }
            catch (Exception ex)
            {
                LogJobUnhandledError(logger, ex, job.GenerationId);

                try
                {
                    // A paid build that died here with no retry left for the customer (a category
                    // the result page offers no Retry for, or every retry used) is refunded.
                    await FailJobAsync(
                        job, ex.Message, ErrorCategorizer.Categorize(ex), stage: "worker_unhandled");
                }
                catch (Exception writeEx)
                {
                    // The failed status could not be written. Letting that out of the loop ended
                    // the worker, and every job still queued with it. The row keeps the status it
                    // has and the reconciler settles it once it is stale.
                    LogFailedStatusNotRecorded(logger, writeEx, job.GenerationId);
                }
            }
            finally
            {
                // The orchestrator registered the id at enqueue; the job is terminal
                // (or dead) by here either way, so the reconciler may now see it.
                inFlight.Remove(job.GenerationId);
                CleanupTempDirectory(job);
            }
        }
    }

    private static TagList BuildJobTags(GenerationContext job, string? stage = null, string? outcome = null)
    {
        var t = new TagList
        {
            { "tier", job.Tier },
            { "project_type", job.ProjectType.ToString() },
            { "mode", job.Mode },
        };
        if (stage is not null) t.Add("stage", stage);
        if (outcome is not null) t.Add("outcome", outcome);
        return t;
    }

    private async Task ProcessJobAsync(GenerationContext job, CancellationToken ct)
    {
        LogProcessingJob(logger, job.GenerationId, job.State, job.RetryCount);

        // Tier 1 (Blueprint) path — orchestrator handed us an already-packing job
        // with schema.json + api-docs.md on disk and no build to run.
        if (job.State == GenerationState.Packing)
        {
            if (job.OutputDirectory is null)
            {
                await FailMissingOutputDir(job);
                return;
            }

            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Packing, ct: ct);
            await PackUploadAndNotifyAsync(job, ct);
            return;
        }

        // The state machine's budget, not a copy of it: only its BuildFailed transition ends this
        // loop in Failed, so a bound that disagreed would leave a job still building, with no
        // failed status and no refund.
        while (job.State == GenerationState.Building && job.RetryCount <= GenerationStateMachine.MaxBuildRetries)
        {
            if (job.OutputDirectory is null)
            {
                await FailMissingOutputDir(job);
                return;
            }

            // ── Record status: building ───────────────────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Building, ct: ct);

            // ── Run dotnet build ──────────────────────────────────────────────
            await deliveryService.AppendBuildLogAsync(
                job.GenerationId,
                $"[Attempt {job.RetryCount + 1}] Running {job.ProjectType} build validation...",
                ct);

            var attempt = new BuildAttemptRecord
            {
                Attempt = job.RetryCount + 1,
                StartedAt = DateTimeOffset.UtcNow,
            };
            job.BuildAttempts.Add(attempt);

            var buildResult = await compileService.ExecuteBuildAsync(job.OutputDirectory, job.ProjectType, ct);

            attempt.CompletedAt = DateTimeOffset.UtcNow;
            attempt.Passed = buildResult.IsSuccess;
            attempt.Steps = buildResult.Steps;
            attempt.Output = buildResult.StandardOutput;

            // Stream build output to the generation's build log
            if (!string.IsNullOrWhiteSpace(buildResult.StandardOutput))
            {
                await deliveryService.AppendBuildLogAsync(
                    job.GenerationId, buildResult.StandardOutput, ct);
            }

            if (buildResult.IsSuccess)
            {
                await deliveryService.AppendBuildLogAsync(
                    job.GenerationId, "BUILD SUCCEEDED", ct);
                // Building → Packing
                await AdvanceAsync(job, GenerationEvent.BuildPassed, ct);

                await PackUploadAndNotifyAsync(job, ct);
                return;
            }

            // ── Build failed — extract errors and decide: retry or fail ───────
            var buildLog = CombineBuildOutput(buildResult);
            var errors = compileService.ExtractBuildErrors(buildLog, job.ProjectType);

            // No matcher fired: send the tail of the raw transcript rather than nothing.
            // An empty error section makes the repair prompt unusable, and three unusable
            // prompts end in the Compile Guarantee refund — a real refund on a
            // failure shape we simply failed to parse.
            var errorSummary = errors.Count > 0
                ? string.Join("\n", errors)
                : TailOf(buildLog, RawLogTailChars);

            // The report records the PARSED errors, not the raw-tail fallback: the fallback
            // is repair-prompt material, and a list of transcript lines masquerading as
            // "errors" would be worse than an honest empty list in a customer-facing file.
            attempt.Errors = errors;

            job.BuildErrorHistory.Add($"Attempt {job.RetryCount + 1}: {errorSummary}");

            await deliveryService.AppendBuildLogAsync(
                job.GenerationId,
                $"BUILD FAILED (attempt {job.RetryCount + 1})\n{errorSummary}",
                ct);

            LogBuildFailed(logger, job.GenerationId, job.RetryCount + 1, errorSummary);

            var newState = GenerationStateMachine.Transition(
                job.State, GenerationEvent.BuildFailed, job);
            job.State = newState;

            if (newState == GenerationState.Failed)
            {
                LogJobFailedPermanently(logger, job.GenerationId, GenerationStateMachine.MaxBuildRetries);

                // Compile Guarantee: the build category is how a paid tier (1-3) gets its
                // automatic refund when the build is still failing after every correction.
                // Best-effort: the refunder never throws, so a refund failure cannot crash the
                // worker loop or reverse the Failed status. The duration is tagged by outcome
                // alone, like a delivered build's.
                await FailJobAsync(
                    job, $"Build failed after {GenerationStateMachine.MaxBuildRetries} retries. Last errors:\n{errorSummary}",
                    ErrorCategorizer.Build, stage: "build_retries_exhausted", tagDurationWithStage: false);

                return;
            }

            Meters.BuildRetried.Add(1, BuildJobTags(job));

            // ── Retry: re-call LLM with error context ─────────────────────────
            // Generating, which the row stores as "generating", not the "generating_code" the
            // orchestrator wrote when the generation started.
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, job.State, ct: ct);

            // job.Prompt is the user's natural-language brief — it says nothing about where
            // files go, so without the tree roots this is the one prompt in the pipeline that
            // asks for [[FILE:path]] blocks while stating no paths, and it is the prompt that
            // only ever runs when a paid generation is already failing.
            var retryPrompt = compileService.BuildRetryContext(
                job.Prompt ?? "Generate code", job.BuildErrorHistory, job.RetryCount,
                TreeRootsOf(job.OutputDirectory));

            // Reuse the SAME resolved credential/model the orchestrator used for codegen, so a
            // BYOK build-repair hits the user's provider/key — not the global Anthropic default.
            var llmResponse = await llmClient.GenerateAsync(
                "Fix the compilation errors in the generated code.", retryPrompt, job.LlmOptions, ct);
            await deliveryService.UpdateTokenUsageAsync(
                job.GenerationId,
                llmResponse.InputTokens,
                llmResponse.OutputTokens,
                llmResponse.Model,
                ct);

            // Don't apply a truncated fix: half-written files guarantee the next build
            // fails too, burning the remaining retries on reproducible truncation.
            LlmResponseGuard.ThrowIfTruncated(llmResponse, "fixing the compilation errors");

            // Route the repair through the same rule as the first pass: a corrected file may
            // only land inside the tree the customer's archive actually has. Writing blindly
            // here re-opened every hole Reconstruct closes — an orphan src/ at the archive
            // root, a __zone__/ fragment shipped as a literal file, a `..` escaping the
            // output directory — because this is a second, later write site.
            var repair = reconstructionService.ResolveRepairWrites(
                reconstructionService.Parse(llmResponse.Text), job.OutputDirectory);

            // Overwrite only the files that changed
            foreach (var (relativePath, content) in repair.Writes)
            {
                var fullPath = Path.Combine(job.OutputDirectory, relativePath);
                var dir = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, content);

                // "Files that were corrected and the corrections applied" — the archive's
                // build-report.json is the customer's only record of what the repair loop
                // touched once the temp directory is gone.
                attempt.CorrectedFiles.Add(relativePath);
            }

            // Refused blocks are dropped, never written — but never silently: the build log is
            // the customer's view of the repair loop, and this is why the next attempt still
            // fails on the same errors.
            if (repair.RejectionNotice is not null)
            {
                LogRepairBlocksRejected(logger, job.GenerationId, string.Join(", ", repair.Rejected));
                await deliveryService.AppendBuildLogAsync(job.GenerationId, repair.RejectionNotice, ct);
            }

            // Generating → Building
            job.State = GenerationStateMachine.Transition(
                job.State, GenerationEvent.CodeReconstructed, job);
        }
    }

    /// <summary>
    /// Everything the build wrote, for error extraction.
    ///
    /// <see cref="DotNetBuildStrategy"/> puts the whole multi-command transcript in
    /// <see cref="BuildResult.StandardOutput"/> and only the failing step's stderr in
    /// <see cref="BuildResult.ErrorOutput"/>. `next build` reports type errors on stdout,
    /// so reading ErrorOutput alone — as this used to — loses the frontend failure
    /// entirely the moment the tool writes anything at all to stderr.
    /// </summary>
    internal static string CombineBuildOutput(BuildResult result)
    {
        var stdout = result.StandardOutput ?? string.Empty;
        var stderr = result.ErrorOutput ?? string.Empty;

        if (string.IsNullOrWhiteSpace(stderr) || string.Equals(stdout, stderr, StringComparison.Ordinal))
            return stdout;
        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;

        return stdout + "\n" + stderr;
    }

    /// <summary>Last <paramref name="maxChars"/> characters of <paramref name="log"/>, marked when clipped.</summary>
    internal static string TailOf(string log, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(log))
            return string.Empty;

        var trimmed = log.TrimEnd();
        return trimmed.Length <= maxChars
            ? trimmed
            : "… (build log truncated)\n" + trimmed[^maxChars..];
    }

    /// <summary>
    /// Zips <paramref name="job"/>'s output directory, uploads to R2, transitions
    /// Packing → Uploading → Success, and emails the customer the download URL.
    /// Used by both the Tier-2/3 build-success path and the Tier-1 skip-build path.
    /// </summary>
    private async Task PackUploadAndNotifyAsync(GenerationContext job, CancellationToken ct)
    {
        await TryWriteBuildReportAsync(job, ct);

        var downloadUrl = await r2UploadService.UploadZipAsync(
            job.GenerationId, job.OutputDirectory!, ct);
        job.DownloadUrl = downloadUrl;

        // Packing → Uploading
        await AdvanceAsync(job, GenerationEvent.ZipCreated, ct);

        // Uploading → Success
        await AdvanceAsync(job, GenerationEvent.UploadedToR2, ct, downloadUrl);

        Meters.Succeeded.Add(1, BuildJobTags(job));
        Meters.DurationMs.Record(
            (DateTimeOffset.UtcNow - job.StartedAt).TotalMilliseconds,
            BuildJobTags(job, outcome: "succeeded"));

        LogJobCompleted(logger, job.GenerationId, job.RetryCount);

        var ownerEmail = await deliveryService.GetGenerationOwnerEmailAsync(job.GenerationId, ct);
        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var (subject, html) = EmailTemplates.GenerationComplete(downloadUrl);
            await emailService.SendAsync(ownerEmail, subject, html, ct);
        }
    }

    /// <summary>
    /// Writes <c>build-report.json</c> into the archive root and appends its verdict to the
    /// streamed build log, which is what the delivery UI badges each half from.
    ///
    /// No-ops for Tier 1, whose Blueprint deliverable never goes near a compiler — a report
    /// claiming otherwise would be exactly the kind of unearned "verified" this exists to
    /// stop. Best-effort: a generation that compiled and uploaded must not be failed over an
    /// unwritable metadata file, so the customer still gets the archive and the log says the
    /// report is missing.
    ///
    /// Called only from <see cref="PackUploadAndNotifyAsync"/>, which is deliberate and is
    /// the whole reason compile-guarantee.md tells customers a failed build ships no report:
    /// the retries-exhausted branch above sets Failed, refunds, and returns without packing,
    /// so there is no archive to write into. Moving this call to that branch without also
    /// producing an archive would write a report nobody can ever read.
    /// </summary>
    private async Task TryWriteBuildReportAsync(GenerationContext job, CancellationToken ct)
    {
        if (job.BuildAttempts.Count == 0 || job.OutputDirectory is null)
            return;

        try
        {
            var report = BuildReportWriter.Create(job, GenerationStateMachine.MaxBuildRetries, DateTimeOffset.UtcNow);
            await BuildReportWriter.WriteAsync(job.OutputDirectory, report, ct);
            await deliveryService.AppendBuildLogAsync(
                job.GenerationId, BuildReportWriter.ToLogSummary(report), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogBuildReportWriteFailed(logger, ex, job.GenerationId);
        }
    }

    /// <summary>
    /// Moves the job on by <paramref name="trigger"/> and writes the state it lands in, so the row
    /// cannot be told a different state from the one the job is in.
    /// </summary>
    private async Task AdvanceAsync(
        GenerationContext job, GenerationEvent trigger, CancellationToken ct, string? downloadUrl = null)
    {
        job.State = GenerationStateMachine.Transition(job.State, trigger, job);
        await deliveryService.UpdateStatusAsync(job.GenerationId, job.State, downloadUrl: downloadUrl, ct: ct);
    }

    private Task FailMissingOutputDir(GenerationContext job) =>
        FailJobAsync(
            job, "No output directory set on the generation context.",
            ErrorCategorizer.Internal, stage: "missing_output_dir");

    /// <summary>
    /// Every way a job ends failed in this worker: records the failure meters, writes the failed
    /// status, then refunds the build when the failure leaves the customer no retry
    /// (<see cref="FailedBuildRefunder.ShouldRefund"/>). Neither the write nor the refund rides
    /// the host's stopping token. Nothing is refunded when the status write throws.
    /// </summary>
    /// <param name="stage">Tags the failure count, and the duration unless
    /// <paramref name="tagDurationWithStage"/> is false.</param>
    private async Task FailJobAsync(
        GenerationContext job,
        string errorMessage,
        string errorCategory,
        string stage,
        bool tagDurationWithStage = true)
    {
        job.State = GenerationState.Failed;
        job.ErrorMessage = errorMessage;

        Meters.Failed.Add(1, BuildJobTags(job, stage: stage));
        Meters.DurationMs.Record(
            (DateTimeOffset.UtcNow - job.StartedAt).TotalMilliseconds,
            BuildJobTags(job, stage: tagDurationWithStage ? stage : null, outcome: "failed"));

        // CancellationToken.None, as the orchestrator's failed write: a host that starts stopping
        // right now must still record it. On the stopping token the write was dropped, while the
        // refund below, which never rode that token, still went out.
        await deliveryService.UpdateStatusAsync(
            job.GenerationId, GenerationState.Failed,
            errorMessage: errorMessage,
            errorCategory: errorCategory,
            ct: CancellationToken.None);

        await refunder.RefundIfOwedAsync(job.GenerationId, job.Tier, errorCategory);
    }

    /// <summary>
    /// The top-level directories of the generated project as it stands on disk — the roots a
    /// repair block's path is allowed to start with, and the list the retry prompt quotes.
    /// </summary>
    private static IReadOnlyCollection<string> TreeRootsOf(string outputDirectory)
        => [.. Directory.EnumerateDirectories(outputDirectory)
                        .Select(d => Path.GetFileName(d)!)
                        .Order(StringComparer.Ordinal)];

    private void CleanupTempDirectory(GenerationContext job)
    {
        if (job.OutputDirectory is null || !Directory.Exists(job.OutputDirectory))
            return;

        try
        {
            Directory.Delete(job.OutputDirectory, recursive: true);
            LogTempDirCleaned(logger, job.GenerationId);
        }
        catch (Exception ex)
        {
            LogTempDirCleanupFailed(logger, ex, job.GenerationId);
        }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 600, Level = LogLevel.Information, Message = "Compile worker started, waiting for jobs…")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 601, Level = LogLevel.Error, Message = "Unhandled error processing generation {Id}")]
    private static partial void LogJobUnhandledError(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 602, Level = LogLevel.Information, Message = "Processing generation {Id}  state={State}  retries={Retries}")]
    private static partial void LogProcessingJob(ILogger logger, string id, GenerationState state, int retries);

    [LoggerMessage(EventId = 603, Level = LogLevel.Information, Message = "Generation {Id} completed successfully after {Retries} retries")]
    private static partial void LogJobCompleted(ILogger logger, string id, int retries);

    [LoggerMessage(EventId = 604, Level = LogLevel.Warning, Message = "Build failed for {Id} (attempt {Attempt}): {Errors}")]
    private static partial void LogBuildFailed(ILogger logger, string id, int attempt, string errors);

    [LoggerMessage(EventId = 605, Level = LogLevel.Error, Message = "Generation {Id} failed permanently after {Max} retries")]
    private static partial void LogJobFailedPermanently(ILogger logger, string id, int max);

    [LoggerMessage(EventId = 606, Level = LogLevel.Information, Message = "Cleaned up temp directory for {Id}")]
    private static partial void LogTempDirCleaned(ILogger logger, string id);

    [LoggerMessage(EventId = 607, Level = LogLevel.Warning, Message = "Failed to clean up temp directory for {Id}")]
    private static partial void LogTempDirCleanupFailed(ILogger logger, Exception ex, string id);

    // 608-610 (the refund outcome) moved to FailedBuildRefunder.

    [LoggerMessage(EventId = 613, Level = LogLevel.Warning, Message = "Generation {Id} interrupted by host shutdown in state {State}; left for the reconciler")]
    private static partial void LogJobInterruptedByShutdown(ILogger logger, string id, GenerationState state);

    [LoggerMessage(EventId = 614, Level = LogLevel.Error, Message = "Generation {Id} was delivered, but a step after delivery threw (owner lookup or completion email); the row stays success")]
    private static partial void LogPostDeliveryStepFailed(ILogger logger, Exception ex, string id);

    // 616, not 615: FailedBuildRefunder has 615.
    [LoggerMessage(EventId = 616, Level = LogLevel.Error, Message = "Could not record generation {Id} as failed; its row keeps the status it has and is left for the reconciler")]
    private static partial void LogFailedStatusNotRecorded(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 611, Level = LogLevel.Warning, Message = "Could not write build-report.json for generation {Id} — archive ships without it")]
    private static partial void LogBuildReportWriteFailed(ILogger logger, Exception ex, string id);

    // 612, not 611: this branch was written against a tree where 611 was free, and
    // build-report.json claimed it first. Duplicate ids inside one class are a SYSLIB1006
    // build error, not a style nit.
    [LoggerMessage(EventId = 612, Level = LogLevel.Warning, Message = "Build-repair blocks refused for generation {Id} — outside the project tree, not written: {Paths}")]
    private static partial void LogRepairBlocksRejected(ILogger logger, string id, string paths);
}
