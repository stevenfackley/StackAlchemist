using System.Threading.Channels;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Background service that consumes <see cref="GenerationContext"/> jobs from the in-process
/// channel, runs <c>dotnet build</c>, zips and uploads to Cloudflare R2 on success, and
/// updates Supabase with real-time status after every state transition.
/// </summary>
public sealed class CompileWorkerService(
    ChannelReader<GenerationContext> jobQueue,
    ICompileService compileService,
    ILlmClient llmClient,
    IReconstructionService reconstructionService,
    IR2UploadService r2UploadService,
    IDeliveryService deliveryService,
    ILogger<CompileWorkerService> logger) : BackgroundService
{
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Compile worker started, waiting for jobs…");

        await foreach (var job in jobQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error processing generation {Id}", job.GenerationId);
                job.State = GenerationState.Failed;
                job.ErrorMessage = ex.Message;

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

    private async Task ProcessJobAsync(GenerationContext job, CancellationToken ct)
    {
        logger.LogInformation(
            "Processing generation {Id}  state={State}  retries={Retries}",
            job.GenerationId, job.State, job.RetryCount);

        while (job.State == GenerationState.Building && job.RetryCount <= MaxRetries)
        {
            if (job.OutputDirectory is null)
            {
                job.State = GenerationState.Failed;
                job.ErrorMessage = "No output directory set on the generation context.";
                await deliveryService.UpdateStatusAsync(
                    job.GenerationId, GenerationState.Failed,
                    errorMessage: job.ErrorMessage, ct: ct);
                return;
            }

            // ── Notify Supabase: building ─────────────────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Building, ct: ct);

            // ── Run dotnet build ──────────────────────────────────────────────
            await deliveryService.AppendBuildLogAsync(
                job.GenerationId,
                $"[Attempt {job.RetryCount + 1}] Running dotnet build...",
                ct);

            var buildResult = await compileService.ExecuteBuildAsync(job.OutputDirectory, ct);

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

                // ── Zip and upload to Cloudflare R2 ───────────────────────────
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

                logger.LogInformation(
                    "Generation {Id} completed successfully after {Retries} retries",
                    job.GenerationId, job.RetryCount);
                return;
            }

            // ── Build failed — extract errors and decide: retry or fail ───────
            var errors = compileService.ExtractBuildErrors(buildResult.ErrorOutput);
            var errorSummary = string.Join("\n", errors);
            job.BuildErrorHistory.Add($"Attempt {job.RetryCount + 1}: {errorSummary}");

            await deliveryService.AppendBuildLogAsync(
                job.GenerationId,
                $"BUILD FAILED (attempt {job.RetryCount + 1})\n{errorSummary}",
                ct);

            logger.LogWarning(
                "Build failed for {Id} (attempt {Attempt}): {Errors}",
                job.GenerationId, job.RetryCount + 1, errorSummary);

            var newState = GenerationStateMachine.Transition(
                job.State, GenerationEvent.BuildFailed, job);
            job.State = newState;

            if (newState == GenerationState.Failed)
            {
                job.ErrorMessage =
                    $"Build failed after {MaxRetries} retries. Last errors:\n{errorSummary}";

                logger.LogError(
                    "Generation {Id} failed permanently after {Max} retries",
                    job.GenerationId, MaxRetries);

                await deliveryService.UpdateStatusAsync(
                    job.GenerationId, GenerationState.Failed,
                    errorMessage: job.ErrorMessage, ct: ct);
                return;
            }

            // ── Retry: re-call LLM with error context ─────────────────────────
            await deliveryService.UpdateStatusAsync(
                job.GenerationId, GenerationState.Generating, ct: ct);

            var retryPrompt = compileService.BuildRetryContext(
                job.Prompt ?? "Generate code", job.BuildErrorHistory, job.RetryCount);

            var llmOutput = await llmClient.GenerateAsync(
                "Fix the compilation errors in the generated code.", retryPrompt, ct);

            var fixedBlocks = reconstructionService.Parse(llmOutput);

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

    private void CleanupTempDirectory(GenerationContext job)
    {
        if (job.OutputDirectory is null || !Directory.Exists(job.OutputDirectory))
            return;

        try
        {
            Directory.Delete(job.OutputDirectory, recursive: true);
            logger.LogInformation("Cleaned up temp directory for {Id}", job.GenerationId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up temp directory for {Id}", job.GenerationId);
        }
    }
}
