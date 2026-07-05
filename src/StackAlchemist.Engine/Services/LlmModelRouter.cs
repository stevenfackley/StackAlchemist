using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Derives the provider + real model id from a stored <c>preferred_model</c> string. The web app
/// (ByokSettingsForm / ALLOWED_PROFILE_MODELS) stores provider-prefixed values for BYOK-only
/// providers ("openai/gpt-4o-mini", "openrouter/anthropic/claude-3.5-sonnet"); an unprefixed value
/// is an Anthropic model sent verbatim to the Messages API.
/// </summary>
public static class LlmModelRouter
{
    private const string OpenAiPrefix = "openai/";
    private const string OpenRouterPrefix = "openrouter/";

    /// <summary>
    /// Maps e.g. "openai/gpt-4o-mini" → (OpenAi, "gpt-4o-mini") and
    /// "openrouter/anthropic/claude-3.5-sonnet" → (OpenRouter, "anthropic/claude-3.5-sonnet").
    /// Any other value is treated as an Anthropic model id.
    /// </summary>
    public static (LlmProvider Provider, string Model) Resolve(string model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.StartsWith(OpenAiPrefix, StringComparison.Ordinal))
            return (LlmProvider.OpenAi, model[OpenAiPrefix.Length..]);

        if (model.StartsWith(OpenRouterPrefix, StringComparison.Ordinal))
            return (LlmProvider.OpenRouter, model[OpenRouterPrefix.Length..]);

        return (LlmProvider.Anthropic, model);
    }
}
