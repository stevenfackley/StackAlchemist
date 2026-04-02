using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Production LLM client that calls the Anthropic Messages API (Claude 3.5 Sonnet).
/// Registered when <c>Anthropic:ApiKey</c> is set; falls back to <see cref="MockLlmClient"/> otherwise.
/// </summary>
public sealed class AnthropicLlmClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<AnthropicLlmClient> logger) : ILlmClient
{
    public const string HttpClientName = "Anthropic";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var apiKey = config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");
        var model = config["Anthropic:Model"] ?? "claude-3-5-sonnet-20241022";
        var maxTokens = int.TryParse(config["Anthropic:MaxTokens"], out var mt) ? mt : 8_192;

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

        var client = httpClientFactory.CreateClient(HttpClientName);

        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(requestBody, options: JsonOpts),
        };
        httpReq.Headers.Add("x-api-key", apiKey);
        httpReq.Headers.Add("anthropic-version", "2023-06-01");

        logger.LogInformation(
            "Calling Anthropic API (model={Model}, maxTokens={MaxTokens})", model, maxTokens);

        var response = await client.SendAsync(httpReq, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Anthropic API error {Code}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode(); // throws HttpRequestException
        }

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Anthropic API returned a null response body.");

        var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text
            ?? throw new InvalidOperationException(
                "Anthropic API response contained no text content block.");

        logger.LogInformation(
            "Anthropic API response: stopReason={Stop}, inputTokens={In}, outputTokens={Out}",
            result.StopReason,
            result.Usage?.InputTokens,
            result.Usage?.OutputTokens);

        return text;
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
}
