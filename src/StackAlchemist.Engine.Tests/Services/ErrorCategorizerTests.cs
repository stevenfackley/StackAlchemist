using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class ErrorCategorizerTests
{
    public static TheoryData<Exception, string> Cases() => new()
    {
        { new SchemaExtractionException("bad json"), ErrorCategorizer.Schema },
        { new SchemaValidationException("unknown entity"), ErrorCategorizer.Schema },
        { new MalformedLlmOutputException("unclosed block"), ErrorCategorizer.Schema },
        { new TruncatedLlmResponseException("hit max_tokens"), ErrorCategorizer.Schema },
        { new LlmRateLimitException("429 after retries"), ErrorCategorizer.RateLimit },
        { new HttpRequestException("connection reset"), ErrorCategorizer.Network },
        { new TimeoutException("timed out"), ErrorCategorizer.Network },
        { new TaskCanceledException("http timeout"), ErrorCategorizer.Network },
        { new IOException("disk error"), ErrorCategorizer.Network },
        { new InvalidOperationException("anything else"), ErrorCategorizer.Internal },
        { new InvalidStateTransitionException("bad transition"), ErrorCategorizer.Internal },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Categorize_MapsExceptionToCategory(Exception ex, string expected)
    {
        ErrorCategorizer.Categorize(ex).Should().Be(expected);
    }

    [Fact]
    public void Categorize_UnwrapsInjectionFailedException()
    {
        var wrapped = new InjectionFailedException(
            "zone fill failed", new LlmRateLimitException("overloaded"));

        ErrorCategorizer.Categorize(wrapped).Should().Be(ErrorCategorizer.RateLimit);
    }

    [Fact]
    public void Categorize_InjectionFailedWithoutInner_IsInternal()
    {
        ErrorCategorizer.Categorize(new InjectionFailedException("no inner", null!))
            .Should().Be(ErrorCategorizer.Internal);
    }
}
