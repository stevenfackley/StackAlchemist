using StackAlchemist.Engine.Models;
using Stripe;
using Stripe.Checkout;

namespace StackAlchemist.Engine.Services;

public interface IStripeWebhookHandler
{
    Task<StripeWebhookResult> HandleAsync(Event stripeEvent, CancellationToken ct);
}

/// <summary>
/// Outcome of a webhook delivery. <see cref="Retry"/> tells the endpoint to return a
/// 5xx so Stripe redelivers the event — set when a side effect failed in a way a
/// redelivery can fix (idempotency log unavailable, RPC failure, enqueue failure).
/// </summary>
public sealed record StripeWebhookResult(bool Processed, string? Reason = null, bool Retry = false);

/// <summary>
/// Dispatches Stripe webhook events to the right side-effect handler.
/// checkout.session.completed runs through the process_checkout_completed RPC, which
/// makes the idempotency-event insert, tier update, and transaction upsert one atomic
/// Postgres transaction; the other event types use the stripe_events table directly.
/// All of that data access goes through <see cref="IBillingStore"/>; a null store (none
/// configured) means no idempotency log, and every event proceeds without one.
/// </summary>
public sealed partial class StripeWebhookHandler(
    IGenerationOrchestrator orchestrator,
    IBillingStore? billing,
    IEmailService emailService,
    ILogger<StripeWebhookHandler> logger) : IStripeWebhookHandler
{
    public async Task<StripeWebhookResult> HandleAsync(Event stripeEvent, CancellationToken ct)
    {
        // checkout.session.completed owns its idempotency inside the RPC — recording the
        // event here, before the side effects, is exactly the bug this design replaces
        // (event marked processed, downstream failure, Stripe retry short-circuits).
        if (stripeEvent.Type == "checkout.session.completed")
            return await HandleCheckoutCompletedAsync(stripeEvent, ct);

        if (billing is not null)
        {
            switch (await billing.TryRecordEventAsync(stripeEvent.Id, stripeEvent.Type, ct))
            {
                case EventRecord.Duplicate:
                    return new StripeWebhookResult(false, "duplicate");
                case EventRecord.Unavailable:
                    // The remaining handlers' writes are status overwrites, safe under
                    // redelivery — ask Stripe to retry rather than process unlogged.
                    return new StripeWebhookResult(false, "idempotency_unavailable", Retry: true);
            }
        }

        return stripeEvent.Type switch
        {
            "checkout.session.async_payment_failed"=> await HandleCheckoutFailedAsync(stripeEvent, ct),
            "payment_intent.payment_failed"        => await HandlePaymentIntentFailedAsync(stripeEvent, ct),
            "charge.refunded"                      => await HandleChargeRefundedAsync(stripeEvent, ct),
            "charge.dispute.created"               => await HandleDisputeOpenedAsync(stripeEvent, ct),
            _                                       => new StripeWebhookResult(false, $"unhandled:{stripeEvent.Type}"),
        };
    }

    // ── checkout.session.completed ─────────────────────────────────────────────
    private async Task<StripeWebhookResult> HandleCheckoutCompletedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Session session)
            return new StripeWebhookResult(false, "not_a_session");

        var meta = session.Metadata ?? new Dictionary<string, string>();
        var tier = int.TryParse(meta.GetValueOrDefault("tier"), out var t) ? t : 2;
        var generationId = meta.GetValueOrDefault("generationId") ?? Guid.NewGuid().ToString();
        var prompt = meta.GetValueOrDefault("prompt");
        var projectType = Enum.TryParse<ProjectType>(meta.GetValueOrDefault("projectType"), ignoreCase: true, out var pt)
            ? pt
            : ProjectType.DotNetNextJs;
        var mode = prompt is { Length: > 0 } ? "simple" : "advanced";

        GenerationSchema? schema = null;
        GenerationPersonalization? personalization = null;

        if (billing is not null)
        {
            // One atomic Postgres transaction: idempotency-event insert + tier update
            // (payment is the authoritative moment a try-before-buy row leaves tier 0)
            // + transaction upsert. All-or-nothing, so a failure here leaves the event
            // unrecorded and Stripe's redelivery re-processes cleanly.
            CheckoutOutcome outcome;
            try
            {
                outcome = await billing.ProcessCheckoutCompletedAsync(
                    stripeEvent.Id, stripeEvent.Type, session.Id, session.PaymentIntentId,
                    generationId, tier, session.AmountTotal ?? 0, ct);
            }
            catch (Exception ex)
            {
                LogCheckoutRpcFailed(logger, ex, stripeEvent.Id, generationId);
                return new StripeWebhookResult(false, "rpc_failed", Retry: true);
            }

            if (!outcome.IsNew)
                return new StripeWebhookResult(false, "duplicate");

            mode = outcome.Mode ?? mode;
            prompt = string.IsNullOrWhiteSpace(prompt) ? outcome.Prompt : prompt;
            projectType = outcome.ProjectType ?? projectType;
            schema = outcome.Schema;
            personalization = outcome.Personalization;

            try
            {
                await orchestrator.EnqueueAsync(new GenerateRequest
                {
                    GenerationId    = generationId,
                    Mode            = mode,
                    Tier            = tier,
                    ProjectType     = projectType,
                    Prompt          = prompt,
                    Schema          = schema,
                    Personalization = personalization,
                }, ct);
            }
            catch (Exception ex)
            {
                // Compensate: un-record the event so Stripe's redelivery gets a fresh
                // is_new=true from the RPC instead of short-circuiting as a duplicate.
                LogCheckoutEnqueueFailed(logger, ex, stripeEvent.Id, generationId);
                await billing.DeleteEventAsync(stripeEvent.Id, ct);
                return new StripeWebhookResult(false, "enqueue_failed", Retry: true);
            }
        }
        else
        {
            // No billing store (local dev): no idempotency log, enqueue directly.
            await orchestrator.EnqueueAsync(new GenerateRequest
            {
                GenerationId    = generationId,
                Mode            = mode,
                Tier            = tier,
                ProjectType     = projectType,
                Prompt          = prompt,
                Schema          = schema,
                Personalization = personalization,
            }, ct);
        }

        var customerEmail = session.CustomerDetails?.Email ?? session.CustomerEmail;
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            var (subject, html) = EmailTemplates.Receipt(tier, session.AmountTotal ?? 0);
            await emailService.SendAsync(customerEmail, subject, html, ct);
        }

        return new StripeWebhookResult(true);
    }

    // ── checkout.session.async_payment_failed ──────────────────────────────────
    private async Task<StripeWebhookResult> HandleCheckoutFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Session session)
            return new StripeWebhookResult(false, "not_a_session");

        if (billing is not null)
        {
            await billing.UpdateTransactionsAsync(
                TransactionKey.StripeSessionId, session.Id,
                status: "failed",
                eventId: stripeEvent.Id,
                returnGenerationId: false,
                ct);
        }

        LogStripeAsyncPaymentFailed(logger, session.Id);
        return new StripeWebhookResult(true);
    }

    // ── payment_intent.payment_failed ──────────────────────────────────────────
    private async Task<StripeWebhookResult> HandlePaymentIntentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not PaymentIntent intent)
            return new StripeWebhookResult(false, "not_a_payment_intent");

        if (billing is not null)
        {
            await billing.UpdateTransactionsAsync(
                TransactionKey.StripePaymentIntent, intent.Id,
                status: "failed",
                eventId: stripeEvent.Id,
                returnGenerationId: false,
                ct);
        }

        LogStripePaymentIntentFailed(logger, intent.Id, intent.LastPaymentError?.Message);
        return new StripeWebhookResult(true);
    }

    // ── charge.refunded ────────────────────────────────────────────────────────
    private async Task<StripeWebhookResult> HandleChargeRefundedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Charge charge)
            return new StripeWebhookResult(false, "not_a_charge");

        var paymentIntentId = charge.PaymentIntentId;
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return new StripeWebhookResult(false, "missing_payment_intent");

        if (billing is not null)
        {
            // Mark the transaction refunded and look up the linked generation_id
            // so we can cancel the generation if it hasn't yet been delivered.
            var generationId = await billing.UpdateTransactionsAsync(
                TransactionKey.StripePaymentIntent, paymentIntentId,
                status: "refunded",
                eventId: stripeEvent.Id,
                returnGenerationId: true,
                ct);

            if (!string.IsNullOrWhiteSpace(generationId))
            {
                await billing.CancelUndeliveredGenerationAsync(generationId, "Refunded by Stripe", ct);
            }
        }

        LogStripeChargeRefunded(logger, charge.Id, paymentIntentId);
        return new StripeWebhookResult(true);
    }

    // ── charge.dispute.created ─────────────────────────────────────────────────
    private async Task<StripeWebhookResult> HandleDisputeOpenedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Dispute dispute)
            return new StripeWebhookResult(false, "not_a_dispute");

        if (billing is not null)
        {
            await billing.UpdateTransactionsAsync(
                TransactionKey.StripeChargeId, dispute.ChargeId,
                status: "disputed",
                eventId: stripeEvent.Id,
                returnGenerationId: false,
                ct);
        }

        LogStripeDisputeOpened(logger, dispute.ChargeId, dispute.Reason);
        return new StripeWebhookResult(true);
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "Stripe checkout async payment failed for session {SessionId}")]
    private static partial void LogStripeAsyncPaymentFailed(ILogger logger, string sessionId);

    [LoggerMessage(EventId = 301, Level = LogLevel.Warning, Message = "Stripe payment intent {Id} failed: {Reason}")]
    private static partial void LogStripePaymentIntentFailed(ILogger logger, string id, string? reason);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "Stripe charge {ChargeId} refunded (intent={IntentId})")]
    private static partial void LogStripeChargeRefunded(ILogger logger, string chargeId, string? intentId);

    [LoggerMessage(EventId = 303, Level = LogLevel.Error, Message = "Stripe dispute opened on charge {ChargeId}: reason={Reason}")]
    private static partial void LogStripeDisputeOpened(ILogger logger, string chargeId, string? reason);

    [LoggerMessage(EventId = 310, Level = LogLevel.Error, Message = "process_checkout_completed RPC failed for event {EventId} (generation {GenerationId}) — returning retry to Stripe")]
    private static partial void LogCheckoutRpcFailed(ILogger logger, Exception ex, string eventId, string generationId);

    [LoggerMessage(EventId = 311, Level = LogLevel.Error, Message = "Enqueue failed after checkout for event {EventId} (generation {GenerationId}) — compensating stripe_events delete + retry")]
    private static partial void LogCheckoutEnqueueFailed(ILogger logger, Exception ex, string eventId, string generationId);
}
