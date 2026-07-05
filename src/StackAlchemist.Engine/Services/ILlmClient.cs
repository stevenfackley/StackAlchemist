using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Abstraction over the LLM API. Mocked in Phase 3, replaced with real Anthropic SDK in Phase 4.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Send a prompt to the LLM and return the text response plus usage metadata.
    /// <paramref name="options"/> carries the resolved per-generation provider/model/key (BYOK +
    /// per-user model routing). When null, the global Anthropic config is used — byte-for-byte the
    /// pre-BYOK behavior. Resolve it ONCE per generation and pass the same instance to every call
    /// (codegen, injection, build-repair) so they share one provider/model/key.
    /// </summary>
    Task<LlmResponse> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        LlmCallOptions? options = null,
        CancellationToken ct = default);
}
