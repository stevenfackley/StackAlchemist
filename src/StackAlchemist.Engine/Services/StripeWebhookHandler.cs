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

public sealed record StripeWebhookResult(bool Processed, string? Reason = null);

/// <summary>
/// Dispatches Stripe webhook events to the right side-effect handler.
/// Idempotency is enforced via the stripe_events Supabase table.
/// </summary>
public sealed class StripeWebhookHandler(
    IGenerationOrchestrator orchestrator,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    IEmailService emailService,
    ILogger<StripeWebhookHandler> logger) : IStripeWebhookHandler
{
    public const string HttpClientName = "SupabaseAdmin";

    public async Task<StripeWebhookResult> HandleAsync(Event stripeEvent, CancellationToken ct)
    {
        var supabase = ResolveSupabase();
        if (supabase is { } sb && !await TryRecordEventAsync(sb, stripeEvent, ct))
        {
            return new StripeWebhookResult(false, "duplicate");
        }

        return stripeEvent.Type switch
        {
            "checkout.session.completed"           => await HandleCheckoutCompletedAsync(stripeEvent, ct),
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
            var recovered = await LoadGenerationPayloadAsync(sb, generationId, ct);
            mode = recovered.Mode ?? mode;
            prompt = string.IsNullOrWhiteSpace(prompt) ? recovered.Prompt : prompt;
            projectType = recovered.ProjectType ?? projectType;
            schema = recovered.Schema;
            personalization = recovered.Personalization;

            await UpsertTransactionAsync(sb, new TransactionUpsert
            {
                StripeSessionId  = session.Id,
                StripePaymentIntent = session.PaymentIntentId,
                Tier             = tier,
                Amount           = session.AmountTotal ?? 0,
                Status           = "completed",
                GenerationId     = generationId,
                LastEventId      = stripeEvent.Id,
            }, ct);
        }

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

        logger.LogWarning("Stripe checkout async payment failed for session {SessionId}", session.Id);
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

        logger.LogWarning("Stripe payment intent {Id} failed: {Reason}",
            intent.Id, intent.LastPaymentError?.Message);
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

        logger.LogInformation("Stripe charge {ChargeId} refunded (intent={IntentId})",
            charge.Id, paymentIntentId);
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

        logger.LogError("Stripe dispute opened on charge {ChargeId}: reason={Reason}",
            dispute.ChargeId, dispute.Reason);
        return new StripeWebhookResult(true);
    }

    // ── Idempotency: stripe_events table ───────────────────────────────────────
    private async Task<bool> TryRecordEventAsync(SupabaseAdmin sb, Event stripeEvent, CancellationToken ct)
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
            if (res.StatusCode == System.Net.HttpStatusCode.Conflict ||
                (int)res.StatusCode == 409)
            {
                return false; // already processed
            }
            res.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            // Fail-open: if the idempotency log is unavailable, still process —
            // logging double-charges is preferable to dropping legitimate events.
            logger.LogWarning(ex, "Failed to record Stripe event {Id} for idempotency", stripeEvent.Id);
            return true;
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

    private sealed class TransactionUpsert
    {
        public required string StripeSessionId { get; init; }
        public string? StripePaymentIntent { get; init; }
        public required int Tier { get; init; }
        public required long Amount { get; init; }
        public required string Status { get; init; }
        public string? GenerationId { get; init; }
        public required string LastEventId { get; init; }
    }

    private async Task UpsertTransactionAsync(SupabaseAdmin sb, TransactionUpsert tx, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{sb.Url}/rest/v1/transactions?on_conflict=stripe_session_id";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    stripe_session_id     = tx.StripeSessionId,
                    stripe_payment_intent = tx.StripePaymentIntent,
                    tier                  = tx.Tier,
                    amount                = tx.Amount,
                    status                = tx.Status,
                    generation_id         = tx.GenerationId,
                    last_stripe_event_id  = tx.LastEventId,
                    updated_at            = DateTimeOffset.UtcNow,
                }),
            };
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upsert transaction for session {SessionId}", tx.StripeSessionId);
        }
    }

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
            logger.LogError(ex, "Failed to update transaction (filter={Filter}, status={Status})", filter, status);
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
            logger.LogError(ex, "Failed to cancel undelivered generation {Id}", generationId);
        }
    }

    private async Task<(string? Mode, string? Prompt, ProjectType? ProjectType, GenerationSchema? Schema, GenerationPersonalization? Personalization)>
        LoadGenerationPayloadAsync(SupabaseAdmin sb, string generationId, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{sb.Url}/rest/v1/generations?id=eq.{generationId}&select=mode,prompt,schema_json,project_type,personalization_json";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.Add("apikey", sb.ServiceRoleKey);
            req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return (null, null, null, null, null);

            var row = doc.RootElement[0];
            string? mode = row.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            string? prompt = row.TryGetProperty("prompt", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            ProjectType? projectType = row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String &&
                Enum.TryParse<ProjectType>(pt.GetString(), ignoreCase: true, out var parsed) ? parsed : null;

            GenerationSchema? schema = null;
            if (row.TryGetProperty("schema_json", out var s) &&
                s.ValueKind != JsonValueKind.Null && s.ValueKind != JsonValueKind.Undefined)
            {
                schema = JsonSerializer.Deserialize<GenerationSchema>(s.GetRawText());
            }

            GenerationPersonalization? personalization = null;
            if (row.TryGetProperty("personalization_json", out var pj) &&
                pj.ValueKind != JsonValueKind.Null && pj.ValueKind != JsonValueKind.Undefined)
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                personalization = JsonSerializer.Deserialize<GenerationPersonalization>(pj.GetRawText(), opts);
            }

            return (mode, prompt, projectType, schema, personalization);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load generation payload for {Id}", generationId);
            return (null, null, null, null, null);
        }
    }
}
