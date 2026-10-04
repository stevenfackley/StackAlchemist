using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// LLM client that calls the Anthropic Messages API (Claude Sonnet 5.5 by default; configurable via
/// <c>Anthropic:Model</c>). Used by <see cref="RoutingLlmClient"/> for both the global default path
/// (no <see cref="LlmCallOptions"/>) and per-user Anthropic model/BYOK-key routing. Retry/backoff is
/// shared with the OpenAI-compatible client via <see cref="LlmHttpRetry"/>.
///
/// Request shape for current models: no <c>thinking</c> field (adaptive thinking is the default
/// and <c>disabled</c> is a 400 on Sonnet 5.5 / Opus 5.5), no sampling parameters (non-default
/// values are a 400), <c>output_config.effort</c> where the model supports it (thinking depth is
/// controlled there and counts toward <c>max_tokens</c>), and <c>fallbacks: "default"</c> where the
/// model accepts server-side fallback, so a safety-classifier decline is retried on Anthropic's
/// recommended model inside the same call. Content is read by block type: thinking blocks come
/// first and are skipped, and only text after the last <c>fallback</c> block is the answer.
/// </summary>
public sealed partial class AnthropicLlmClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<AnthropicLlmClient> logger) : ILlmClient
{
    public const string HttpClientName = "Anthropic";

    /// <summary>Output budget when <c>Anthropic:MaxTokens</c> is unset. Covers thinking plus the reply
    /// (Sonnet 5.x tokenizes ~30% more tokens than Sonnet 4.6 for the same text) and stays well
    /// inside the 10-minute HTTP timeout for a non-streaming request.</summary>
    public const int DefaultMaxTokens = 20_000;

    /// <summary>Effort when <c>Anthropic:Effort</c> is unset or invalid — Anthropic's starting point
    /// for code generation on Sonnet 5.5 (effort levels were recalibrated; re-sweep before raising).</summary>
    public const string DefaultEffort = "medium";

    private const string FallbackBetaHeader = "server-side-fallback-2026-07-01";

    private static readonly HashSet<string> EffortLevels = new(StringComparer.Ordinal)
    {
        "low", "medium", "high", "xhigh", "max",
    };

    // Models that take output_config.effort. Haiku 4.5, Sonnet 4.5 and older reject it.
    private static readonly string[] EffortModelPrefixes =
    [
        "claude-sonnet-5", "claude-opus-5", "claude-fable-", "claude-mythos-",
        "claude-opus-4-8", "claude-opus-4-7", "claude-opus-4-6", "claude-opus-4-5", "claude-sonnet-4-6",
    ];

    // Models that accept the scalar fallbacks: "default" form (Claude API only, which is all we call).
    private static readonly HashSet<string> DefaultFallbackModels = new(StringComparer.Ordinal)
    {
        "claude-sonnet-5-5", "claude-opus-5-5", "claude-opus-5", "claude-fable-5-1",
    };

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
        var maxTokens = int.TryParse(config["Anthropic:MaxTokens"], out var mt) ? mt : DefaultMaxTokens;
        var effort = SupportsEffort(model) ? ResolveEffort(config["Anthropic:Effort"]) : null;
        var useFallbacks = DefaultFallbackModels.Contains(model);
        var client = httpClientFactory.CreateClient(HttpClientName);

        LogCallingApi(logger, model, maxTokens, effort ?? "n/a");

        using var response = await LlmHttpRetry.SendWithRetryAsync(
            client,
            () => BuildRequest(apiKey, model, maxTokens, effort, useFallbacks, systemPrompt, userPrompt),
            logger,
            "Anthropic",
            ct);

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Anthropic API returned a null response body.");

        LogApiResponse(logger, result.StopReason, result.Usage?.InputTokens ?? 0, result.Usage?.OutputTokens ?? 0);

