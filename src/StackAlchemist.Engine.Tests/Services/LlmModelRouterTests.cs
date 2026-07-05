using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class LlmModelRouterTests
{
    [Theory]
    [InlineData("claude-sonnet-4-6", LlmProvider.Anthropic, "claude-sonnet-4-6")]
    [InlineData("claude-3-5-haiku-20241022", LlmProvider.Anthropic, "claude-3-5-haiku-20241022")]
    [InlineData("openai/gpt-4o-mini", LlmProvider.OpenAi, "gpt-4o-mini")]
    [InlineData("openrouter/anthropic/claude-3.5-sonnet", LlmProvider.OpenRouter, "anthropic/claude-3.5-sonnet")]
    public void Resolve_MapsPrefixToProviderAndStripsIt(string model, LlmProvider expectedProvider, string expectedModel)
    {
        var (provider, realModel) = LlmModelRouter.Resolve(model);

        provider.Should().Be(expectedProvider);
        realModel.Should().Be(expectedModel);
    }

    [Fact]
    public void Resolve_UnprefixedValue_IsAnthropicVerbatim()
    {
        var (provider, realModel) = LlmModelRouter.Resolve("some-future-anthropic-model");

        provider.Should().Be(LlmProvider.Anthropic);
        realModel.Should().Be("some-future-anthropic-model");
    }
}
