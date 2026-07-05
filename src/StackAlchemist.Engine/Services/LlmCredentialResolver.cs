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
    // Anthropic models we are willing to bill on OUR global key. Mirrors the web-side
    // ALLOWED_PROFILE_MODELS Anthropic entries; the engine re-checks because the profiles row is
    // RLS-writable and the web allowlist can be bypassed via direct PostgREST. A user who brings
    // their own Anthropic key is not constrained by this — they pay for whatever model they pick.
    private static readonly HashSet<string> AnthropicGlobalKeyModelAllowList = new(StringComparer.Ordinal)
    {
        "claude-sonnet-4-6",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022",
    };

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

        // Bind the stored key to the provider it was issued for (by key prefix). A BYOK key is NEVER
        // forwarded to a different vendor's API — e.g. a stored OpenAI key must not be sent to
        // Anthropic just because preferred_model points at Claude. A mismatch is treated as "no key".
        if (apiKey is not null && LlmModelRouter.ProviderForKey(apiKey) != provider)
        {
            LogKeyProviderMismatch(logger, generationId, provider.ToString());
            apiKey = null;
        }

        if (provider == LlmProvider.Anthropic)
        {
            if (apiKey is null)
            {
                // No usable BYOK key → this generation spends OUR global Anthropic key. Constrain the
                // model to a known allowlist so a directly-written preferred_model (the profiles
                // UPDATE policy is RLS-writable, bypassing the web-side allowlist) cannot pin us to an
                // arbitrarily expensive model (e.g. Opus). Unknown → the configured default.
                if (!string.Equals(realModel, globalModel, StringComparison.Ordinal)
                    && !AnthropicGlobalKeyModelAllowList.Contains(realModel))
                {
                    LogModelDowngraded(logger, generationId, realModel);
                    realModel = globalModel;
                }

                // Pure default (no key, default model) → null keeps the unchanged global path.
                if (string.Equals(realModel, globalModel, StringComparison.Ordinal))
                    return null;
            }

            // A per-user Anthropic model (allowlisted, or any model when the user brought their own
            // Anthropic key and pays for it themselves).
            LogResolved(logger, generationId, provider.ToString(), realModel, apiKey is not null);
            return new LlmCallOptions(provider, realModel, apiKey);
        }

        // BYOK-only providers (OpenAI / OpenRouter): pass through even without a matching key so the
        // provider client fails the generation with a clear, user-safe ByokConfigException — never
        // a worker crash and never a silent fallback to our key for a provider we have no key for.
        LogResolved(logger, generationId, provider.ToString(), realModel, apiKey is not null);
        return new LlmCallOptions(provider, realModel, apiKey);
    }

    [LoggerMessage(EventId = 850, Level = LogLevel.Information, Message = "Resolved LLM credentials for generation {Id}: provider={Provider}, model={Model}, byok={HasKey}")]
    private static partial void LogResolved(ILogger logger, string id, string provider, string model, bool hasKey);

    [LoggerMessage(EventId = 851, Level = LogLevel.Warning, Message = "Failed to resolve LLM credentials for generation {Id} — using global config")]
    private static partial void LogResolveFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 852, Level = LogLevel.Warning, Message = "Stored BYOK key for generation {Id} does not belong to the routed provider {Provider} — ignoring it (key never cross-forwarded)")]
    private static partial void LogKeyProviderMismatch(ILogger logger, string id, string provider);

    [LoggerMessage(EventId = 853, Level = LogLevel.Warning, Message = "Requested Anthropic model {Model} for generation {Id} is not allowlisted for the global key — downgrading to the default")]
    private static partial void LogModelDowngraded(ILogger logger, string id, string model);
}
