using System.Diagnostics;
using System.Threading.Channels;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Telemetry;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Background service that consumes <see cref="GenerationContext"/> jobs from the in-process
/// channel, runs <c>dotnet build</c>, zips and uploads to Cloudflare R2 on success, and
/// updates Supabase with real-time status after every state transition.
/// </summary>
public sealed partial class CompileWorkerService(
    ChannelReader<GenerationContext> jobQueue,
    ICompileService compileService,
    ILlmClient llmClient,
    IReconstructionService reconstructionService,
    IR2UploadService r2UploadService,
    IDeliveryService deliveryService,
    IEmailService emailService,
    IRefundService refundService,
    IInFlightGenerationRegistry inFlight,
    ILogger<CompileWorkerService> logger) : BackgroundService
{
    private const int MaxRetries = 3;

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
            catch (Exception ex)
            {
                LogJobUnhandledError(logger, ex, job.GenerationId);
                job.State = GenerationState.Failed;
                job.ErrorMessage = ex.Message;

                Meters.Failed.Add(1, BuildJobTags(job, stage: "worker_unhandled"));
                Meters.DurationMs.Record(
                    (DateTimeOffset.UtcNow - job.StartedAt).TotalMilliseconds,
                    BuildJobTags(job, stage: "worker_unhandled", outcome: "failed"));

                await deliveryService.UpdateStatusAsync(
                    job.GenerationId, GenerationState.Failed,
                    errorMessage: ex.Message,
                    errorCategory: ErrorCategorizer.Categorize(ex),
                    ct: stoppingToken);
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
                await FailMissingOutputDir(job, ct);
                return;
            }

            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Packing, ct: ct);
            await PackUploadAndNotifyAsync(job, ct);
            return;
        }

        while (job.State == GenerationState.Building && job.RetryCount <= MaxRetries)
        {
            if (job.OutputDirectory is null)
            {
                await FailMissingOutputDir(job, ct);
                return;
            }

            // ── Notify Supabase: building ─────────────────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Building, ct: ct);

            // ── Run dotnet build ──────────────────────────────────────────────
            await deliveryService.AppendBuildLogAsync(
                job.GenerationId,
                $"[Attempt {job.RetryCount + 1}] Running {job.ProjectType} build validation...",
                ct);

            var buildResult = await compileService.ExecuteBuildAsync(job.OutputDirectory, job.ProjectType, ct);

            // Stream build output to Supabase
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
                job.State = GenerationStateMachine.Transition(
                    job.State, GenerationEvent.BuildPassed, job);

                await deliveryService.UpdateStatusAsync(
                    job.GenerationId, GenerationState.Packing, ct: ct);

                await PackUploadAndNotifyAsync(job, ct);
                return;
            }

            // ── Build failed — extract errors and decide: retry or fail ───────
            var buildLog = CombineBuildOutput(buildResult);
            var errors = compileService.ExtractBuildErrors(buildLog, job.ProjectType);

            // No matcher fired: send the tail of the raw transcript rather than nothing.
            // An empty error section makes the repair prompt unusable, and three unusable
            // prompts end in TryIssueCompileGuaranteeRefundAsync — a real refund on a
            // failure shape we simply failed to parse.
            var errorSummary = errors.Count > 0
                ? string.Join("\n", errors)
                : TailOf(buildLog, RawLogTailChars);

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
                job.ErrorMessage =
                    $"Build failed after {MaxRetries} retries. Last errors:\n{errorSummary}";

                LogJobFailedPermanently(logger, job.GenerationId, MaxRetries);

                Meters.Failed.Add(1, BuildJobTags(job, stage: "build_retries_exhausted"));
                Meters.DurationMs.Record(
                    (DateTimeOffset.UtcNow - job.StartedAt).TotalMilliseconds,
                    BuildJobTags(job, outcome: "failed"));

                await deliveryService.UpdateStatusAsync(
                    job.GenerationId, GenerationState.Failed,
                    errorMessage: job.ErrorMessage,
                    errorCategory: ErrorCategorizer.Build,
                    ct: ct);

                // Compile Guarantee: paid tiers (1-3) get an automatic refund when the
                // build is still failing after MaxRetries corrections. Best-effort —
                // see TryIssueCompileGuaranteeRefundAsync for why failures never
                // propagate past this point.
                if (job.Tier >= 1)
                {
                    await TryIssueCompileGuaranteeRefundAsync(job, ct);
                }

                return;
            }

            Meters.BuildRetried.Add(1, BuildJobTags(job));

            // ── Retry: re-call LLM with error context ─────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Generating, ct: ct);

            var retryPrompt = compileService.BuildRetryContext(
                job.Prompt ?? "Generate code", job.BuildErrorHistory, job.RetryCount);

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

            var fixedBlocks = reconstructionService.Parse(llmResponse.Text);

            // Overwrite only the files that changed
            foreach (var (relativePath, content) in fixedBlocks)
            {
                var fullPath = Path.Combine(job.OutputDirectory, relativePath);
                var dir = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, content);
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
        var downloadUrl = await r2UploadService.UploadZipAsync(
            job.GenerationId, job.OutputDirectory!, ct);
        job.DownloadUrl = downloadUrl;

        // Packing → Uploading
        job.State = GenerationStateMachine.Transition(
            job.State, GenerationEvent.ZipCreated, job);

        await deliveryService.UpdateStatusAsync(
            job.GenerationId, GenerationState.Uploading, ct: ct);

        // Uploading → Success
        job.State = GenerationStateMachine.Transition(
            job.State, GenerationEvent.UploadedToR2, job);

        await deliveryService.UpdateStatusAsync(
            job.GenerationId, GenerationState.Success,
            downloadUrl: downloadUrl, ct: ct);

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
    /// Attempts the Compile Guarantee refund for a paid-tier generation that just
    /// exhausted all build-correction retries, then emails the customer once the
    /// refund is actually initiated. Deliberately swallows every exception: a
    /// refund (or Supabase/Stripe) failure here must never crash the worker loop
    /// or reverse the generation's already-persisted Failed status — the
    /// charge.refunded webhook remains the eventual source of truth regardless of
    /// whether this call succeeds.
    /// </summary>
    private async Task TryIssueCompileGuaranteeRefundAsync(GenerationContext job, CancellationToken ct)
    {
        try
        {
            var outcome = await refundService.RefundFailedGenerationAsync(job.GenerationId, ct);
            if (outcome != RefundOutcome.Issued)
            {
                LogRefundNotIssued(logger, job.GenerationId, outcome);
                return;
            }

            LogRefundIssued(logger, job.GenerationId);

            var ownerEmail = await deliveryService.GetGenerationOwnerEmailAsync(job.GenerationId, ct);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                var (subject, html) = EmailTemplates.RefundIssued(job.Tier);
                await emailService.SendAsync(ownerEmail, subject, html, ct);
            }
        }
        catch (Exception ex)
        {
            LogRefundAttemptFailed(logger, ex, job.GenerationId);
        }
    }

    private async Task FailMissingOutputDir(GenerationContext job, CancellationToken ct)
    {
        job.State = GenerationState.Failed;
        job.ErrorMessage = "No output directory set on the generation context.";

        Meters.Failed.Add(1, BuildJobTags(job, stage: "missing_output_dir"));
        Meters.DurationMs.Record(
            (DateTimeOffset.UtcNow - job.StartedAt).TotalMilliseconds,
            BuildJobTags(job, stage: "missing_output_dir", outcome: "failed"));

        await deliveryService.UpdateStatusAsync(
            job.GenerationId, GenerationState.Failed,
            errorMessage: job.ErrorMessage,
            errorCategory: ErrorCategorizer.Internal,
            ct: ct);
    }

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

    [LoggerMessage(EventId = 608, Level = LogLevel.Information, Message = "Compile-guarantee refund issued for generation {Id}")]
    private static partial void LogRefundIssued(ILogger logger, string id);

    [LoggerMessage(EventId = 609, Level = LogLevel.Information, Message = "Compile-guarantee refund not issued for generation {Id}: {Outcome}")]
    private static partial void LogRefundNotIssued(ILogger logger, string id, RefundOutcome outcome);

    [LoggerMessage(EventId = 610, Level = LogLevel.Error, Message = "Compile-guarantee refund attempt threw for generation {Id} — worker continues, generation stays Failed")]
    private static partial void LogRefundAttemptFailed(ILogger logger, Exception ex, string id);
}
