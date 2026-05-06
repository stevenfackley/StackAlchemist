using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Sends transactional email via the Resend HTTP API. Failures are logged and
/// swallowed so a Resend outage never breaks the generation or webhook pipeline.
/// </summary>
public sealed partial class ResendEmailService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ResendEmailService> logger) : IEmailService
{
    public const string HttpClientName = "Resend";
    private const string ApiBase = "https://api.resend.com/emails";

    public async Task SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        var apiKey = config["Resend:ApiKey"];
        var from = config["Resend:FromEmail"] ?? "StackAlchemist <noreply@stackalchemist.app>";
        var maskedTo = MaskEmail(to);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogResendNotConfigured(logger, maskedTo, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            LogNoRecipient(logger, subject);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, ApiBase)
            {
                Content = JsonContent.Create(new
                {
                    from,
                    to = new[] { to },
                    subject,
                    html,
                }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                LogResendNonOk(logger, (int)res.StatusCode, maskedTo, body);
                return;
            }

            LogEmailSent(logger, maskedTo, subject);
        }
        catch (Exception ex)
        {
            LogEmailSendFailed(logger, ex, maskedTo);
        }
    }

    /// <summary>
    /// Masks an email for logging so structured logs/aggregators don't ingest raw PII
    /// (GDPR/CCPA). Keeps the first character of the local-part and the full domain
    /// for support diagnostics: <c>jane.doe@example.com</c> → <c>j***@example.com</c>.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0) return "***";
        return email[..1] + "***" + email[at..];
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 700, Level = LogLevel.Debug, Message = "Resend not configured — skipping email to {To} ({Subject})")]
    private static partial void LogResendNotConfigured(ILogger logger, string to, string subject);

    [LoggerMessage(EventId = 701, Level = LogLevel.Debug, Message = "Skipping email send: no recipient ({Subject})")]
    private static partial void LogNoRecipient(ILogger logger, string subject);

    [LoggerMessage(EventId = 702, Level = LogLevel.Warning, Message = "Resend returned {Code} for email to {To}: {Body}")]
    private static partial void LogResendNonOk(ILogger logger, int code, string to, string body);

    [LoggerMessage(EventId = 703, Level = LogLevel.Information, Message = "Sent transactional email to {To}: {Subject}")]
    private static partial void LogEmailSent(ILogger logger, string to, string subject);

    [LoggerMessage(EventId = 704, Level = LogLevel.Error, Message = "Failed to send transactional email to {To}")]
    private static partial void LogEmailSendFailed(ILogger logger, Exception ex, string to);
}

/// <summary>
/// Logger-only fallback used when Resend is unconfigured. Keeps the email send
/// callsites non-conditional.
/// </summary>
public sealed partial class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        LogNoOpEmail(logger, ResendEmailService.MaskEmail(to), subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 750, Level = LogLevel.Information, Message = "[NoOp email] would send to {To}: {Subject}")]
    private static partial void LogNoOpEmail(ILogger logger, string to, string subject);
}

/// <summary>
/// Static template builders. Keep marketing-tone copy out of services and small
/// enough to inline; richer templates can graduate to a Razor or MJML pipeline later.
/// </summary>
public static class EmailTemplates
{
    public static (string Subject, string Html) Receipt(int tier, long amountCents) =>
        ($"Your StackAlchemist receipt — tier {tier}",
         $"<p>Thanks for your purchase. We charged your card <strong>${amountCents / 100m:0.00}</strong> for tier {tier}.</p>" +
         "<p>Your generation is queued — you'll get another email the moment your build is ready.</p>" +
         "<p>— The StackAlchemist team</p>");

    public static (string Subject, string Html) GenerationComplete(string downloadUrl) =>
        ("Your generated app is ready",
         "<p>Your StackAlchemist build passed compile validation and is ready to download.</p>" +
         $"<p><a href=\"{downloadUrl}\">Download your project</a> (link is short-lived — save it locally).</p>" +
         "<p>If anything looks off, reply to this email — we read every one.</p>");
}
