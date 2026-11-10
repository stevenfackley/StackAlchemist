using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>What a buyer is charged for a paid tier and what the Stripe checkout page tells them they get.</summary>
public sealed record PaidTier(long Cents, string Name, string Description);

/// <summary>
/// Prices and checkout wording for the paid tiers (1 to 3). The description is the line a buyer
/// reads on Stripe's payment page, so it names only what that tier delivers: tier 1 is
/// <c>schema.json</c> and <c>api-docs.md</c> (<see cref="Tier1ArtifactBuilder"/>), tier 2 is the
/// compiled source for the stack they chose, tier 3 adds the <c>Tier3-Infrastructure</c> templates.
/// The web app shows the same prices (pricing page, tier step, upgrade panel); change them together.
/// </summary>
public static class TierCatalog
{
    public static bool TryGetPaidTier(int tier, ProjectType projectType, out PaidTier paidTier)
    {
        var stack = StackName(projectType);
        PaidTier? found = tier switch
        {
            1 => new(29_900, "StackAlchemist Blueprint",
                "schema.json and api-docs.md: your data model and CRUD API contract. No code."),
            2 => new(59_900, "StackAlchemist Boilerplate",
                $"Full {stack} source code, built before delivery (Compile Guarantee)"),
            3 => new(99_900, "StackAlchemist Infrastructure",
                $"{stack} source code plus AWS CDK, Terraform, a Helm chart and a deployment guide"),
            _ => null,
        };

        paidTier = found!;
        return found is not null;
    }

    private static string StackName(ProjectType projectType) => projectType switch
    {
        ProjectType.PythonReact => "FastAPI + React",
        _ => ".NET + Next.js",
    };
}
