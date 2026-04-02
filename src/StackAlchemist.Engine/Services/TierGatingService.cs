using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface ITierGatingService
{
    /// <summary>Returns the deliverable set for the given tier, or throws <see cref="InvalidTierException"/>.</summary>
    TierDeliverables GetDeliverables(int tier);

    /// <summary>Returns true when the tier requires running the LLM code-generation pipeline.</summary>
    bool ShouldTriggerCodeGeneration(int tier);
}

/// <summary>
/// Controls which artifacts are produced based on the purchased tier.
///
/// Tier 1 — Blueprint  ($299): Schema JSON + API docs
/// Tier 2 — Boilerplate ($599): Schema + full compilable codebase zip
/// Tier 3 — Infrastructure ($999): Tier 2 + IaC (AWS CDK / Terraform) + Helm chart + runbook
/// </summary>
public sealed class TierGatingService : ITierGatingService
{
    public TierDeliverables GetDeliverables(int tier) => tier switch
    {
        1 => new TierDeliverables(
            IncludesSchema: true,
            IncludesApiDocs: true,
            IncludesCodeGeneration: false,
            IncludesIaC: false,
            IncludesHelmCharts: false,
            IncludesRunbook: false),

        2 => new TierDeliverables(
            IncludesSchema: true,
            IncludesApiDocs: true,
            IncludesCodeGeneration: true,
            IncludesIaC: false,
            IncludesHelmCharts: false,
            IncludesRunbook: false),

        3 => new TierDeliverables(
            IncludesSchema: true,
            IncludesApiDocs: true,
            IncludesCodeGeneration: true,
            IncludesIaC: true,
            IncludesHelmCharts: true,
            IncludesRunbook: true),

        _ => throw new InvalidTierException(
            $"Tier {tier} is not a valid tier. Valid tiers are 1, 2, 3."),
    };

    public bool ShouldTriggerCodeGeneration(int tier) => tier >= 2;
}
