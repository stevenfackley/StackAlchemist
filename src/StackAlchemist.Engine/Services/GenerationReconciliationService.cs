using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Periodic reconciler for generation rows orphaned by a restart or stalled in flight.
/// Replaces the old one-shot startup sweep, which (a) only ran once, so a row that went
/// stale after boot hung until the next deploy, and (b) could only FAIL rows — now,
/// early-stage rows with attempt budget left are RE-ENQUEUED instead, so an engine
/// restart costs the user a delay, not their generation.
///
/// Each tick: (1) flush buffered critical writes (a rescued terminal write beats
/// failing the row), (2) list stale non-terminal rows, (3) skip rows this instance is
/// actively processing, (4) leave unpaid paid-tier checkouts alone (and fail them once
/// their Checkout Session can no longer be paid), (5) re-enqueue pending/generating_code
/// rows under budget via a CAS claim, (6) conditionally fail the rest with a cause-specific
/// message.
///
/// Step (4) closes #421: the web inserts a paid-tier row as `pending` before the user pays,
/// and the Stripe webhook is what starts it. Without the payment check an abandoned checkout
/// went stale after 15 minutes and was built for free, up to three times.
/// </summary>
public sealed partial class GenerationReconciliationService(
    IDeliveryService deliveryService,
    IGenerationOrchestrator orchestrator,
    IInFlightGenerationRegistry inFlight,
    IPendingWriteBuffer pendingWrites,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<GenerationReconciliationService> logger,
    IBillingStore? billing = null) : BackgroundService
{
    private const int DefaultStaleMinutes = 15;
    private const int DefaultIntervalMinutes = 5;
    private const int DefaultMaxAttempts = 3; // matches the frontend's attempt_count < 3 retry gate
    private static readonly TimeSpan MaxBufferedWriteAge = TimeSpan.FromHours(1);

    // A Checkout Session can be paid for at most 24 hours after creation; an hour of slack covers
    // the webhook's own retries. Past this, an unpaid paid-tier row can never be paid.
    private static readonly TimeSpan UnpaidCheckoutLifetime = TimeSpan.FromHours(25);

    // Construction time ≈ process start (singleton hosted service, built at host boot).
    private readonly DateTimeOffset _processStartedUtc = timeProvider.GetUtcNow();

    // Statuses safe to re-fire from the top of the pipeline: no on-disk artifacts to
    // lose yet. Later states (building/packing/uploading) reference a temp directory
    // that died with the old process, and extracting_schema is driven by a frontend
    // HTTP call we cannot replay — those fail with a clear message instead.
    private static readonly HashSet<string> RequeueableStatuses = ["pending", "generating_code", "generating"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var staleMinutes = configuration.GetValue("Generation:StaleReconcileMinutes", DefaultStaleMinutes);
        if (staleMinutes <= 0)
        {
            LogReconcileDisabled(logger);
            return;
        }

        var intervalMinutes = configuration.GetValue("Generation:ReconcileIntervalMinutes", DefaultIntervalMinutes);

        // Immediate startup sweep (preserves the old service's behavior), then periodic.
        await RunOnceSafeAsync(TimeSpan.FromMinutes(staleMinutes), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceSafeAsync(TimeSpan.FromMinutes(staleMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private async Task RunOnceSafeAsync(TimeSpan staleWindow, CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(staleWindow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A sweep failure must never kill the host.
            LogReconcileTickFailed(logger, ex);
        }
    }

    internal async Task RunOnceAsync(TimeSpan staleWindow, CancellationToken ct)
    {
        await FlushPendingWritesAsync(ct);

        var maxAttempts = configuration.GetValue("Generation:MaxAttempts", DefaultMaxAttempts);
        var rows = await deliveryService.GetStaleNonTerminalAsync(staleWindow, ct);

        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested)
                return;

            // Alive in this process (e.g. a multi-minute LLM call) — not orphaned.
            if (inFlight.Contains(row.Id))
                continue;

            // A paid tier that is still pending has not started; only a completed payment may
            // start it. Unpaid: wait while the checkout can still complete, then fail the row.
            if (row.Status == "pending" && row.Tier > 0 && !await IsPaidAsync(row.Id, ct))
            {
                if (timeProvider.GetUtcNow() - row.UpdatedAt > UnpaidCheckoutLifetime)
                {
                    await deliveryService.TryFailStaleRowAsync(
                        row, staleWindow, "Checkout was not completed.", ErrorCategorizer.Internal, ct);
                }
                continue;
            }

            if (RequeueableStatuses.Contains(row.Status) && row.AttemptCount < maxAttempts)
            {
                if (!await deliveryService.TryClaimForRequeueAsync(row, staleWindow, ct))
                    continue; // another instance/tick won the claim, or the row progressed

                LogRequeueingOrphan(logger, row.Id, row.Status, row.AttemptCount + 1);
                await orchestrator.EnqueueAsync(new GenerateRequest
                {
                    GenerationId    = row.Id,
                    Mode            = row.Mode ?? "advanced",
                    Tier            = row.Tier,
                    ProjectType     = row.ProjectType ?? ProjectType.DotNetNextJs,
                    Prompt          = row.Prompt,
                    Schema          = row.Schema,
                    Personalization = row.Personalization,
                }, ct);
                continue;
            }

            var message = row.AttemptCount >= maxAttempts
                ? $"Generation failed after {maxAttempts} attempts."
                : row.UpdatedAt < _processStartedUtc
                    ? "Generation did not complete — the engine restarted while this job was in flight."
                    : $"Generation stalled and timed out after {staleWindow.TotalMinutes:0} minutes without progress.";

            await deliveryService.TryFailStaleRowAsync(row, staleWindow, message, ErrorCategorizer.Internal, ct);
        }
    }

    /// <summary>
    /// True when a completed Stripe transaction backs the generation. No billing store (local dev)
    /// means nothing can ever be paid; a lookup failure skips the row this tick rather than risk
    /// building it unpaid.
    /// </summary>
    private async Task<bool> IsPaidAsync(string generationId, CancellationToken ct)
    {
        if (billing is null)
            return false;

        try
        {
            return await billing.FindCompletedTransactionAsync(generationId, ct) is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPaymentLookupFailed(logger, ex, generationId);
            return false;
        }
    }

    private async Task FlushPendingWritesAsync(CancellationToken ct)
    {
        // Snapshot the count so a write that re-buffers can't loop forever in one tick.
        var toFlush = pendingWrites.Count;
        for (var i = 0; i < toFlush && !ct.IsCancellationRequested; i++)
        {
            if (!pendingWrites.TryDequeue(out var write) || write is null)
                return;

            if (await deliveryService.TryPatchOnceAsync(write.GenerationId, write.Payload, ct))
            {
                LogBufferedWriteFlushed(logger, write.GenerationId);
                continue;
            }

            if (timeProvider.GetUtcNow() - write.EnqueuedAtUtc < MaxBufferedWriteAge)
            {
                pendingWrites.Enqueue(write);
            }
            else
            {
                // Past the age cap: the stale sweep below owns the row's fate now —
                // resurrecting an hour-old terminal write could overwrite newer state.
                LogBufferedWriteDropped(logger, write.GenerationId);
            }
        }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 700, Level = LogLevel.Information, Message = "Generation reconciliation disabled (Generation:StaleReconcileMinutes <= 0)")]
    private static partial void LogReconcileDisabled(ILogger logger);

    [LoggerMessage(EventId = 701, Level = LogLevel.Error, Message = "Reconciliation tick failed")]
    private static partial void LogReconcileTickFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 702, Level = LogLevel.Information, Message = "Re-enqueueing orphaned generation {Id} (was {Status}, attempt {Attempt})")]
    private static partial void LogRequeueingOrphan(ILogger logger, string id, string status, int attempt);

    [LoggerMessage(EventId = 703, Level = LogLevel.Information, Message = "Flushed buffered critical write for generation {Id}")]
    private static partial void LogBufferedWriteFlushed(ILogger logger, string id);

    [LoggerMessage(EventId = 704, Level = LogLevel.Error, Message = "Dropped buffered critical write for generation {Id} (older than the age cap) — stale sweep owns the row now")]
    private static partial void LogBufferedWriteDropped(ILogger logger, string id);

    [LoggerMessage(EventId = 705, Level = LogLevel.Warning, Message = "Could not check payment for pending generation {Id} — skipping it this tick")]
    private static partial void LogPaymentLookupFailed(ILogger logger, Exception ex, string id);
}
