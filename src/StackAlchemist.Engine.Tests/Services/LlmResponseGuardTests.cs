using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class LlmResponseGuardTests
{
    [Fact]
    public void ThrowIfTruncated_MaxTokens_Throws()
    {
        var response = new LlmResponse("partial", 10, 8192, "claude-sonnet-4-6", "max_tokens");

        var act = () => LlmResponseGuard.ThrowIfTruncated(response, "generating the application code");

        act.Should().Throw<TruncatedLlmResponseException>()
            .WithMessage("*too large*");
    }

    [Theory]
    [InlineData("end_turn")]
    [InlineData("stop_sequence")]
    [InlineData(null)]
    public void ThrowIfTruncated_OtherStopReasons_DoesNotThrow(string? stopReason)
    {
        var response = new LlmResponse("complete", 10, 100, "claude-sonnet-4-6", stopReason);

        var act = () => LlmResponseGuard.ThrowIfTruncated(response, "anything");

        act.Should().NotThrow();
    }
}
