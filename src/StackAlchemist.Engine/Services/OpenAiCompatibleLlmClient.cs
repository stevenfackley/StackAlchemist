using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// LLM client for OpenAI-compatible Chat Completions providers (OpenAI, OpenRouter). Both are
/// BYOK-only — there is no global key for them — so a missing/undecryptable key fails the
/// generation with a user-safe <see cref="ByokConfigException"/> (never a plaintext-key leak).
/// Shares retry/backoff with the Anthropic client via <see cref="LlmHttpRetry"/>. Non-streaming by
/// design: the simplest correct Chat Completions call, matching the one-shot request/response shape
/// the rest of the pipeline expects.
/// </summary>
public sealed partial class OpenAiCompatibleLlmClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<OpenAiCompatibleLlmClient> logger)
{
    public const string OpenAiHttpClientName = "OpenAI";
    public const string OpenRouterHttpClientName = "OpenRouter";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<LlmResponse> GenerateAsync(
        string systemPrompt, string userPrompt, LlmCallOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.HasKey)
        {
            // BYOK-only provider with no usable key — fail cleanly with a user-safe message that
            // never contains key material.
            throw new ByokConfigException(
                $"This generation is set to use {ProviderLabel(options.Provider)}, which needs your own API key. " +
                "Add a valid key under Bring Your Own Key in dashboard API settings (or switch back to the default Claude model) and try again.");
        }

        var maxTokens = int.TryParse(config["Anthropic:MaxTokens"], out var mt) ? mt : 8_192;
        var clientName = options.Provider == LlmProvider.OpenRouter ? OpenRouterHttpClientName : OpenAiHttpClientName;
        var client = httpClientFactory.CreateClient(clientName);

        LogCallingApi(logger, ProviderLabel(options.Provider), options.Model, maxTokens);

        using var response = await LlmHttpRetry.SendWithRetryAsync(
            client,
            () => BuildRequest(options, maxTokens, systemPrompt, userPrompt),
            logger,
            ProviderLabel(options.Provider),
            ct);

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException($"{ProviderLabel(options.Provider)} API returned a null response body.");

        var choice = result.Choices.FirstOrDefault()
            ?? throw new InvalidOperationException($"{ProviderLabel(options.Provider)} API response contained no choices.");

        var text = choice.Message?.Content
            ?? throw new InvalidOperationException($"{ProviderLabel(options.Provider)} API response contained no message content.");

        LogApiResponse(logger, ProviderLabel(options.Provider), choice.FinishReason,
            result.Usage?.PromptTokens ?? 0, result.Usage?.CompletionTokens ?? 0);

        // Map OpenAI's "length" finish reason onto Anthropic's "max_tokens" so the shared
        // LlmResponseGuard truncation check works uniformly across providers.
        var stopReason = choice.FinishReason == "length" ? "max_tokens" : choice.FinishReason;

        return new LlmResponse(
            text,
            result.Usage?.PromptTokens ?? 0,
            result.Usage?.CompletionTokens ?? 0,
            options.Model,
            stopReason);
    }

    private static HttpRequestMessage BuildRequest(
        LlmCallOptions options, int maxTokens, string systemPrompt, string userPrompt)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
        messages.Add(new ChatMessage { Role = "user", Content = userPrompt });

        var requestBody = new ChatCompletionRequest
        {
            Model = options.Model,
            MaxTokens = maxTokens,
            Messages = messages,
        };

        // Relative to the client's BaseAddress ("…/v1/"), so no leading slash.
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(requestBody, options: JsonOpts),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        return request;
    }

    private static string ProviderLabel(LlmProvider provider) => provider switch
    {
        LlmProvider.OpenAi => "OpenAI",
        LlmProvider.OpenRouter => "OpenRouter",
        _ => provider.ToString(),
    };

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("max_tokens")]
        public required int MaxTokens { get; init; }

        [JsonPropertyName("messages")]
        public required List<ChatMessage> Messages { get; init; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = "";

        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice> Choices { get; init; } = [];

        [JsonPropertyName("usage")]
        public ChatUsage? Usage { get; init; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; init; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; init; }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 830, Level = LogLevel.Information, Message = "Calling {Provider} API (model={Model}, maxTokens={MaxTokens})")]
    private static partial void LogCallingApi(ILogger logger, string provider, string model, int maxTokens);

    [LoggerMessage(EventId = 831, Level = LogLevel.Information, Message = "{Provider} API response: finishReason={Finish}, promptTokens={In}, completionTokens={Out}")]
    private static partial void LogApiResponse(ILogger logger, string provider, string? finish, int @in, int @out);
}
