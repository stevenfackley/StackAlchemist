namespace StackAlchemist.Engine.Services;

/// <summary>
/// Abstraction over the LLM API. Mocked in Phase 3, replaced with real Anthropic SDK in Phase 4.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Send a prompt to the LLM and return the raw text response.
    /// </summary>
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
