using FluentAssertions;
using StackAlchemist.Engine.Middleware;

namespace StackAlchemist.Engine.Tests.Middleware;

/// <summary>
/// The service-key comparison: only the exact configured key passes. (That a request without the
/// key gets a 401 from the running Engine is covered in <c>RateLimitTests</c>.)
/// </summary>
public sealed class EngineCallerTests
{
    private const string Configured = "engine-service-key-0123456789abcdef";

    [Fact]
    public void TheConfiguredKey_Passes() =>
        EngineCaller.HasServiceKey(Configured, Configured).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("engine-service-key-0123456789abcdee")] // last character differs
    [InlineData("Engine-service-key-0123456789abcdef")] // case differs
    [InlineData("engine-service-key")] // a prefix of the key
    [InlineData("engine-service-key-0123456789abcdef0")] // the key plus one character
    [InlineData(" engine-service-key-0123456789abcdef")]
    public void AnythingElse_IsRefused(string? presented) =>
        EngineCaller.HasServiceKey(presented, Configured).Should().BeFalse();
}
