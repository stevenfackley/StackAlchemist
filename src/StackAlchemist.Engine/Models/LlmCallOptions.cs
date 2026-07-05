namespace StackAlchemist.Engine.Models;

/// <summary>
/// LLM provider a generation routes to, derived from the stored preferred_model prefix.
/// </summary>
public enum LlmProvider
{
    Anthropic,
    OpenAi,
    OpenRouter,
}

/// <summary>
/// Resolved per-generation LLM routing context. Built once at generation start from the owning
/// user's profile (preferred_model + decrypted api_key_override) and threaded through every LLM
/// call in that generation — codegen, Swiss-Cheese injection, and build-repair all use the SAME
/// provider, model, and key. Null anywhere means "use the global Anthropic config", byte-for-byte
/// the pre-BYOK behavior.
///
/// <see cref="ApiKey"/> is decrypted key material: it is deliberately redacted from
/// <see cref="ToString"/> so an accidental log of this record can never leak a user's key.
/// </summary>
public sealed record LlmCallOptions(
    LlmProvider Provider,
    string Model,
    string? ApiKey)
{
    /// <summary>True when a BYOK key was resolved. Log this, never <see cref="ApiKey"/>.</summary>
    public bool HasKey => !string.IsNullOrEmpty(ApiKey);

    // Records auto-generate a ToString that prints every property — that would leak ApiKey into
    // any log line that interpolates this record. Redact it explicitly.
    public override string ToString() =>
        $"LlmCallOptions {{ Provider = {Provider}, Model = {Model}, HasKey = {HasKey} }}";
}

/// <summary>
/// Raw credential fields read from a generation's owning profile: the encrypted BYOK key
/// ciphertext (or null) and the preferred_model string (or null). Decryption happens later in
/// <c>ByokKeyProtector</c> — this record never carries plaintext.
/// </summary>
public sealed record ProfileCredential(string? ApiKeyCiphertext, string? PreferredModel);
