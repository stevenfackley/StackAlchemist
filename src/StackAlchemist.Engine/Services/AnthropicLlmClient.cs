using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// LLM client that calls the Anthropic Messages API (Claude Sonnet 4.6 by default; configurable via
/// <c>Anthropic:Model</c>). Used by <see cref="RoutingLlmClient"/> for both the global default path
/// (no <see cref="LlmCallOptions"/>) and per-user Anthropic model/BYOK-key routing. Retry/backoff is
/// shared with the OpenAI-compatible client via <see cref="LlmHttpRetry"/>.
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
        LlmCallOptions? options = null,
        CancellationToken ct = default)
    {
        // BYOK key when the caller resolved one, else the global key. A per-user Anthropic model
        // routes here with options.ApiKey == null and falls back to the global key — identical to
        // the pre-BYOK path when options is null.
        var apiKey = options?.ApiKey
            ?? config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");
        var model = options?.Model ?? config["Anthropic:Model"] ?? AnthropicDefaults.ModelId;
        var maxTokens = int.TryParse(config["Anthropic:MaxTokens"], out var mt) ? mt : 8_192;
        var client = httpClientFactory.CreateClient(HttpClientName);

        LogCallingApi(logger, model, maxTokens);

        using var response = await LlmHttpRetry.SendWithRetryAsync(
            client,
            () => BuildRequest(apiKey, model, maxTokens, systemPrompt, userPrompt),
            logger,
            "Anthropic",
            ct);

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

    /// <summary>Kept for backward-compatible unit coverage; delegates to the shared retry policy.</summary>
    internal static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt) =>
        LlmHttpRetry.GetRetryDelay(response, attempt);

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

    [LoggerMessage(EventId = 804, Level = LogLevel.Warning, Message = "Anthropic response TRUNCATED at max_tokens (model={Model}, maxTokens={MaxTokens}) — output is incomplete")]
    private static partial void LogResponseTruncated(ILogger logger, string model, int maxTokens);
}
