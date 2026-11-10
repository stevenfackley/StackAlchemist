using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The Stripe checkout line item: the price charged, and a description that promises only what
/// the tier delivers, for the stack the buyer chose.
/// </summary>
public sealed class TierCatalogTests
{
    [Theory]
    [InlineData(1, 29_900, "StackAlchemist Blueprint")]
    [InlineData(2, 59_900, "StackAlchemist Boilerplate")]
    [InlineData(3, 99_900, "StackAlchemist Infrastructure")]
    public void PaidTiers_HaveTheirPriceAndName(int tier, long cents, string name)
    {
        TierCatalog.TryGetPaidTier(tier, ProjectType.DotNetNextJs, out var paid).Should().BeTrue();

        paid.Cents.Should().Be(cents);
        paid.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(0)] // Spark is free: nothing to check out
    [InlineData(4)]
    [InlineData(-1)]
    public void OtherTiers_HaveNoCheckout(int tier) =>
        TierCatalog.TryGetPaidTier(tier, ProjectType.DotNetNextJs, out _).Should().BeFalse();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void SourceTiers_NameTheStackTheBuyerChose(int tier)
    {
        TierCatalog.TryGetPaidTier(tier, ProjectType.PythonReact, out var python);
        TierCatalog.TryGetPaidTier(tier, ProjectType.DotNetNextJs, out var dotnet);

        python.Description.Should().Contain("FastAPI + React").And.NotContain(".NET");
        dotnet.Description.Should().Contain(".NET + Next.js").And.NotContain("FastAPI");
    }

    [Theory]
    [InlineData(ProjectType.DotNetNextJs)]
    [InlineData(ProjectType.PythonReact)]
    public void Blueprint_PromisesItsTwoDocumentsAndNothingElse(ProjectType projectType)
    {
        TierCatalog.TryGetPaidTier(1, projectType, out var blueprint);

        // What Tier1ArtifactBuilder writes. It writes no OpenAPI spec and no SQL.
        blueprint.Description.Should().Contain("schema.json").And.Contain("api-docs.md");
        blueprint.Description.Should().NotContainAny("OpenAPI", "SQL", "migration");
        // Stack-agnostic: the buyer's stack choice does not change it.
        blueprint.Description.Should().NotContainAny(".NET", "FastAPI");
    }
}
