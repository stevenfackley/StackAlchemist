using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Tests for tier gating logic — ensures correct deliverables per purchased tier.
///
/// Tier 1 (Blueprint, $299):      Schema + API docs only
/// Tier 2 (Boilerplate, $599):    Full codebase zip
/// Tier 3 (Infrastructure, $999): Codebase + IaC + Helm + runbook
/// </summary>
public class TierGatingServiceTests
{
    private readonly TierGatingService _sut = new();

    [Fact]
    public void GetDeliverables_Tier1_ReturnsSchemaAndDocsOnly()
    {
        var deliverables = _sut.GetDeliverables(1);

        deliverables.IncludesSchema.Should().BeTrue();
        deliverables.IncludesApiDocs.Should().BeTrue();
        deliverables.IncludesCodeGeneration.Should().BeFalse();
        deliverables.IncludesIaC.Should().BeFalse();
        deliverables.IncludesHelmCharts.Should().BeFalse();
        deliverables.IncludesRunbook.Should().BeFalse();
    }

    [Fact]
    public void GetDeliverables_Tier2_ReturnsSchemaDocsAndCode()
    {
        var deliverables = _sut.GetDeliverables(2);

        deliverables.IncludesSchema.Should().BeTrue();
        deliverables.IncludesApiDocs.Should().BeTrue();
        deliverables.IncludesCodeGeneration.Should().BeTrue();
        deliverables.IncludesIaC.Should().BeFalse();
        deliverables.IncludesHelmCharts.Should().BeFalse();
        deliverables.IncludesRunbook.Should().BeFalse();
    }

    [Fact]
    public void GetDeliverables_Tier3_ReturnsEverything()
    {
        var deliverables = _sut.GetDeliverables(3);

        deliverables.IncludesSchema.Should().BeTrue();
        deliverables.IncludesApiDocs.Should().BeTrue();
        deliverables.IncludesCodeGeneration.Should().BeTrue();
        deliverables.IncludesIaC.Should().BeTrue();
        deliverables.IncludesHelmCharts.Should().BeTrue();
        deliverables.IncludesRunbook.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void GetDeliverables_InvalidTier_ThrowsInvalidTierException(int invalidTier)
    {
        var act = () => _sut.GetDeliverables(invalidTier);

        act.Should().Throw<InvalidTierException>()
           .WithMessage($"*{invalidTier}*valid tiers are 1, 2, 3*");
    }

    [Fact]
    public void ShouldTriggerCodeGeneration_Tier1_ReturnsFalse()
    {
        _sut.ShouldTriggerCodeGeneration(tier: 1).Should().BeFalse();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ShouldTriggerCodeGeneration_Tier2Or3_ReturnsTrue(int tier)
    {
        _sut.ShouldTriggerCodeGeneration(tier).Should().BeTrue();
    }
}
