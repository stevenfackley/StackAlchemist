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
/// message, and refund the ones that were paid for. A row whose stored schema no longer parses is failed at step (5) instead: it can
/// never be rebuilt, and skipping it left it stale on every tick, forever (#425).
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
    IBillingStore? billing = null,
    IFailedBuildRefunder? refunder = null) : BackgroundService
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

    internal const string UnreadableRowMessage =
        "This generation could not be resumed: its saved schema or settings could not be read. "
        + "Please start a new generation.";

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

            // The job finished in this process and its terminal write (success with the download
            // link, or failed) is still buffered after a store outage: the flush above could not
            // land it this tick. The row is not orphaned. Failing it here would be overwritten by
            // that write on a later tick, and for a paid row the refund below would by then have
            // paid back a customer whose build was delivered. A write that ages out of the buffer
            // is dropped, and the row then comes back to this sweep.
            if (pendingWrites.HasPendingFor(row.Id))
                continue;

            // Only a completed payment may start, or restart, a paid tier. No billing store
            // (local dev): a pending paid row can never be paid, and a started one is left to
            // run, as PaidGenerationGate and the webhook do.
            var payment = row.Tier > 0 && (billing is not null || row.Status == "pending")
                ? await CheckPaymentAsync(row.Id, ct)
                : Payment.Paid;

            // The lookup failed: nothing is known, so nothing is built, failed or refunded.
            if (payment == Payment.Unknown)
                continue;

            // A paid tier that is still pending has not started. Unpaid: wait while the checkout
            // can still complete, then fail the row.
            if (row.Status == "pending" && payment == Payment.Unpaid)
            {
                if (timeProvider.GetUtcNow() - row.UpdatedAt > UnpaidCheckoutLifetime)
                {
                    await deliveryService.TryFailStaleRowAsync(
                        row, staleWindow, "Checkout was not completed.", ErrorCategorizer.Internal, ct);
                }
                continue;
            }

            // After the payment gate on purpose: an unpaid row waits on its checkout like any other.
            if (row.IsUnreadable)
            {
                LogFailingUnreadableRow(logger, row.Id, row.Status);
                await deliveryService.TryFailStaleRowAsync(
                    row, staleWindow, UnreadableRowMessage, ErrorCategorizer.Schema, ct);
                continue;
            }

            // payment == Unpaid past this point means a started paid row whose payment is no
            // longer completed: refunded or disputed while it was in flight. It is never rebuilt
            // (that would deliver a refunded build); it falls through and is failed.
            if (RequeueableStatuses.Contains(row.Status) && row.AttemptCount < maxAttempts && payment == Payment.Paid)
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

            var failed = await deliveryService.TryFailStaleRowAsync(
                row, staleWindow, message, ErrorCategorizer.Internal, ct);

            // A paid build this sweep has just ended: out of attempts, or stranded in a late
            // stage by a restart. The category offers the customer no Retry, so the payment goes
            // back. Only when this sweep won the write: a row that moved on is not ours to
            // refund. The unpaid checkout above never reaches here, and the refund service
            // refunds a completed transaction only.
            // Not on ct: the row is already failed, and a host that starts stopping now would
            // cancel the refund with nothing left to try it again.
            if (failed && refunder is not null && FailedBuildRefunder.ShouldRefund(row.Tier, ErrorCategorizer.Internal))
            {
                await refunder.TryRefundAsync(row.Id, row.Tier, RefundReason.BuildCouldNotFinish, CancellationToken.None);
            }
        }
    }

    private enum Payment
    {
        /// <summary>A completed Stripe transaction backs the generation (or the tier is free).</summary>
        Paid,

        /// <summary>No completed transaction: never paid, refunded, or disputed. No billing store means nothing can ever be paid.</summary>
        Unpaid,

        /// <summary>The lookup failed. The row is skipped this tick rather than acted on blind.</summary>
        Unknown,
    }

    private async Task<Payment> CheckPaymentAsync(string generationId, CancellationToken ct)
    {
        if (billing is null)
            return Payment.Unpaid;

        try
        {
            return await billing.FindCompletedTransactionAsync(generationId, ct) is not null
                ? Payment.Paid
                : Payment.Unpaid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPaymentLookupFailed(logger, ex, generationId);
            return Payment.Unknown;
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

    [LoggerMessage(EventId = 706, Level = LogLevel.Warning, Message = "Failing generation {Id} (was {Status}): its stored schema/personalization JSON could not be read")]
    private static partial void LogFailingUnreadableRow(ILogger logger, string id, string status);
}
