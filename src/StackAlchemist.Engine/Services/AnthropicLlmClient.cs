using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Production LLM client that calls the Anthropic Messages API (Claude Sonnet 4.6 by default; configurable via <c>Anthropic:Model</c>).
/// Registered when <c>Anthropic:ApiKey</c> is set; falls back to <see cref="MockLlmClient"/> otherwise.
/// </summary>
public sealed partial class AnthropicLlmClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<AnthropicLlmClient> logger) : ILlmClient
{
    public const string HttpClientName = "Anthropic";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<LlmResponse> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var apiKey = config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");
        var model = config["Anthropic:Model"] ?? AnthropicDefaults.ModelId;
        var maxTokens = int.TryParse(config["Anthropic:MaxTokens"], out var mt) ? mt : 8_192;
        var client = httpClientFactory.CreateClient(HttpClientName);
        LogCallingApi(logger, model, maxTokens);
        for (var attempt = 0; ; attempt++)
        {
            using var httpReq = BuildRequest(apiKey, model, maxTokens, systemPrompt, userPrompt);
            using var response = await client.SendAsync(httpReq, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct)
                    ?? throw new InvalidOperationException("Anthropic API returned a null response body.");

                var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text
                    ?? throw new InvalidOperationException(
                        "Anthropic API response contained no text content block.");

                LogApiResponse(logger, result.StopReason, result.Usage?.InputTokens ?? 0, result.Usage?.OutputTokens ?? 0);

                if (result.StopReason == "max_tokens")
                    LogResponseTruncated(logger, model, maxTokens);

                return new LlmResponse(
                    text,
                    result.Usage?.InputTokens ?? 0,
                    result.Usage?.OutputTokens ?? 0,
                    model,
                    result.StopReason);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var retryable = IsRetryable(response.StatusCode);

            // Rate-limit/overload (429/529) recovers on Anthropic's side within seconds to
            // minutes — give it a deeper retry budget than hard 5xx failures, and surface
            // exhaustion as a typed exception so the user sees "high demand" not a stack trace.
            var isRateLimit = IsRateLimit(response.StatusCode);
            var maxAttemptIndex = isRateLimit ? 4 : 2; // 5 total tries vs 3

            if (!retryable || attempt >= maxAttemptIndex)
            {
                LogApiError(logger, (int)response.StatusCode, body);
                if (isRateLimit)
                {
                    throw new LlmRateLimitException(
                        $"Anthropic API rate-limited/overloaded (HTTP {(int)response.StatusCode}) after {attempt + 1} attempts.");
                }
                response.EnsureSuccessStatusCode();
            }

            var delay = GetRetryDelay(response, attempt);
            LogApiRetry(logger, (int)response.StatusCode, attempt + 1, delay.TotalMilliseconds);

            await Task.Delay(delay, ct);
        }
    }

    private static HttpRequestMessage BuildRequest(
        string apiKey,
        string model,
        int maxTokens,
        string systemPrompt,
        string userPrompt)
    {
        var requestBody = new AnthropicRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Messages =
            [
                new AnthropicMessage { Role = "user", Content = userPrompt },
            ],
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(requestBody, options: JsonOpts),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        (int)statusCode is 429 or 500 or 502 or 503 or 529;

    private static bool IsRateLimit(HttpStatusCode statusCode) =>
        (int)statusCode is 429 or 529;

    // Cap covers Retry-After headers too: a server asking for a 10-minute pause would
    // otherwise stall the whole generation pipeline on one in-flight call.
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

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

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("max_tokens")]
        public required int MaxTokens { get; init; }

        [JsonPropertyName("system")]
        public required string System { get; init; }

        [JsonPropertyName("messages")]
        public required List<AnthropicMessage> Messages { get; init; }
    }

    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock> Content { get; init; } = [];

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; init; }
    }

    private sealed class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 800, Level = LogLevel.Information, Message = "Calling Anthropic API (model={Model}, maxTokens={MaxTokens})")]
    private static partial void LogCallingApi(ILogger logger, string model, int maxTokens);

    [LoggerMessage(EventId = 801, Level = LogLevel.Information, Message = "Anthropic API response: stopReason={Stop}, inputTokens={In}, outputTokens={Out}")]
    private static partial void LogApiResponse(ILogger logger, string? stop, int @in, int @out);

    [LoggerMessage(EventId = 802, Level = LogLevel.Error, Message = "Anthropic API error {Code}: {Body}")]
    private static partial void LogApiError(ILogger logger, int code, string body);

    [LoggerMessage(EventId = 803, Level = LogLevel.Warning, Message = "Anthropic API error {Code} on attempt {Attempt}; retrying in {DelayMs} ms")]
    private static partial void LogApiRetry(ILogger logger, int code, int attempt, double delayMs);

    [LoggerMessage(EventId = 804, Level = LogLevel.Warning, Message = "Anthropic response TRUNCATED at max_tokens (model={Model}, maxTokens={MaxTokens}) — output is incomplete")]
    private static partial void LogResponseTruncated(ILogger logger, string model, int maxTokens);
}
