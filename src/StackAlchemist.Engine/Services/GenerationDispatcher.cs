using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>Starts a generation without making the caller wait for it.</summary>
public interface IGenerationDispatcher
{
    /// <summary>Returns at once. The generation runs in the background until it ends or the host stops.</summary>
    void Dispatch(GenerateRequest request);
}

/// <summary>
/// Runs <see cref="IGenerationOrchestrator.EnqueueAsync"/> on the app-lifetime token, never on an
/// HTTP request's. For tiers 2 and 3 that call includes the LLM code generation, which takes
/// minutes. On a request token, whoever hangs up first cancels the build: the browser, the proxy's
/// read timeout, or Stripe giving up on a webhook delivery it expects answered in seconds.
///
/// Fire-and-forget is safe here because every engine service is a singleton (nothing is disposed
/// when the request ends) and EnqueueAsync records its own failures on the row. A throw before it
/// gets that far is logged; the row is still pending, so the reconciler re-enqueues it once it
/// goes stale (a paid row only when its payment is recorded, which the webhook does first).
/// </summary>
public sealed partial class BackgroundGenerationDispatcher(
    IGenerationOrchestrator orchestrator,
    IHostApplicationLifetime lifetime,
    ILogger<BackgroundGenerationDispatcher> logger) : IGenerationDispatcher
{
    public void Dispatch(GenerateRequest request)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await orchestrator.EnqueueAsync(request, lifetime.ApplicationStopping);
            }
            catch (Exception ex)
            {
                LogDispatchFailed(logger, ex, request.GenerationId);
            }
        });
    }

    [LoggerMessage(EventId = 409, Level = LogLevel.Error, Message = "Background generation dispatch failed for {GenerationId}; the row is left for the reconciler")]
    private static partial void LogDispatchFailed(ILogger logger, Exception ex, string generationId);
}
