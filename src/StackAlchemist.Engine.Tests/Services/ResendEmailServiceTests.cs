using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Tests for ResendEmailService.MaskEmail — the helper used to keep raw email
/// addresses out of structured logs (GDPR/CCPA). The function is exercised on
/// every email send + every no-op send, so misbehavior is high-blast-radius.
/// </summary>
public class ResendEmailServiceTests
{
    [Theory]
    [InlineData("jane.doe@example.com", "j***@example.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("J@gmail.com", "J***@gmail.com")]
    public void MaskEmail_TypicalAddress_KeepsFirstCharAndDomain(string input, string expected)
    {
        ResendEmailService.MaskEmail(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MaskEmail_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        ResendEmailService.MaskEmail(input).Should().BeEmpty();
    }

    [Theory]
    [InlineData("noatsignhere", "***")]
    [InlineData("@nolocalpart.com", "***")]
    public void MaskEmail_Malformed_ReturnsTripleStar(string input, string expected)
    {
        ResendEmailService.MaskEmail(input).Should().Be(expected);
    }

    [Fact]
    public void MaskEmail_PreservesDomain_ForSupportDiagnostics()
    {
        // Support needs to know the domain to triage delivery issues.
        ResendEmailService.MaskEmail("user@customer-domain.io").Should().EndWith("@customer-domain.io");
    }

    [Fact]
    public void MaskEmail_NeverContainsLocalPartAfterFirstChar()
    {
        const string raw = "verylongusername.with.dots@example.com";
        var masked = ResendEmailService.MaskEmail(raw);
        masked.Should().NotContain("erylongusername");
        masked.Should().NotContain("with.dots");
    }
}
