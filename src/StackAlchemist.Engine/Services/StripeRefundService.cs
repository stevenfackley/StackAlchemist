using Stripe;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Result of a <see cref="IRefundService.RefundFailedGenerationAsync"/> call.
/// </summary>
public enum RefundOutcome
{
    /// <summary>
    /// No side effect happened: no completed transaction for this generation
    /// (free tier, unpaid, or the checkout hasn't landed yet), or the
    /// transaction is already refunded / refund_pending / disputed. This is
    /// the safe default an unconfigured test double returns.
    /// </summary>
    NotEligible = 0,

    /// <summary>The Stripe refund was created and the transaction claimed as refund_pending.</summary>
    Issued,

    /// <summary>An eligible transaction existed but the Stripe call or a billing-store read/claim failed.</summary>
    Failed,
}

/// <summary>
/// Issues the Compile Guarantee's automatic refund: given a generation that just
/// exhausted all build-correction retries, look up its completed Stripe
/// transaction and refund the full amount on the underlying payment intent.
/// </summary>
public interface IRefundService
{
    Task<RefundOutcome> RefundFailedGenerationAsync(string generationId, CancellationToken ct);
}

/// <summary>
/// Stripe-backed <see cref="IRefundService"/>. This only INITIATES the refund —
/// it atomically claims the transaction (completed → refund_pending) and calls
/// the Stripe refund API. <see cref="StripeWebhookHandler"/>'s
/// <c>charge.refunded</c> handler remains the source of truth that finalizes
/// the row to <c>refunded</c> once Stripe confirms the money actually moved.
///
/// Idempotent by construction: the eligibility lookup and the claim (both in
/// <see cref="IBillingStore"/>) filter on <c>status = 'completed'</c>, so a transaction that's already
/// refunded, already refund_pending, disputed, or was never completed (free
/// tier, failed payment) is skipped without a Stripe call or an exception —
/// including a second call for the same generation after the first succeeded.
/// </summary>
public sealed partial class StripeRefundService(
    IBillingStore? billing,
    IConfiguration config,
    RefundService stripeRefunds,
    ILogger<StripeRefundService> logger) : IRefundService
{
    public async Task<RefundOutcome> RefundFailedGenerationAsync(string generationId, CancellationToken ct)
    {
        var secretKey = config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            LogStripeNotConfigured(logger, generationId);
            return RefundOutcome.NotEligible;
        }

        if (billing is null)
        {
            LogStoreNotConfigured(logger, generationId);
            return RefundOutcome.NotEligible;
        }

        EligibleTransaction? transaction;
        try
        {
            transaction = await billing.FindCompletedTransactionAsync(generationId, ct);
        }
        catch (Exception ex)
        {
            LogRefundLookupFailed(logger, ex, generationId);
            return RefundOutcome.Failed;
        }

        if (transaction is null)
        {
            LogNoEligibleTransaction(logger, generationId);
            return RefundOutcome.NotEligible;
        }

        bool claimed;
        try
        {
            claimed = await billing.TryClaimForRefundAsync(transaction.Id, ct);
        }
        catch (Exception ex)
        {
            LogRefundLookupFailed(logger, ex, generationId);
            return RefundOutcome.Failed;
        }

        if (!claimed)
        {
            // Lost the race, or the row moved out of 'completed' between the lookup
            // and the claim (already refunded/refund_pending/disputed) — the second
            // half of the idempotency story alongside the lookup's filter above.
            LogRefundAlreadyClaimed(logger, generationId, transaction.Id);
            return RefundOutcome.NotEligible;
        }

        try
        {
            StripeConfiguration.ApiKey = secretKey;
            var refund = await stripeRefunds.CreateAsync(new RefundCreateOptions
            {
                PaymentIntent = transaction.PaymentIntentId,
                Metadata = new Dictionary<string, string>
                {
                    ["generation_id"] = generationId,
                    ["reason"] = "compile_guarantee",
                },
            }, cancellationToken: ct);

            LogRefundIssued(logger, generationId, transaction.PaymentIntentId, refund.Id);
            return RefundOutcome.Issued;
        }
        catch (Exception ex)
        {
            LogStripeRefundFailed(logger, ex, generationId, transaction.PaymentIntentId);
            // Don't strand the row in refund_pending forever with no Stripe refund
            // behind it — revert so a future manual retry (or re-run) can still
            // find it eligible. Not on ct: the Stripe call may have failed because
            // the host is stopping, and an already-cancelled token would skip the revert.
            await billing.RevertRefundClaimAsync(transaction.Id, CancellationToken.None);
            return RefundOutcome.Failed;
        }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 800, Level = LogLevel.Debug, Message = "Stripe not configured — skipping compile-guarantee refund for generation {Id}")]
    private static partial void LogStripeNotConfigured(ILogger logger, string id);

    [LoggerMessage(EventId = 801, Level = LogLevel.Debug, Message = "Billing store not configured — skipping compile-guarantee refund for generation {Id}")]
    private static partial void LogStoreNotConfigured(ILogger logger, string id);

    [LoggerMessage(EventId = 802, Level = LogLevel.Information, Message = "No eligible completed transaction for generation {Id} — skipping refund (free tier, unpaid, or already settled)")]
    private static partial void LogNoEligibleTransaction(ILogger logger, string id);

    [LoggerMessage(EventId = 803, Level = LogLevel.Information, Message = "Transaction {TransactionId} for generation {Id} was not claimable (already refunded/refund_pending/disputed) — skipping refund")]
    private static partial void LogRefundAlreadyClaimed(ILogger logger, string id, string transactionId);

    [LoggerMessage(EventId = 804, Level = LogLevel.Information, Message = "Compile-guarantee refund issued for generation {Id} (payment_intent={PaymentIntentId}, refund={RefundId})")]
    private static partial void LogRefundIssued(ILogger logger, string id, string paymentIntentId, string refundId);

    [LoggerMessage(EventId = 805, Level = LogLevel.Error, Message = "Stripe refund failed for generation {Id} (payment_intent={PaymentIntentId}) — reverting transaction to completed")]
    private static partial void LogStripeRefundFailed(ILogger logger, Exception ex, string id, string paymentIntentId);

    [LoggerMessage(EventId = 806, Level = LogLevel.Error, Message = "Failed to look up/claim the transaction for generation {Id}")]
    private static partial void LogRefundLookupFailed(ILogger logger, Exception ex, string id);
}
