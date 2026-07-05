using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Shared retry/backoff for all LLM HTTP providers (Anthropic, OpenAI, OpenRouter). Factored out
/// of <see cref="AnthropicLlmClient"/> so every provider gets identical behavior: retry 429/5xx with
/// exponential backoff + jitter (honoring Retry-After, capped at 60s), a deeper budget for
/// rate-limit/overload (5 tries vs 3 for hard 5xx), and a typed <see cref="LlmRateLimitException"/>
/// when a rate limit is never cleared. The provider's error body is logged for diagnostics but is
/// NEVER surfaced in the thrown exception, so no key/PII can reach a generation row.
/// </summary>
public static partial class LlmHttpRetry
{
    // Cap covers Retry-After headers too: a server asking for a 10-minute pause would otherwise
    // stall the whole generation pipeline on one in-flight call.
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Sends the request produced by <paramref name="requestFactory"/> (fresh per attempt —
    /// an HttpRequestMessage cannot be resent), retrying transient failures. Returns the first
    /// successful response; the caller owns its disposal. Throws <see cref="LlmRateLimitException"/>
    /// on exhausted 429/529, or <see cref="HttpRequestException"/> on other exhausted/non-retryable
    /// failures.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        ILogger logger,
        string provider,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return response;

            var body = await response.Content.ReadAsStringAsync(ct);
            var retryable = IsRetryable(response.StatusCode);

            // Rate-limit/overload (429/529) recovers on the provider's side within seconds to
            // minutes — give it a deeper retry budget than hard 5xx failures, and surface
            // exhaustion as a typed exception so the user sees "high demand" not a stack trace.
            var isRateLimit = IsRateLimit(response.StatusCode);
            var maxAttemptIndex = isRateLimit ? 4 : 2; // 5 total tries vs 3

            if (!retryable || attempt >= maxAttemptIndex)
            {
                // Provider 401/403 bodies can echo a fragment of the submitted key (e.g. OpenAI's
                // "Incorrect API key provided: sk-…"). Redact anything key-shaped before it reaches
                // the centralized logs.
                LogApiError(logger, provider, (int)response.StatusCode, ScrubKeys(body));
                if (isRateLimit)
                {
                    var code = (int)response.StatusCode;
                    response.Dispose();
                    throw new LlmRateLimitException(
                        $"{provider} API rate-limited/overloaded (HTTP {code}) after {attempt + 1} attempts.");
                }

                // EnsureSuccessStatusCode throws HttpRequestException whose message carries only the
                // status code/reason — never the body (no key/PII leak to the surfaced error).
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
                throw new UnreachableException();
            }

            var delay = GetRetryDelay(response, attempt);
            LogApiRetry(logger, provider, (int)response.StatusCode, attempt + 1, delay.TotalMilliseconds);
            response.Dispose();

            await Task.Delay(delay, ct);
        }
    }

    internal static bool IsRetryable(HttpStatusCode statusCode) =>
        (int)statusCode is 429 or 500 or 502 or 503 or 529;

    internal static bool IsRateLimit(HttpStatusCode statusCode) =>
        (int)statusCode is 429 or 529;

    internal static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var delay = ComputeRetryDelay(response, attempt);
        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }

    private static TimeSpan ComputeRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfterDelta)
            return retryAfterDelta;

        if (response.Headers.RetryAfter?.Date is { } retryAfterDate)
        {
            var delay = retryAfterDate - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                return delay;
        }

        if (response.Headers.TryGetValues("retry-after", out var values))
        {
            var rawValue = values.FirstOrDefault();
            if (int.TryParse(rawValue, out var seconds) && seconds >= 0)
                return TimeSpan.FromSeconds(seconds);
        }

        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var jitterMs = Random.Shared.Next(100, 750);
        return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
    }

    // Redacts key-shaped tokens ("sk-…", incl. sk-ant-/sk-or-/sk-proj- variants) from a string
    // before it is logged. Defense-in-depth for provider error bodies that partially echo the key.
    internal static string ScrubKeys(string body) =>
        string.IsNullOrEmpty(body) ? body : ApiKeyPattern().Replace(body, "sk-***redacted***");

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{4,}")]
    private static partial Regex ApiKeyPattern();

    [LoggerMessage(EventId = 820, Level = LogLevel.Error, Message = "{Provider} API error {Code}: {Body}")]
    private static partial void LogApiError(ILogger logger, string provider, int code, string body);

    [LoggerMessage(EventId = 821, Level = LogLevel.Warning, Message = "{Provider} API error {Code} on attempt {Attempt}; retrying in {DelayMs} ms")]
    private static partial void LogApiRetry(ILogger logger, string provider, int code, int attempt, double delayMs);
}
