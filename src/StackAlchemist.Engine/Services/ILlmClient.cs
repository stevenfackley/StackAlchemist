using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Abstraction over the LLM API. Mocked in Phase 3, replaced with real Anthropic SDK in Phase 4.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Send a prompt to the LLM and return the text response plus usage metadata.
    /// </summary>
    Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
