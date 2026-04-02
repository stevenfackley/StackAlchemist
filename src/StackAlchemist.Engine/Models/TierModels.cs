namespace StackAlchemist.Engine.Models;

/// <summary>
/// Describes which deliverables are unlocked for a purchased tier.
/// </summary>
/// <param name="IncludesSchema">Schema JSON + ERD diagram — all tiers.</param>
/// <param name="IncludesApiDocs">Generated OpenAPI / Markdown docs — all tiers.</param>
/// <param name="IncludesCodeGeneration">Full compilable codebase zip — Tier 2+.</param>
/// <param name="IncludesIaC">AWS CDK + Terraform provider scripts — Tier 3 only.</param>
/// <param name="IncludesHelmCharts">Kubernetes Helm chart — Tier 3 only.</param>
/// <param name="IncludesRunbook">Markdown deployment runbook — Tier 3 only.</param>
public sealed record TierDeliverables(
    bool IncludesSchema,
    bool IncludesApiDocs,
    bool IncludesCodeGeneration,
    bool IncludesIaC,
    bool IncludesHelmCharts,
    bool IncludesRunbook);
