using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Sends transactional email via the Resend HTTP API. Failures are logged and
/// swallowed so a Resend outage never breaks the generation or webhook pipeline.
/// </summary>
public sealed class ResendEmailService(
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

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("Resend not configured — skipping email to {To} ({Subject})", to, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogDebug("Skipping email send: no recipient ({Subject})", subject);
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
                logger.LogWarning(
                    "Resend returned {Code} for email to {To}: {Body}",
                    (int)res.StatusCode, to, body);
                return;
            }

            logger.LogInformation("Sent transactional email to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send transactional email to {To}", to);
        }
    }
}

/// <summary>
/// Logger-only fallback used when Resend is unconfigured. Keeps the email send
/// callsites non-conditional.
/// </summary>
public sealed class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        logger.LogInformation("[NoOp email] would send to {To}: {Subject}", to, subject);
        return Task.CompletedTask;
    }
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
