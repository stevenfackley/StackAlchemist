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

    [Theory]
    [InlineData("OpenAI/gpt-4o-mini", LlmProvider.OpenAi, "gpt-4o-mini")]
    [InlineData("OPENROUTER/anthropic/claude-3.5-sonnet", LlmProvider.OpenRouter, "anthropic/claude-3.5-sonnet")]
    public void Resolve_PrefixMatchingIsCaseInsensitive(string model, LlmProvider expectedProvider, string expectedModel)
    {
        // L1: a mis-cased prefix must not silently fall through to the Anthropic path (which, pre-fix,
        // could pair a stored non-Anthropic key with Anthropic).
        var (provider, realModel) = LlmModelRouter.Resolve(model);

        provider.Should().Be(expectedProvider);
        realModel.Should().Be(expectedModel);
    }

    [Theory]
    [InlineData("sk-ant-api03-abc", LlmProvider.Anthropic)]
    [InlineData("sk-or-v1-abc", LlmProvider.OpenRouter)]
    [InlineData("sk-proj-abc", LlmProvider.OpenAi)]
    [InlineData("sk-abc", LlmProvider.OpenAi)]
    public void ProviderForKey_IdentifiesVendorByPrefix(string key, LlmProvider expected)
    {
        LlmModelRouter.ProviderForKey(key).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-key")]
    [InlineData("Bearer sk-ant-abc")] // must start with the prefix, not merely contain it
    public void ProviderForKey_UnrecognizedShape_ReturnsNull(string? key)
    {
        LlmModelRouter.ProviderForKey(key).Should().BeNull();
    }
}
