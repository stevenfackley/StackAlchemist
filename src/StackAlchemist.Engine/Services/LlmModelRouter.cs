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
    /// Any other value is treated as an Anthropic model id. Prefix matching is case-insensitive so a
    /// mis-cased "OpenAI/…" cannot silently fall through to the Anthropic path.
    /// </summary>
    public static (LlmProvider Provider, string Model) Resolve(string model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.StartsWith(OpenAiPrefix, StringComparison.OrdinalIgnoreCase))
            return (LlmProvider.OpenAi, model[OpenAiPrefix.Length..]);

        if (model.StartsWith(OpenRouterPrefix, StringComparison.OrdinalIgnoreCase))
            return (LlmProvider.OpenRouter, model[OpenRouterPrefix.Length..]);

        return (LlmProvider.Anthropic, model);
    }

    /// <summary>
    /// Best-effort provider identity of a raw API key from its documented prefix, so a stored BYOK
    /// key is only ever transmitted to the vendor that issued it — never cross-forwarded (e.g. an
    /// OpenAI key must not reach api.anthropic.com just because the model dropdown is on Claude).
    /// Anthropic keys begin "sk-ant-", OpenRouter "sk-or-", OpenAI "sk-" (checked last). Returns null
    /// for an unrecognized shape, which the resolver treats as "no usable key".
    /// </summary>
    public static LlmProvider? ProviderForKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        if (apiKey.StartsWith("sk-ant-", StringComparison.Ordinal))
            return LlmProvider.Anthropic;
        if (apiKey.StartsWith("sk-or-", StringComparison.Ordinal))
            return LlmProvider.OpenRouter;
        if (apiKey.StartsWith("sk-", StringComparison.Ordinal))
            return LlmProvider.OpenAi;
        return null;
    }
}
