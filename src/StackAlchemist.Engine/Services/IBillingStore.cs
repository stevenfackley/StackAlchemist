using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>Outcome of recording a Stripe event in the idempotency log.</summary>
public enum EventRecord
{
    /// <summary>First delivery: the event row was inserted, so its side effects should run.</summary>
    New,

    /// <summary>The event was already recorded: a redelivery, to be skipped.</summary>
    Duplicate,

    /// <summary>The log could not be reached; the caller asks Stripe to retry rather than process unlogged.</summary>
    Unavailable,
}

/// <summary>The transactions column a Stripe event identifies its rows by.</summary>
public enum TransactionKey
{
    /// <summary><c>stripe_session_id</c> (checkout.session.* events).</summary>
    StripeSessionId,

    /// <summary><c>stripe_payment_intent</c> (payment_intent.* and charge.refunded events).</summary>
    StripePaymentIntent,

    /// <summary><c>stripe_charge_id</c> (charge.dispute.* events).</summary>
    StripeChargeId,
}

/// <summary>A <c>completed</c> transaction a Compile Guarantee refund can be issued against.</summary>
/// <param name="Id">The transaction's id.</param>
/// <param name="PaymentIntentId">The Stripe payment intent the refund is created on.</param>
public sealed record EligibleTransaction(string Id, string PaymentIntentId);

/// <summary>
/// Result of <see cref="IBillingStore.ProcessCheckoutCompletedAsync"/>. When <see cref="IsNew"/> is
/// false the event was a redelivery and every other field is null; otherwise they carry the paid
/// generation's stored inputs (each null when the row does not exist or the column is empty).
/// </summary>
/// <param name="IsNew">True when this call recorded the event (first delivery).</param>
/// <param name="Mode">The generation's <c>mode</c>.</param>
/// <param name="Prompt">The generation's <c>prompt</c>.</param>
/// <param name="ProjectType">The generation's <c>project_type</c>, parsed.</param>
/// <param name="Schema">The generation's <c>schema_json</c>, deserialized.</param>
/// <param name="Personalization">The generation's <c>personalization_json</c>, deserialized.</param>
public sealed record CheckoutOutcome(
    bool IsNew, string? Mode, string? Prompt, ProjectType? ProjectType,
    GenerationSchema? Schema, GenerationPersonalization? Personalization);

/// <summary>
/// The billing tables (stripe_events, transactions, and the tier/cancel writes on
/// generations) behind the Stripe webhook and the compile-guarantee refund.
/// Unregistered when no store is configured (DATABASE_URL unset outside Production); callers
/// treat a null store as "no idempotency log, proceed".
/// </summary>
public interface IBillingStore
{
    /// <summary>Insert into stripe_events; Duplicate on conflict; Unavailable when the store cannot be reached (never throws).</summary>
    Task<EventRecord> TryRecordEventAsync(string eventId, string eventType, CancellationToken ct);

    /// <summary>Compensation for a failed enqueue after a successful checkout RPC. Swallows and logs failures.</summary>
    Task DeleteEventAsync(string eventId, CancellationToken ct);

    /// <summary>
    /// process_checkout_completed: one atomic call. Throws on transport failure so the caller can ask Stripe to retry.
    /// The atomicity includes reading the row back: the event, tier and transaction writes commit only once the
    /// outcome has been mapped, so a throw of any kind leaves nothing recorded.
    /// </summary>
    Task<CheckoutOutcome> ProcessCheckoutCompletedAsync(
        string eventId, string eventType, string sessionId, string? paymentIntentId,
        string generationId, int tier, long amount, CancellationToken ct);

    /// <summary>Status overwrite on every transaction matching key = value. Returns the first row's generation_id when asked. Swallows and logs failures.</summary>
    Task<string?> UpdateTransactionsAsync(TransactionKey key, string value, string status, string eventId, bool returnGenerationId, CancellationToken ct);

    /// <summary>generations: status='failed' unless already 'success'. Swallows and logs failures.</summary>
    Task CancelUndeliveredGenerationAsync(string generationId, string reason, CancellationToken ct);

    /// <summary>Newest 'completed' transaction for the generation, or null. Throws on transport failure.</summary>
    Task<EligibleTransaction?> FindCompletedTransactionAsync(string generationId, CancellationToken ct);

    /// <summary>CAS completed → refund_pending; false when zero rows matched. Throws on transport failure.</summary>
    Task<bool> TryClaimForRefundAsync(string transactionId, CancellationToken ct);

    /// <summary>refund_pending → completed after a failed Stripe call. Swallows and logs failures.</summary>
    Task RevertRefundClaimAsync(string transactionId, CancellationToken ct);
}
