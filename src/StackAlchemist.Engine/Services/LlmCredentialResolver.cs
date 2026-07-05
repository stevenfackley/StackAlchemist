using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface ILlmCredentialResolver
{
    /// <summary>
    /// Resolves the per-generation LLM routing options for the owning user, or null when the row
    /// has no override AND uses the default model (→ the unchanged global Anthropic path). Never
    /// throws and never logs key material: a decryption failure degrades per provider (Anthropic
    /// falls back to the global key; BYOK-only providers surface a clean error at call time).
    /// </summary>
    Task<LlmCallOptions?> ResolveAsync(string generationId, CancellationToken ct);
}

/// <summary>
/// Reads the owning user's profile for a generation (encrypted api_key_override + preferred_model),
/// decrypts the key via <see cref="ByokKeyProtector"/>, and builds the <see cref="LlmCallOptions"/>
/// that thread through the whole generation. Resolves ONCE per generation (called at generation
/// start); the result is carried on the <c>GenerationContext</c> so codegen, injection, and
/// build-repair reuse the same provider/model/key.
/// </summary>
public sealed partial class LlmCredentialResolver(
    IDeliveryService deliveryService,
    ByokKeyProtector keyProtector,
    IConfiguration config,
    ILogger<LlmCredentialResolver> logger) : ILlmCredentialResolver
{
    public async Task<LlmCallOptions?> ResolveAsync(string generationId, CancellationToken ct)
    {
        ProfileCredential? credential;
        try
        {
            credential = await deliveryService.GetGenerationCredentialAsync(generationId, ct);
        }
        catch (Exception ex)
        {
            // A profile-read failure must never crash the pipeline — fall back to global config.
            LogResolveFailed(logger, ex, generationId);
            return null;
        }

        if (credential is null)
            return null;

        var globalModel = config["Anthropic:Model"] ?? AnthropicDefaults.ModelId;
        var model = string.IsNullOrWhiteSpace(credential.PreferredModel) ? globalModel : credential.PreferredModel!;
        var (provider, realModel) = LlmModelRouter.Resolve(model);
        var apiKey = keyProtector.TryDecrypt(credential.ApiKeyCiphertext);

        if (provider == LlmProvider.Anthropic)
        {
            // Pure default (no BYOK key, default model) → null keeps the unchanged global path.
            if (apiKey is null && string.Equals(model, globalModel, StringComparison.Ordinal))
                return null;

            // Per-user Anthropic model and/or BYOK key. A null key here falls back to the global
            // Anthropic key inside AnthropicLlmClient (graceful degrade for an undecryptable key).
            LogResolved(logger, generationId, provider.ToString(), realModel, apiKey is not null);
            return new LlmCallOptions(provider, realModel, apiKey);
        }

        // BYOK-only providers (OpenAI / OpenRouter): pass through even without a key so the
        // provider client fails the generation with a clear, user-safe ByokConfigException — never
        // a worker crash and never a silent fallback to our key for a provider we have no key for.
        LogResolved(logger, generationId, provider.ToString(), realModel, apiKey is not null);
        return new LlmCallOptions(provider, realModel, apiKey);
    }

    [LoggerMessage(EventId = 850, Level = LogLevel.Information, Message = "Resolved LLM credentials for generation {Id}: provider={Provider}, model={Model}, byok={HasKey}")]
    private static partial void LogResolved(ILogger logger, string id, string provider, string model, bool hasKey);

    [LoggerMessage(EventId = 851, Level = LogLevel.Warning, Message = "Failed to resolve LLM credentials for generation {Id} — using global config")]
    private static partial void LogResolveFailed(ILogger logger, Exception ex, string id);
}
