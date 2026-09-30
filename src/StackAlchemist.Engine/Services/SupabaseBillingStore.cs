using System.Net.Http.Json;
using System.Text.Json;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// <see cref="IBillingStore"/> over Supabase PostgREST with the service-role key. The request and
/// response handling is moved verbatim from <see cref="StripeWebhookHandler"/> and
/// <see cref="StripeRefundService"/>, which used to own it inline. Registered when DATABASE_URL is
/// absent and the Supabase URL and service-role key are both configured.
/// <para>
/// Unconfigured (no URL or key) it behaves as the callers did without Supabase:
/// <see cref="TryRecordEventAsync"/> reports <see cref="EventRecord.New"/> (no idempotency log,
/// proceed), the writes are no-ops, the reads find nothing, and
/// <see cref="ProcessCheckoutCompletedAsync"/> throws.
/// </para>
/// </summary>
public sealed partial class SupabaseBillingStore(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SupabaseBillingStore> logger) : IBillingStore
{
    /// <summary>The named <see cref="HttpClient"/> for service-role PostgREST calls.</summary>
    public const string HttpClientName = "SupabaseAdmin";

    private sealed record SupabaseAdmin(string Url, string ServiceRoleKey);

    // ── Idempotency: stripe_events table (non-checkout event types) ─────────────

    /// <inheritdoc />
    public async Task<EventRecord> TryRecordEventAsync(string eventId, string eventType, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return EventRecord.New;

        try
        {
            var endpoint = $"{sb.Url}/rest/v1/stripe_events";
            var payload = new { id = eventId, type = eventType };
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
            LogStripeEventRecordFailed(logger, ex, eventId);
            return EventRecord.Unavailable;
        }
    }

    // ── checkout.session.completed: atomic RPC + compensation ──────────────────

