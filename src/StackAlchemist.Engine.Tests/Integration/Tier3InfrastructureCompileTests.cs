using System.Text.Json;
using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// THE gate for the Tier-3 ($999) deliverable — the counterpart of
/// <see cref="V1TemplateCompileTests"/> for everything Tier 3 adds on top of Tier 2.
///
/// Tier 3's promise is <c>infra/</c>: a CDK app, a Terraform baseline and a Helm chart. Every
/// existing test of that overlay ran against a <c>MockFileSystem</c> seeded with synthetic
/// inline files — <c>GenerationPipelineTests</c> writes its own <c>infra/cdk/bin/app.ts</c>
/// before asserting one comes out. So the suite proved the orchestrator copies files it was
/// handed, and proved nothing whatsoever about the files we actually ship. Two defects lived
/// in that gap, and both were total:
///
/// <list type="number">
/// <item>
/// <c>infra/cdk/bin/app.ts</c> was not in the repository. The root <c>.gitignore</c>'s .NET
/// <c>[Bb]in/</c> rule swallowed the directory, so the entrypoint existed only as an untracked
/// file on one machine. Every clone — and every archive built from one — shipped a CDK app the
/// CLI cannot find an app in.
/// </item>
/// <item>
/// Rendering the set threw. Helm charts are Go <c>text/template</c> files sharing Handlebars'
/// <c>{{ }}</c> delimiters, and <c>{{- toYaml .Values.resources | nindent 12 }}</c> parses as a
/// Handlebars block-params declaration: <c>HandlebarsCompilerException</c>, straight out of
/// <c>AppendTier3InfrastructureFiles</c> into the orchestrator's catch. Tier 3 could not
/// produce an archive at all.
/// </item>
/// </list>
///
/// So this renders the real tree the real way (<see cref="Tier3TemplateHarness"/>) and then
/// runs the customer's own commands over the result.
/// </summary>
public sealed class Tier3InfrastructureCompileTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "sa-tier3-infra-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort: node_modules on Windows resists deletion */ }
    }

    /// <summary>
    /// The render itself is an assertion. Before the <see cref="ForeignTemplatePaths"/>
    /// exemption this threw on the Helm chart, which is what made Tier 3 undeliverable — so a
    /// test that merely inspects the output would never have run far enough to inspect
    /// anything.
    ///
    /// Also the entrypoint gate: <c>bin/app.ts</c> is what <c>cdk.json</c> names, and without
    /// it every downstream CDK command fails on a tree the customer paid $999 for.
    /// </summary>
    [Fact]
    public void Render_ProducesACompleteCdkAppAndAnIntactHelmChart()
    {
        var files = Tier3TemplateHarness.RenderTo(_tempDir);

        files.Should().Contain("infra/cdk/bin/app.ts",
            "the CDK entrypoint is the file `cdk.json` points at — without it `cdk synth`, "
            + "`cdk deploy` and every other command fail on a tree the customer paid for");
        files.Should().Contain("infra/cdk/cdk.json",
            "without cdk.json the CLI has no --app and refuses to run");
        files.Should().Contain("infra/cdk/lib/invoice-hub-stack.ts",
            "the stack filename token must be substituted, not left as {{ProjectNameKebab}}");
        files.Should().Contain("infra/cdk/package-lock.json",
            "`npm ci` — what the runbook tells the customer to run — requires a lockfile");

        // The Helm chart must arrive as a Helm chart: Go template syntax intact, because
        // nothing is allowed to have "rendered" it on the way out.
        var deployment = File.ReadAllText(
            Path.Combine(_tempDir, "infra/helm/templates/deployment.yaml"));
        deployment.Should().Contain("{{ .Values.replicaCount }}")
                  .And.Contain("| nindent",
                      "Helm's own template syntax must reach the customer untouched — a "
                      + "Handlebars pass over it does not mangle the chart, it throws");
    }

    /// <summary>
    /// No file in the Tier-3 deliverable may carry one of OUR tokens.
    ///
    /// This is the specific hazard the <see cref="ForeignTemplatePaths"/> exemption creates:
    /// exempt files are passed through verbatim, so a <c>{{ProjectName}}</c> left inside one
    /// ships literally to the buyer. Asserting "no <c>{{</c>" the way the V1 gate does is not
    /// available here — the chart is legitimately full of <c>{{ .Values… }}</c> — so the
    /// assertion names our tokens instead.
    /// </summary>
    [Fact]
    public void RenderedTree_CarriesNoneOfOurUnsubstitutedTokens()
    {
        string[] ourTokens =
        [
            "{{ProjectName", "{{Entity", "{{Field", "{{Table",
            "{{FrontendUrl", "{{DbConnectionString", "{{#each", "{{#if",
        ];

        var files = Tier3TemplateHarness.RenderTo(_tempDir);

        foreach (var relativePath in files.Where(p => p.StartsWith("infra/", StringComparison.Ordinal)
                                                   || p.EndsWith("DEPLOYMENT.md", StringComparison.Ordinal)))
        {
            var content = File.ReadAllText(Path.Combine(_tempDir, relativePath));
            foreach (var token in ourTokens)
            {
                content.Should().NotContain(token,
                    $"{relativePath} would ship an unsubstituted StackAlchemist token to the customer");
            }
        }
    }

    /// <summary>
    /// The gate proper: the customer's documented CDK workflow, run against the rendered tree.
    ///
    /// <c>npm ci</c> then <c>npm run synth</c>, exactly as <c>DEPLOYMENT.md</c> instructs, with
    /// AWS credentials deliberately stripped from the child environment. Credential-free synth
    /// is a promise the runbook makes, and it is the only version of this test that can run on
    /// a CI runner anyway.
    /// </summary>
    [Fact]
    public async Task CdkApp_InstallsAndSynthesisesTheStack()
    {
        if (!IntegrationToolchain.Available(ProcessCommandResolver.Npm, "--version", "npm", requiredOnCi: true))
            return;

        Tier3TemplateHarness.RenderTo(_tempDir);
        var cdkDir = Path.Combine(_tempDir, "infra", "cdk");

        var (installExit, installLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm, "ci --no-audit --no-fund", cdkDir, NpmTimeout);

        installExit.Should().Be(0,
            $"`npm ci` is the first command the Tier-3 runbook gives the customer.\n\n{IntegrationToolchain.Tail(installLog)}");

        var (synthExit, synthLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm,
            $"run synth -- --context imageUri={Tier3TemplateHarness.SynthImageUri}",
            cdkDir,
            SynthTimeout,
            CredentialFreeEnvironment);

        synthExit.Should().Be(0,
            "`cdk synth` is the customer's first real use of the infrastructure they paid for. "
            + "It failed for as long as bin/app.ts was missing from the repository.\n\n"
            + IntegrationToolchain.Tail(synthLog));

        // Exit 0 alone would also describe a CLI that printed help and stopped. The synthesised
        // CloudFormation template is the artefact, so assert the artefact.
        var templatePath = Path.Combine(
            cdkDir, "cdk.out", $"{Tier3TemplateHarness.ExpectedStackName}.template.json");

        File.Exists(templatePath).Should().BeTrue(
            $"synth must emit cdk.out/{Tier3TemplateHarness.ExpectedStackName}.template.json — "
            + $"that name is proof bin/app.ts really instantiated the stack.\n\n{IntegrationToolchain.Tail(synthLog)}");

        using var template = JsonDocument.Parse(File.ReadAllText(templatePath));
        var resourceTypes = template.RootElement
            .GetProperty("Resources")
            .EnumerateObject()
            .Select(r => r.Value.GetProperty("Type").GetString())
            .ToList();

        resourceTypes.Should().Contain("AWS::ECS::Service")
                     .And.Contain("AWS::RDS::DBInstance")
                     .And.Contain("AWS::ElasticLoadBalancingV2::LoadBalancer",
                         "the synthesised template must contain the stack DEPLOYMENT.md "
                         + "advertises — ECS Fargate behind an ALB with a PostgreSQL instance");
    }

    /// <summary>
    /// The Helm half. <c>helm lint</c> parses and renders every chart template, so it is the
    /// cheapest command that proves the chart survived generation as a working chart rather
    /// than as YAML-shaped text.
    ///
    /// Required on CI: ubuntu-latest ships Helm 3.x.
    /// </summary>
    [Fact]
    public async Task HelmChart_Lints()
    {
        if (!IntegrationToolchain.Available("helm", "version", "Helm", requiredOnCi: true))
            return;

        Tier3TemplateHarness.RenderTo(_tempDir);

        var (exitCode, log) = await IntegrationToolchain.RunAsync(
            "helm", "lint .", Path.Combine(_tempDir, "infra", "helm"), HelmTimeout);

        exitCode.Should().Be(0,
            $"the Tier-3 chart must lint as shipped.\n\n{IntegrationToolchain.Tail(log)}");
    }

    /// <summary>
    /// The Terraform half.
    ///
    /// Skips cleanly when Terraform is absent — <b>including on CI</b>, and that is not
    /// laziness: the GitHub-hosted ubuntu-latest image no longer ships Terraform, and
    /// <c>terraform validate</c> requires an <c>init</c> that downloads the ~600 MB AWS
    /// provider. So this runs on a developer machine, where it is nearly free, and the CI job
    /// leans on the CDK and Helm gates above. Install Terraform in the backend job if the
    /// baseline ever starts changing often enough to need a machine watching it.
    /// </summary>
    [Fact]
    public async Task TerraformBaseline_Validates()
    {
        if (!IntegrationToolchain.Available("terraform", "version", "Terraform", requiredOnCi: false))
            return;

        Tier3TemplateHarness.RenderTo(_tempDir);
        var terraformDir = Path.Combine(_tempDir, "infra", "terraform");

        var (initExit, initLog) = await IntegrationToolchain.RunAsync(
            "terraform", "init -backend=false -input=false -no-color", terraformDir, TerraformTimeout);

        initExit.Should().Be(0,
            $"`terraform init` must succeed on the shipped baseline.\n\n{IntegrationToolchain.Tail(initLog)}");

        var (validateExit, validateLog) = await IntegrationToolchain.RunAsync(
            "terraform", "validate -no-color", terraformDir, TerraformTimeout);

        validateExit.Should().Be(0,
            $"`terraform validate` must pass on the shipped baseline.\n\n{IntegrationToolchain.Tail(validateLog)}");
    }

    /// <summary>Cold npm registry fetch of the CDK toolchain.</summary>
    private static readonly TimeSpan NpmTimeout = TimeSpan.FromMinutes(10);

    /// <summary>ts-node compile plus a full construct-tree synthesis.</summary>
    private static readonly TimeSpan SynthTimeout = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan HelmTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Generous: `init` downloads the AWS provider on a cold cache.</summary>
    private static readonly TimeSpan TerraformTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Strips every AWS credential source from the child process, so the synth this gate runs
    /// is the credential-free one DEPLOYMENT.md promises rather than one that quietly depended
    /// on whatever profile the developer happened to have exported. Pointing the config files
    /// at a non-existent path defeats <c>~/.aws/credentials</c>; disabling IMDS stops the SDK
    /// from stalling on the instance metadata endpoint when this runs on EC2.
    /// </summary>
    private static readonly Dictionary<string, string?> CredentialFreeEnvironment = new()
    {
        ["AWS_ACCESS_KEY_ID"] = null,
        ["AWS_SECRET_ACCESS_KEY"] = null,
        ["AWS_SESSION_TOKEN"] = null,
        ["AWS_PROFILE"] = null,
        ["AWS_REGION"] = null,
        ["AWS_DEFAULT_REGION"] = null,
        ["CDK_DEFAULT_ACCOUNT"] = null,
        ["CDK_DEFAULT_REGION"] = null,
        ["AWS_SHARED_CREDENTIALS_FILE"] = "/nonexistent/sa-gate/credentials",
        ["AWS_CONFIG_FILE"] = "/nonexistent/sa-gate/config",
        ["AWS_EC2_METADATA_DISABLED"] = "true",
    };
}
