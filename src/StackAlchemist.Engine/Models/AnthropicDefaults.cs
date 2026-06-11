namespace StackAlchemist.Engine.Models;

/// <summary>
/// Single code-level source of truth for the Anthropic model the engine targets.
/// Config (<c>Anthropic:Model</c> / the <c>ANTHROPIC_MODEL</c> env var) overrides this
/// at runtime; bump the constant when Anthropic's deprecation schedule requires it.
/// </summary>
public static class AnthropicDefaults
{
    public const string ModelId = "claude-sonnet-4-6";
    public const string ModelDisplayName = "Claude Sonnet 4.6";
}
