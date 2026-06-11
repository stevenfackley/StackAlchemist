using System.Net.Http.Json;
using System.Text.Json;
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
/// </summary>
public sealed partial class StripeWebhookHandler(
    IGenerationOrchestrator orchestrator,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    IEmailService emailService,
    ILogger<StripeWebhookHandler> logger) : IStripeWebhookHandler
{
    public const string HttpClientName = "SupabaseAdmin";

    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    private enum EventRecord { New, Duplicate, Unavailable }

    public async Task<StripeWebhookResult> HandleAsync(Event stripeEvent, CancellationToken ct)
    {
        // checkout.session.completed owns its idempotency inside the RPC — recording the
        // event here, before the side effects, is exactly the bug this design replaces
        // (event marked processed, downstream failure, Stripe retry short-circuits).
        if (stripeEvent.Type == "checkout.session.completed")
            return await HandleCheckoutCompletedAsync(stripeEvent, ct);

        if (ResolveSupabase() is { } sb)
        {
            switch (await TryRecordEventAsync(sb, stripeEvent, ct))
            {
                case EventRecord.Duplicate:
                    return new StripeWebhookResult(false, "duplicate");
                case EventRecord.Unavailable:
                    // The remaining handlers' PATCHes are status overwrites, safe under
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

        if (ResolveSupabase() is { } sb)
        {
            // One atomic Postgres transaction: idempotency-event insert + tier update
            // (payment is the authoritative moment a try-before-buy row leaves tier 0)
            // + transaction upsert. All-or-nothing, so a failure here leaves the event
            // unrecorded and Stripe's redelivery re-processes cleanly.
            CheckoutRpcOutcome outcome;
            try
            {
                outcome = await ProcessCheckoutCompletedRpcAsync(sb, stripeEvent, session, generationId, tier, ct);
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
                await DeleteStripeEventAsync(sb, stripeEvent.Id, ct);
                return new StripeWebhookResult(false, "enqueue_failed", Retry: true);
            }
        }
        else
        {
            // Supabase unconfigured (local dev): no idempotency log, enqueue directly.
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

        if (ResolveSupabase() is { } sb)
        {
            await UpdateTransactionByFilterAsync(sb,
                filter: $"stripe_session_id=eq.{session.Id}",
                status: "failed",
                eventId: stripeEvent.Id,
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

        if (ResolveSupabase() is { } sb)
        {
            await UpdateTransactionByFilterAsync(sb,
                filter: $"stripe_payment_intent=eq.{intent.Id}",
                status: "failed",
                eventId: stripeEvent.Id,
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

        if (ResolveSupabase() is { } sb)
        {
            // Mark the transaction refunded and look up the linked generation_id
            // so we can cancel the generation if it hasn't yet been delivered.
            var generationId = await UpdateTransactionByFilterAsync(sb,
                filter: $"stripe_payment_intent=eq.{paymentIntentId}",
                status: "refunded",
                eventId: stripeEvent.Id,
                ct,
                returnGenerationId: true);

            if (!string.IsNullOrWhiteSpace(generationId))
            {
                await CancelUndeliveredGenerationAsync(sb, generationId!, "Refunded by Stripe", ct);
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

        if (ResolveSupabase() is { } sb)
        {
            await UpdateTransactionByFilterAsync(sb,
                filter: $"stripe_charge_id=eq.{dispute.ChargeId}",
                status: "disputed",
                eventId: stripeEvent.Id,
                ct);
        }

        LogStripeDisputeOpened(logger, dispute.ChargeId, dispute.Reason);
        return new StripeWebhookResult(true);
    }

    // ── Idempotency: stripe_events table (non-checkout event types) ─────────────
    private async Task<EventRecord> TryRecordEventAsync(SupabaseAdmin sb, Event stripeEvent, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{sb.Url}/rest/v1/stripe_events";
            var payload = new { id = stripeEvent.Id, type = stripeEvent.Type };
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(payload),
            };
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
            req.Headers.Add("Prefer", "return=minimal");

            using var res = await client.SendAsync(req, ct);
            if ((int)res.StatusCode == 409)
                return EventRecord.Duplicate; // already processed

            res.EnsureSuccessStatusCode();
            return EventRecord.New;
        }
        catch (Exception ex)
        {
            // Idempotency log unavailable: the caller returns Retry so Stripe redelivers
            // once the log is reachable, instead of processing unlogged (the old fail-open
            // allowed concurrent duplicate deliveries to double-process).
            LogStripeEventRecordFailed(logger, ex, stripeEvent.Id);
            return EventRecord.Unavailable;
        }
    }

    // ── checkout.session.completed: atomic RPC + compensation ──────────────────

    private sealed record CheckoutRpcOutcome(
        bool IsNew,
        string? Mode,
        string? Prompt,
        ProjectType? ProjectType,
        GenerationSchema? Schema,
        GenerationPersonalization? Personalization);

    private async Task<CheckoutRpcOutcome> ProcessCheckoutCompletedRpcAsync(
        SupabaseAdmin sb, Event stripeEvent, Session session, string generationId, int tier, CancellationToken ct)
    {
        var endpoint = $"{sb.Url}/rest/v1/rpc/process_checkout_completed";
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                p_event_id       = stripeEvent.Id,
                p_event_type     = stripeEvent.Type,
                p_session_id     = session.Id,
                p_payment_intent = session.PaymentIntentId,
                p_generation_id  = generationId,
                p_tier           = tier,
                p_amount         = session.AmountTotal ?? 0,
            }),
        };
        req.Headers.Add("apikey", sb.ServiceRoleKey);
        req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException("process_checkout_completed returned no rows.");

        var row = doc.RootElement[0];
        var isNew = row.TryGetProperty("is_new", out var n) && n.ValueKind == JsonValueKind.True;
        if (!isNew)
            return new CheckoutRpcOutcome(false, null, null, null, null, null);

        string? mode = row.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        string? prompt = row.TryGetProperty("prompt", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        ProjectType? projectType =
            row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String &&
            Enum.TryParse<ProjectType>(pt.GetString(), ignoreCase: true, out var parsed)
                ? parsed
                : null;

        GenerationSchema? schema = null;
        if (row.TryGetProperty("schema_json", out var s) &&
            s.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            schema = JsonSerializer.Deserialize<GenerationSchema>(s.GetRawText());
        }

        GenerationPersonalization? personalization = null;
        if (row.TryGetProperty("personalization_json", out var pj) &&
            pj.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            personalization = JsonSerializer.Deserialize<GenerationPersonalization>(pj.GetRawText(), CaseInsensitiveJson);
        }

        return new CheckoutRpcOutcome(true, mode, prompt, projectType, schema, personalization);
    }

    private async Task DeleteStripeEventAsync(SupabaseAdmin sb, string eventId, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{sb.Url}/rest/v1/stripe_events?id=eq.{eventId}";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
            req.Headers.Add("Prefer", "return=minimal");

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Manual-recovery breadcrumb: the event stays recorded, so the redelivery will
            // report "duplicate" — the generation row is the place to look (the orchestrator
            // usually marks it failed itself before EnqueueAsync ever throws).
            LogCompensationDeleteFailed(logger, ex, eventId);
        }
    }

    // ── Supabase helpers ───────────────────────────────────────────────────────
    private SupabaseAdmin? ResolveSupabase()
    {
        var url = config["Supabase:Url"];
        var key = config["Supabase:ServiceRoleKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) return null;
        return new SupabaseAdmin(url.TrimEnd('/'), key);
    }

    private sealed record SupabaseAdmin(string Url, string ServiceRoleKey);

    /// <summary>
    /// Patches all transactions matching the given PostgREST filter to a new status.
    /// Returns the linked generation_id from the first matching row when
    /// <paramref name="returnGenerationId"/> is true; otherwise null.
    /// </summary>
    private async Task<string?> UpdateTransactionByFilterAsync(
        SupabaseAdmin sb,
        string filter,
        string status,
        string eventId,
        CancellationToken ct,
        bool returnGenerationId = false)
    {
        try
        {
            var endpoint = $"{sb.Url}/rest/v1/transactions?{filter}";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    status               = status,
                    last_stripe_event_id = eventId,
                    updated_at           = DateTimeOffset.UtcNow,
                }),
            };
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
            req.Headers.Add("Prefer", returnGenerationId ? "return=representation" : "return=minimal");

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            if (!returnGenerationId) return null;

            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            return doc.RootElement[0].TryGetProperty("generation_id", out var g) &&
                   g.ValueKind == JsonValueKind.String
                ? g.GetString()
                : null;
        }
        catch (Exception ex)
        {
            LogTxUpdateFailed(logger, ex, filter, status);
            return null;
        }
    }

    private async Task CancelUndeliveredGenerationAsync(
        SupabaseAdmin sb, string generationId, string reason, CancellationToken ct)
    {
        try
        {
            // Only flip generations that haven't already been delivered to the user.
            var endpoint = $"{sb.Url}/rest/v1/generations?id=eq.{generationId}&status=neq.success";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    status        = "failed",
                    error_message = reason,
                }),
            };
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
            req.Headers.Add("Prefer", "return=minimal");

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            LogGenerationCancelFailed(logger, ex, generationId);
        }
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

    [LoggerMessage(EventId = 304, Level = LogLevel.Warning, Message = "Failed to record Stripe event {Id} for idempotency")]
    private static partial void LogStripeEventRecordFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 306, Level = LogLevel.Error, Message = "Failed to update transaction (filter={Filter}, status={Status})")]
    private static partial void LogTxUpdateFailed(ILogger logger, Exception ex, string filter, string status);

    [LoggerMessage(EventId = 307, Level = LogLevel.Error, Message = "Failed to cancel undelivered generation {Id}")]
    private static partial void LogGenerationCancelFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 310, Level = LogLevel.Error, Message = "process_checkout_completed RPC failed for event {EventId} (generation {GenerationId}) — returning retry to Stripe")]
    private static partial void LogCheckoutRpcFailed(ILogger logger, Exception ex, string eventId, string generationId);

    [LoggerMessage(EventId = 311, Level = LogLevel.Error, Message = "Enqueue failed after checkout for event {EventId} (generation {GenerationId}) — compensating stripe_events delete + retry")]
    private static partial void LogCheckoutEnqueueFailed(ILogger logger, Exception ex, string eventId, string generationId);

    [LoggerMessage(EventId = 312, Level = LogLevel.Error, Message = "MANUAL RECOVERY: compensating delete of stripe_event {EventId} failed — redelivery will report duplicate; check the generation row")]
    private static partial void LogCompensationDeleteFailed(ILogger logger, Exception ex, string eventId);
}
