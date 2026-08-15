using System.IO.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Renders what a <b>Tier 3</b> buyer actually downloads: the V1 application tree with the
/// real <c>Tier3-Infrastructure</c> set layered on top, produced by the same two calls
/// <c>GenerationOrchestrator.AppendTier3InfrastructureFiles</c> makes —
/// <see cref="ITemplateProvider.LoadTemplate"/> then <see cref="ITemplateProvider.Render"/>,
/// overwriting into the file map — with the same <see cref="TemplateProvider"/> instance and
/// the same <see cref="V1TemplateHarness.SampleVariables"/>.
///
/// It exists because every existing test of the Tier-3 append runs against a
/// <c>MockFileSystem</c> seeded with synthetic inline files. That is why nobody noticed that
/// <c>infra/cdk/bin/app.ts</c> — the CDK entrypoint the whole app is unusable without — was
/// never in the repository at all: the mocks always wrote one themselves.
/// </summary>
internal static class Tier3TemplateHarness
{
    /// <summary>Mirrors <c>GenerationOrchestrator.Tier3InfrastructureTemplateSet</c>.</summary>
    public const string TemplateSetName = "Tier3-Infrastructure";

    /// <summary>Directory of the CDK app inside the rendered tree, relative to its root.</summary>
    public const string CdkDirectory = "infra/cdk";

    /// <summary>Directory of the Terraform baseline inside the rendered tree.</summary>
    public const string TerraformDirectory = "infra/terraform";

    /// <summary>Directory of the Helm chart inside the rendered tree.</summary>
    public const string HelmDirectory = "infra/helm";

    /// <summary>
    /// The CloudFormation stack id <c>bin/app.ts</c> constructs, for the rendered project name.
    /// Asserting on it is what makes "synth exited 0" mean "the entrypoint instantiated the
    /// stack" rather than "the CLI printed help and left".
    /// </summary>
    public const string ExpectedStackName = "InvoiceHubStack";

    /// <summary>
    /// A stand-in image reference for <c>cdk synth</c>. The stack throws without the
    /// <c>imageUri</c> context value on purpose — it must never synthesise a deployable
    /// template pointing at a placeholder — so the gate has to supply one, exactly as the
    /// runbook tells the customer to.
    /// </summary>
    public const string SynthImageUri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/invoice-hub:ci";

    /// <summary>
    /// Renders the full Tier-3 deliverable to <paramref name="outputDirectory"/> and returns
    /// the written relative paths, ordered.
    /// </summary>
    public static IReadOnlyList<string> RenderTo(string outputDirectory)
    {
        var provider = new TemplateProvider(new FileSystem(), V1TemplateHarness.ResolveTemplatesRoot());
        var reconstruction = new ReconstructionService();
        var variables = V1TemplateHarness.SampleVariables();

        // The Tier-2 baseline: the same load → render → reconstruct the paid path runs.
        var rendered = provider.Render(provider.LoadTemplate(V1TemplateHarness.TemplateSetName), variables);
        var files = reconstruction.Reconstruct(rendered, [], provider);

        // …then the Tier-3 overlay, byte-for-byte the orchestrator's append.
        foreach (var (path, content) in provider.Render(provider.LoadTemplate(TemplateSetName), variables))
        {
            files[path] = content;
        }

        Directory.CreateDirectory(outputDirectory);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return [.. files.Keys.OrderBy(p => p, StringComparer.Ordinal)];
    }
}
