using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class LlmHttpRetryTests
{
    [Theory]
    // OpenAI's 401 body echoes a redacted-but-partial key; our scrubber removes the whole token.
    [InlineData(
        "{\"error\":{\"message\":\"Incorrect API key provided: sk-proj-abc123DEF456. You can find your key at...\"}}",
        "sk-proj-abc123DEF456")]
    [InlineData("Invalid key sk-ant-api03-secret-value here", "sk-ant-api03-secret-value")]
    [InlineData("openrouter says sk-or-v1-longsecrettoken is bad", "sk-or-v1-longsecrettoken")]
    public void ScrubKeys_RemovesKeyShapedTokens(string body, string leaked)
    {
        var scrubbed = LlmHttpRetry.ScrubKeys(body);

        scrubbed.Should().NotContain(leaked);
        scrubbed.Should().Contain("sk-***redacted***");
    }

    [Fact]
    public void ScrubKeys_LeavesKeylessBodyUntouched()
    {
        const string body = "{\"error\":{\"message\":\"rate limit exceeded\",\"type\":\"rate_limit\"}}";

        LlmHttpRetry.ScrubKeys(body).Should().Be(body);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ScrubKeys_HandlesEmptyBody(string? body)
    {
        LlmHttpRetry.ScrubKeys(body!).Should().Be(body);
    }
}