    /// <inheritdoc />
    public async Task<CheckoutOutcome> ProcessCheckoutCompletedAsync(
        string eventId, string eventType, string sessionId, string? paymentIntentId,
        string generationId, int tier, long amount, CancellationToken ct)
    {
        var sb = ResolveSupabase() ?? throw new InvalidOperationException("Supabase is not configured");

        var endpoint = $"{sb.Url}/rest/v1/rpc/process_checkout_completed";
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                p_event_id       = eventId,
                p_event_type     = eventType,
                p_session_id     = sessionId,
                p_payment_intent = paymentIntentId,
                p_generation_id  = generationId,
                p_tier           = tier,
                p_amount         = amount,
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
            return new CheckoutOutcome(false, null, null, null, null, null);

        string? mode = row.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        string? prompt = row.TryGetProperty("prompt", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        ProjectType? projectType =
            row.TryGetProperty("project_type", out var pt) && pt.ValueKind == JsonValueKind.String &&
            Enum.TryParse<ProjectType>(pt.GetString(), ignoreCase: true, out var parsed)
                ? parsed
                : null;

        // DeliveryJson.Read for both columns: schema_json is lowercase when the Engine wrote it and
        // camelCase when the web did, and a case-sensitive read of either yields an empty model.
        GenerationSchema? schema = null;
        if (row.TryGetProperty("schema_json", out var s) &&
            s.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            schema = JsonSerializer.Deserialize<GenerationSchema>(s.GetRawText(), DeliveryJson.Read);
        }

        GenerationPersonalization? personalization = null;
        if (row.TryGetProperty("personalization_json", out var pj) &&
            pj.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            personalization = JsonSerializer.Deserialize<GenerationPersonalization>(pj.GetRawText(), DeliveryJson.Read);
        }

        return new CheckoutOutcome(true, mode, prompt, projectType, schema, personalization);
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(string eventId, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return;

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

    // ── Transactions / generations writes (webhook) ────────────────────────────

    /// <summary>
    /// Patches all transactions matching <paramref name="key"/> = <paramref name="value"/> to a new
    /// status. Returns the linked generation_id from the first matching row when
    /// <paramref name="returnGenerationId"/> is true; otherwise null.
    /// </summary>
    public async Task<string?> UpdateTransactionsAsync(
        TransactionKey key,
        string value,
        string status,
        string eventId,
        bool returnGenerationId,
        CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return null;

        var filter = key switch
        {
            TransactionKey.StripeSessionId => $"stripe_session_id=eq.{value}",
            TransactionKey.StripePaymentIntent => $"stripe_payment_intent=eq.{value}",
            TransactionKey.StripeChargeId => $"stripe_charge_id=eq.{value}",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown transaction key."),
        };

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

    /// <inheritdoc />
    public async Task CancelUndeliveredGenerationAsync(string generationId, string reason, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return;

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

    // ── Compile Guarantee refund ───────────────────────────────────────────────

    /// <summary>
    /// The only transactions eligible for a Compile Guarantee refund are rows
    /// still in 'completed' status linked to this generation — this single
    /// filter is what makes "already refunded / refund_pending / disputed / no
    /// transaction at all (free tier)" all resolve to "skip" without needing a
    /// separate check for each case.
    /// </summary>
    public async Task<EligibleTransaction?> FindCompletedTransactionAsync(string generationId, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return null;

        var endpoint =
            $"{sb.Url}/rest/v1/transactions?generation_id=eq.{generationId}&status=eq.completed" +
            "&select=id,stripe_payment_intent&order=created_at.desc&limit=1";

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
        req.Headers.Add("apikey", sb.ServiceRoleKey);
        req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        var row = doc.RootElement[0];
        var id = row.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString()
            : null;
        var paymentIntent = row.TryGetProperty("stripe_payment_intent", out var piEl) && piEl.ValueKind == JsonValueKind.String
            ? piEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(paymentIntent))
            return null;

        return new EligibleTransaction(id, paymentIntent);
    }

    /// <summary>
    /// Conditional PATCH (CAS): only flips status when it's still 'completed'.
    /// Returns false when zero rows matched — a concurrent/duplicate caller
    /// already claimed it, or it moved to another terminal status.
    /// </summary>
    public async Task<bool> TryClaimForRefundAsync(string transactionId, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return false;

        var endpoint = $"{sb.Url}/rest/v1/transactions?id=eq.{transactionId}&status=eq.completed";
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Patch, endpoint)
        {
            Content = JsonContent.Create(new
            {
                status = "refund_pending",
                updated_at = DateTimeOffset.UtcNow,
            }),
        };
        req.Headers.Add("apikey", sb.ServiceRoleKey);
        req.Headers.Add("Authorization", $"Bearer {sb.ServiceRoleKey}");
        req.Headers.Add("Prefer", "return=representation");

        using var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return false;

        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 1;
    }

    /// <inheritdoc />
    public async Task RevertRefundClaimAsync(string transactionId, CancellationToken ct)
    {
        if (ResolveSupabase() is not { } sb)
            return;

        try
        {
            var endpoint = $"{sb.Url}/rest/v1/transactions?id=eq.{transactionId}&status=eq.refund_pending";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    status = "completed",
                    updated_at = DateTimeOffset.UtcNow,
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
            LogRevertClaimFailed(logger, ex, transactionId);
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

    // ── LoggerMessage source-gen (EventIds kept from the webhook handler / refund service) ──

    [LoggerMessage(EventId = 304, Level = LogLevel.Warning, Message = "Failed to record Stripe event {Id} for idempotency")]
    private static partial void LogStripeEventRecordFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 306, Level = LogLevel.Error, Message = "Failed to update transaction (filter={Filter}, status={Status})")]
    private static partial void LogTxUpdateFailed(ILogger logger, Exception ex, string filter, string status);

    [LoggerMessage(EventId = 307, Level = LogLevel.Error, Message = "Failed to cancel undelivered generation {Id}")]
    private static partial void LogGenerationCancelFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 312, Level = LogLevel.Error, Message = "MANUAL RECOVERY: compensating delete of stripe_event {EventId} failed — redelivery will report duplicate; check the generation row")]
    private static partial void LogCompensationDeleteFailed(ILogger logger, Exception ex, string eventId);

    [LoggerMessage(EventId = 807, Level = LogLevel.Error, Message = "MANUAL RECOVERY: failed to revert transaction {TransactionId} back to completed after a failed Stripe refund call")]
    private static partial void LogRevertClaimFailed(ILogger logger, Exception ex, string transactionId);
}
