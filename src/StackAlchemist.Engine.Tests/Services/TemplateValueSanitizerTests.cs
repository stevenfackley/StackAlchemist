using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class TemplateValueSanitizerTests
{
    [Theory]
    [InlineData("{{7*7}}App", "App")]
    [InlineData("My{{ProjectName}}App", "MyProjectNameApp")]
    [InlineData("../etc/passwd", "etcpasswd")]
    [InlineData("Task Manager", "TaskManager")]
    [InlineData("order-item", "orderitem")]
    [InlineData("Product", "Product")]
    [InlineData("snake_case_ok", "snake_case_ok")]
    public void SanitizeIdentifier_StripsUnsafeCharacters(string input, string expected)
    {
        TemplateValueSanitizer.SanitizeIdentifier(input, "Fallback").Should().Be(expected);
    }

    [Theory]
    [InlineData("123Product", "Product")]
    [InlineData("_private", "private")]
    [InlineData("99_bottles", "bottles")]
    public void SanitizeIdentifier_TrimsToLeadingLetter(string input, string expected)
    {
        TemplateValueSanitizer.SanitizeIdentifier(input, "Fallback").Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{{}}")]
    [InlineData("12345")]
    [InlineData(null)]
    public void SanitizeIdentifier_EmptyAfterStripping_ReturnsFallback(string? input)
    {
        TemplateValueSanitizer.SanitizeIdentifier(input, "GeneratedApp").Should().Be("GeneratedApp");
    }
}
