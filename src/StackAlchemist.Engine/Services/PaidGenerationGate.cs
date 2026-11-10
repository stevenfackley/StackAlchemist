namespace StackAlchemist.Engine.Services;

/// <summary>Whether <c>/api/generate</c> may start a generation.</summary>
public enum PaymentCheck
{
    /// <summary>Free tier, or a paid tier backed by a completed transaction.</summary>
    Allowed,

    /// <summary>A paid tier with no completed transaction: never paid, refunded, or disputed.</summary>
    Unpaid,

    /// <summary>The payment could not be looked up. Nothing is known, so nothing is started.</summary>
    Unavailable,
}

public interface IPaidGenerationGate
{
    Task<PaymentCheck> CheckAsync(string generationId, int requestedTier, CancellationToken ct);
}

/// <summary>
/// The payment check in front of <c>/api/generate</c>. That endpoint takes its tier from the web
/// app, which took it from the browser, so a paid tier there proves nothing: only a completed
/// transaction does. The Stripe webhook does not come through here (it records the payment and
/// enqueues in one step), and the reconciler has its own copy of this rule for stale rows (#421).
///
/// The tier checked is the higher of the request's and the row's, the same one
/// <see cref="GenerationOrchestrator.EnqueueAsync"/> builds. No billing store (DATABASE_URL unset,
/// which Production refuses to start with) allows everything, as the webhook does.
/// </summary>
public sealed class PaidGenerationGate(
    IDeliveryService deliveryService,
    IBillingStore? billing,
    ILogger<PaidGenerationGate> logger) : IPaidGenerationGate
{
    /// <summary>Written to the row's <c>error_message</c>, so the owner reads it on the result page.</summary>
    public const string UnpaidMessage =
        "This build has no completed payment, so it was not started. "
        + "If the payment was refunded, start a new build.";

    public async Task<PaymentCheck> CheckAsync(string generationId, int requestedTier, CancellationToken ct)
    {
        var tier = requestedTier;
        if (tier <= 0)
        {
            // Only a tier-0 request needs the row: a paid request is checked whatever the row says.
            var snapshot = await deliveryService.GetGenerationSnapshotAsync(generationId, ct);
            tier = Math.Max(tier, snapshot?.Tier ?? 0);
        }

        if (tier <= 0 || billing is null)
            return PaymentCheck.Allowed;

        try
        {
            return await billing.FindCompletedTransactionAsync(generationId, ct) is not null
                ? PaymentCheck.Allowed
                : PaymentCheck.Unpaid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.PaymentCheckUnavailable(ex, generationId);
            return PaymentCheck.Unavailable;
        }
    }
}