        // Whole chain declined (the fallback model, if any, refused too). Branch before reading content.
        var servedBy = string.IsNullOrWhiteSpace(result.Model) ? model : result.Model!;
        if (result.StopReason == "refusal")
        {
            LogRefused(logger, servedBy, result.StopDetails?.Category);
            throw new LlmRefusalException(servedBy, result.StopDetails?.Category);
        }

        // Text after the last fallback block is the answer; anything before it is the declining
        // model's partial output. Thinking blocks are skipped by type.
        var lastFallback = result.Content.FindLastIndex(c => c.Type == "fallback");
        var textBlocks = result.Content
            .Skip(lastFallback + 1)
            .Where(c => c.Type == "text" && c.Text is not null)
            .Select(c => c.Text!)
            .ToList();
        if (textBlocks.Count == 0)
            throw new InvalidOperationException("Anthropic API response contained no text content block.");
        if (lastFallback >= 0)
            LogFellBack(logger, model, servedBy);

        if (result.StopReason == "max_tokens")
            LogResponseTruncated(logger, servedBy, maxTokens);

        return new LlmResponse(
            string.Concat(textBlocks),
            result.Usage?.InputTokens ?? 0,
            result.Usage?.OutputTokens ?? 0,
            servedBy,
            result.StopReason);
    }

    internal static bool SupportsEffort(string model) =>
        EffortModelPrefixes.Any(p => model.StartsWith(p, StringComparison.Ordinal));

    private static string ResolveEffort(string? configured) =>
        configured is not null && EffortLevels.Contains(configured.Trim()) ? configured.Trim() : DefaultEffort;

    private static HttpRequestMessage BuildRequest(
        string apiKey,
        string model,
        int maxTokens,
        string? effort,
        bool useFallbacks,
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
            OutputConfig = effort is null ? null : new AnthropicOutputConfig { Effort = effort },
            Fallbacks = useFallbacks ? "default" : null,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(requestBody, options: JsonOpts),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        if (useFallbacks)
            request.Headers.Add("anthropic-beta", FallbackBetaHeader);
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

        [JsonPropertyName("output_config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AnthropicOutputConfig? OutputConfig { get; init; }

        [JsonPropertyName("fallbacks")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Fallbacks { get; init; }
    }

    private sealed class AnthropicOutputConfig
    {
        [JsonPropertyName("effort")]
        public required string Effort { get; init; }
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
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("content")]
        public List<AnthropicContentBlock> Content { get; init; } = [];

        [JsonPropertyName("stop_details")]
        public AnthropicStopDetails? StopDetails { get; init; }

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

    private sealed class AnthropicStopDetails
    {
        [JsonPropertyName("category")]
        public string? Category { get; init; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }
    }

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 800, Level = LogLevel.Information, Message = "Calling Anthropic API (model={Model}, maxTokens={MaxTokens}, effort={Effort})")]
    private static partial void LogCallingApi(ILogger logger, string model, int maxTokens, string effort);

    [LoggerMessage(EventId = 801, Level = LogLevel.Information, Message = "Anthropic API response: stopReason={Stop}, inputTokens={In}, outputTokens={Out}")]
    private static partial void LogApiResponse(ILogger logger, string? stop, int @in, int @out);

    [LoggerMessage(EventId = 804, Level = LogLevel.Warning, Message = "Anthropic response TRUNCATED at max_tokens (model={Model}, maxTokens={MaxTokens}) — output is incomplete")]
    private static partial void LogResponseTruncated(ILogger logger, string model, int maxTokens);

    [LoggerMessage(EventId = 805, Level = LogLevel.Warning, Message = "Anthropic request declined (stop_reason=refusal, model={Model}, category={Category})")]
    private static partial void LogRefused(ILogger logger, string model, string? category);

    [LoggerMessage(EventId = 806, Level = LogLevel.Information, Message = "Anthropic server-side fallback: {Requested} declined, {ServedBy} answered")]
    private static partial void LogFellBack(ILogger logger, string requested, string servedBy);
}
