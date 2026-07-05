using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>An eligible transaction existed but the Stripe call or Supabase write failed.</summary>
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
/// Idempotent by construction: the eligibility lookup and the claim PATCH both
/// filter on <c>status=eq.completed</c>, so a transaction that's already
/// refunded, already refund_pending, disputed, or was never completed (free
/// tier, failed payment) is skipped without a Stripe call or an exception —
/// including a second call for the same generation after the first succeeded.
/// </summary>
public sealed partial class StripeRefundService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    RefundService stripeRefunds,
    ILogger<StripeRefundService> logger) : IRefundService
{
    public const string HttpClientName = "SupabaseAdmin";

    private sealed record SupabaseAdmin(string Url, string ServiceRoleKey);

    private sealed record EligibleTransaction(string Id, string PaymentIntentId);

    public async Task<RefundOutcome> RefundFailedGenerationAsync(string generationId, CancellationToken ct)
    {
        var secretKey = config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            LogStripeNotConfigured(logger, generationId);
            return RefundOutcome.NotEligible;
        }

        if (ResolveSupabase() is not { } sb)
        {
            LogSupabaseNotConfigured(logger, generationId);
            return RefundOutcome.NotEligible;
        }

        EligibleTransaction? transaction;
        try
        {
            transaction = await FindCompletedTransactionAsync(sb, generationId, ct);
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
            claimed = await TryClaimForRefundAsync(sb, transaction.Id, ct);
        }
        catch (Exception ex)
        {
            LogRefundLookupFailed(logger, ex, generationId);
            return RefundOutcome.Failed;
        }

        if (!claimed)
        {
            // Lost the race, or the row moved out of 'completed' between the GET
            // and the PATCH (already refunded/refund_pending/disputed) — the
            // second half of the idempotency story alongside the GET filter above.
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
            // find it eligible.
            await RevertClaimAsync(sb, transaction.Id, ct);
            return RefundOutcome.Failed;
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

    /// <summary>
    /// The only transactions eligible for a Compile Guarantee refund are rows
    /// still in 'completed' status linked to this generation — this single
    /// filter is what makes "already refunded / refund_pending / disputed / no
    /// transaction at all (free tier)" all resolve to "skip" without needing a
    /// separate check for each case.
    /// </summary>
    private async Task<EligibleTransaction?> FindCompletedTransactionAsync(
        SupabaseAdmin sb, string generationId, CancellationToken ct)
    {
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
    private async Task<bool> TryClaimForRefundAsync(SupabaseAdmin sb, string transactionId, CancellationToken ct)
    {
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

    private async Task RevertClaimAsync(SupabaseAdmin sb, string transactionId, CancellationToken ct)
    {
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

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 800, Level = LogLevel.Debug, Message = "Stripe not configured — skipping compile-guarantee refund for generation {Id}")]
    private static partial void LogStripeNotConfigured(ILogger logger, string id);

    [LoggerMessage(EventId = 801, Level = LogLevel.Debug, Message = "Supabase not configured — skipping compile-guarantee refund for generation {Id}")]
    private static partial void LogSupabaseNotConfigured(ILogger logger, string id);

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

    [LoggerMessage(EventId = 807, Level = LogLevel.Error, Message = "MANUAL RECOVERY: failed to revert transaction {TransactionId} back to completed after a failed Stripe refund call")]
    private static partial void LogRevertClaimFailed(ILogger logger, Exception ex, string transactionId);
}
