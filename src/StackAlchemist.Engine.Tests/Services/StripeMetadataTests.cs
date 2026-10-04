using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class StripeMetadataTests
{
    [Fact]
    public void Clip_LeavesShortValuesAndMapsNullToEmpty()
    {
        StripeMetadata.Clip("build a CRM").Should().Be("build a CRM");
        StripeMetadata.Clip(null).Should().Be("");
        StripeMetadata.Clip(new string('a', 500)).Should().HaveLength(500);
    }

    [Fact]
    public void Clip_CutsLongValuesToStripesLimit()
    {
        // The web accepts 2,000-character prompts; Stripe rejects metadata values over 500.
        StripeMetadata.Clip(new string('p', 2000)).Should().HaveLength(StripeMetadata.MaxValueLength);
    }

    [Fact]
    public void Clip_NeverSplitsASurrogatePair()
    {
        var value = new string('a', 499) + "\U0001F680" + "tail"; // a rocket emoji straddles the cut
        var clipped = StripeMetadata.Clip(value);

        clipped.Should().HaveLength(499);
        char.IsHighSurrogate(clipped[^1]).Should().BeFalse();
    }
}
