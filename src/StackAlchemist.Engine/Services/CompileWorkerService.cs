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
    ILogger<CompileWorkerService> logger) : BackgroundService
{
    private const int MaxRetries = 3;

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
                    errorMessage: ex.Message, ct: stoppingToken);
            }
            finally
            {
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
            var errors = compileService.ExtractBuildErrors(buildResult.ErrorOutput, job.ProjectType);
            var errorSummary = string.Join("\n", errors);
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
                    errorMessage: job.ErrorMessage, ct: ct);
                return;
            }

            Meters.BuildRetried.Add(1, BuildJobTags(job));

            // ── Retry: re-call LLM with error context ─────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Generating, ct: ct);

            var retryPrompt = compileService.BuildRetryContext(
                job.Prompt ?? "Generate code", job.BuildErrorHistory, job.RetryCount);

            var llmResponse = await llmClient.GenerateAsync(
                "Fix the compilation errors in the generated code.", retryPrompt, ct);
            await deliveryService.UpdateTokenUsageAsync(
                job.GenerationId,
                llmResponse.InputTokens,
                llmResponse.OutputTokens,
                llmResponse.Model,
                ct);

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
            errorMessage: job.ErrorMessage, ct: ct);
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
}
