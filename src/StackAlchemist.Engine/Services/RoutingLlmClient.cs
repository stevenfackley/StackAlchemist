using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// The registered <see cref="ILlmClient"/>. Routes each call to the right provider based on the
/// per-generation <see cref="LlmCallOptions"/> resolved at generation start:
/// <list type="bullet">
///   <item>null → the global default client (Anthropic when ANTHROPIC_API_KEY is set, else the
///   mock) — byte-for-byte the pre-BYOK path.</item>
///   <item>Anthropic → the Anthropic Messages API (BYOK key when present, else the global key).</item>
///   <item>OpenAI / OpenRouter → OpenAI-compatible Chat Completions (BYOK required).</item>
/// </list>
/// Never logs key material — only the provider, model, and a <c>byok</c> presence flag.
/// </summary>
public sealed partial class RoutingLlmClient(
    AnthropicLlmClient anthropic,
    OpenAiCompatibleLlmClient openAiCompatible,
    MockLlmClient mock,
    IConfiguration config,
    ILogger<RoutingLlmClient> logger) : ILlmClient
{
    private bool HasGlobalAnthropicKey => !string.IsNullOrWhiteSpace(config["Anthropic:ApiKey"]);

    public Task<LlmResponse> GenerateAsync(
        string systemPrompt, string userPrompt, LlmCallOptions? options = null, CancellationToken ct = default)
    {
        if (options is null)
        {
            // No per-user override → byte-for-byte the pre-BYOK path.
            return HasGlobalAnthropicKey
                ? anthropic.GenerateAsync(systemPrompt, userPrompt, null, ct)
                : mock.GenerateAsync(systemPrompt, userPrompt, null, ct);
        }

        switch (options.Provider)
        {
            case LlmProvider.OpenAi:
            case LlmProvider.OpenRouter:
                LogRouting(logger, options.Provider.ToString(), options.Model, options.HasKey);
                return openAiCompatible.GenerateAsync(systemPrompt, userPrompt, options, ct);

            case LlmProvider.Anthropic:
            default:
                // Per-user Anthropic model routing. When neither a global key NOR a BYOK key is
                // available (local dev / CI), the mock keeps the pipeline working.
                if (!options.HasKey && !HasGlobalAnthropicKey)
                    return mock.GenerateAsync(systemPrompt, userPrompt, null, ct);

                LogRouting(logger, options.Provider.ToString(), options.Model, options.HasKey);
                return anthropic.GenerateAsync(systemPrompt, userPrompt, options, ct);
        }
    }

    [LoggerMessage(EventId = 840, Level = LogLevel.Information, Message = "Routing LLM call to {Provider} (model={Model}, byok={HasKey})")]
    private static partial void LogRouting(ILogger logger, string provider, string model, bool hasKey);
}
