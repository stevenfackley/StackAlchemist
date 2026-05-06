using System.IO.Abstractions.TestingHelpers;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Integration tests for the V1 (one-shot) generation pipeline path.
///
/// V2 (Swiss Cheese) coverage lives in <see cref="SwissCheeseEndToEndTests"/>; this file
/// owns the V1 LLM-returns-everything-at-once path that runs in test/prod today.
/// Wires the real <see cref="GenerationOrchestrator"/>, <see cref="ReconstructionService"/>,
/// and <see cref="TemplateProvider"/> against a <see cref="MockFileSystem"/> with
/// a stub <see cref="ILlmClient"/> emitting <c>[[FILE:path]]…[[END_FILE]]</c> blocks.
///
/// No Postgres / Testcontainers required: the Engine speaks to Supabase via PostgREST
/// HTTP and has no local DB on its hot path.
/// </summary>
public class GenerationPipelineTests
{
    private const string TemplatesRoot = "/templates";

    [Fact]
    public async Task V1_FullPipeline_WithValidSchema_ProducesReconstructedFiles()
    {
        var fs = BuildFileSystemWithV1DotNetTemplates();
        var (orchestrator, queue) = BuildV1Orchestrator(fs);

        var response = await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "v1-smoke-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities =
                [
                    new SchemaEntity
                    {
                        Name = "Product",
                        Fields =
                        [
                            new SchemaField { Name = "Id", Type = "uuid", Pk = true },
                            new SchemaField { Name = "Name", Type = "string" },
                            new SchemaField { Name = "Price", Type = "decimal" },
                        ],
                    },
                ],
            },
        });

        // Orchestrator transitions Pending → Generating → Building and enqueues the job.
        response.Status.Should().Be("building");
        response.JobId.Should().StartWith("v1-smoke-");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        ctx.Should().NotBeNull();
        ctx!.State.Should().Be(GenerationState.Building);
        ctx.OutputDirectory.Should().NotBeNullOrEmpty();

        var outputDir = ctx.OutputDirectory!;
        var emitted = fs.AllFiles
            .Where(p => p.StartsWith(outputDir, StringComparison.Ordinal))
            .Select(p => p.Replace('\\', '/'))
            .ToList();

        // V1 path emits the LLM-supplied files directly via reconstruction.
        emitted.Should().Contain(p => p.EndsWith("Models/Product.cs", StringComparison.Ordinal),
            "stub LLM returned a [[FILE:Models/Product.cs]] block");
        emitted.Should().Contain(p => p.EndsWith("Repositories/ProductRepository.cs", StringComparison.Ordinal));

        // Schema-wide template files (Program.cs, csproj) survive reconstruction unchanged.
        emitted.Should().Contain(p => p.EndsWith("Program.cs", StringComparison.Ordinal));

        // No raw [[FILE:…]] / [[END_FILE]] markers leak into emitted files.
        foreach (var path in emitted)
        {
            var content = fs.File.ReadAllText(path);
            content.Should().NotContain("[[FILE:", $"file {path} retained a [[FILE:]] marker");
            content.Should().NotContain("[[END_FILE]]", $"file {path} retained an [[END_FILE]] marker");
        }
    }

    [Fact]
    public async Task V1_FullPipeline_LlmTokenUsage_FlowsToDelivery()
    {
        var fs = BuildFileSystemWithV1DotNetTemplates();
        var delivery = Substitute.For<IDeliveryService>();
        var (orchestrator, _) = BuildV1Orchestrator(fs, delivery);

        await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "v1-tokens-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities =
                [
                    new SchemaEntity
                    {
                        Name = "Product",
                        Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }],
                    },
                ],
            },
        });

        // V1 path makes one LLM call; orchestrator should report the usage exactly once.
        await delivery.Received(1).UpdateTokenUsageAsync(
            Arg.Is<string>(id => id.StartsWith("v1-tokens-", StringComparison.Ordinal)),
            inputTokens: V1StubLlmClient.StubInputTokens,
            outputTokens: V1StubLlmClient.StubOutputTokens,
            model: V1StubLlmClient.StubModel,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tier1_SkipsCodeGeneration_EmitsBlueprintArtifacts()
    {
        var fs = BuildFileSystemWithV1DotNetTemplates();
        var llmClient = new V1StubLlmClient();
        var delivery = Substitute.For<IDeliveryService>();
        var (orchestrator, queue) = BuildV1Orchestrator(fs, delivery, llmClient);

        var response = await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "tier1-blueprint-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 1,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities =
                [
                    new SchemaEntity
                    {
                        Name = "Product",
                        Fields =
                        [
                            new SchemaField { Name = "Id", Type = "uuid", Pk = true },
                            new SchemaField { Name = "Name", Type = "string" },
                            new SchemaField { Name = "Price", Type = "decimal" },
                        ],
                    },
                    new SchemaEntity
                    {
                        Name = "Order",
                        Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }],
                    },
                ],
            },
        });

        // Tier 1 routes Generating → Packing directly, skipping the build loop.
        response.Status.Should().Be("packing");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        ctx.Should().NotBeNull();
        ctx!.State.Should().Be(GenerationState.Packing);

        // CRITICAL: Tier 1 must NOT trigger an LLM call — that would be a billing leak
        // (Tier 1 is $299 Blueprint, codegen is paid Anthropic tokens we'd lose money on).
        llmClient.CallCount.Should().Be(0);
        await delivery.DidNotReceive().UpdateTokenUsageAsync(
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Output contains the two Blueprint deliverables.
        var outputDir = ctx.OutputDirectory!;
        var emitted = fs.AllFiles
            .Where(p => p.StartsWith(outputDir, StringComparison.Ordinal))
            .Select(p => p.Replace('\\', '/'))
            .ToList();

        emitted.Should().Contain(p => p.EndsWith("/schema.json", StringComparison.Ordinal));
        emitted.Should().Contain(p => p.EndsWith("/api-docs.md", StringComparison.Ordinal));

        // CRITICAL: no codegen output — no .cs / .ts / .tsx files.
        emitted.Where(p => p.EndsWith(".cs", StringComparison.Ordinal)
                        || p.EndsWith(".tsx", StringComparison.Ordinal)
                        || p.EndsWith(".ts", StringComparison.Ordinal))
            .Should().BeEmpty("Tier 1 deliberately skips code generation");

        // schema.json round-trips both entities.
        var schemaPath = emitted.First(p => p.EndsWith("/schema.json", StringComparison.Ordinal));
        var schemaContent = fs.File.ReadAllText(schemaPath.Replace('/', Path.DirectorySeparatorChar));
        schemaContent.Should().Contain("\"Product\"").And.Contain("\"Order\"");

        // api-docs.md contains CRUD endpoints for each entity.
        var docsPath = emitted.First(p => p.EndsWith("/api-docs.md", StringComparison.Ordinal));
        var docsContent = fs.File.ReadAllText(docsPath.Replace('/', Path.DirectorySeparatorChar));
        docsContent.Should().Contain("## Product").And.Contain("## Order");
        docsContent.Should().Contain("GET /api/v1/products").And.Contain("GET /api/v1/orders");
        docsContent.Should().Contain("POST /api/v1/products");
        docsContent.Should().Contain("DELETE /api/v1/orders/{id}");
    }

    // Compile-retry coverage (build-failure-retry, max-retries-exhausted) lives in
    // CompileWorkerRetryTests.cs since it drives CompileWorkerService directly,
    // not the orchestrator.

    [Fact]
    public async Task Tier3_FullPipeline_AppendsIaCAndHelmAndRunbook()
    {
        // V1 templates + Tier3-Infrastructure templates are both required: the orchestrator
        // first renders the chosen project type's templates, then layers Tier 3 infra
        // files on top via AppendTier3InfrastructureFiles.
        var fs = BuildFileSystemWithV1DotNetTemplates();
        AddTier3InfrastructureTemplates(fs);

        var (orchestrator, queue) = BuildV1Orchestrator(fs);

        var response = await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "tier3-iac-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 3,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities =
                [
                    new SchemaEntity
                    {
                        Name = "Order",
                        Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }],
                    },
                ],
            },
            Personalization = new GenerationPersonalization { ProjectName = "TaskManager" },
        });

        response.Status.Should().Be("building");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        var outputDir = ctx!.OutputDirectory!;
        var emitted = fs.AllFiles
            .Where(p => p.StartsWith(outputDir, StringComparison.Ordinal))
            .Select(p => p.Replace('\\', '/'))
            .ToList();

        // Tier 3 still includes the codegen output that Tier 2 produces.
        emitted.Should().Contain(p => p.EndsWith("Models/Product.cs", StringComparison.Ordinal),
            "Tier 3 layers infra ON TOP of Tier 2 codegen, not instead of it");

        // Runbook present.
        emitted.Should().Contain(p => p.EndsWith("/DEPLOYMENT.md", StringComparison.Ordinal));

        // CDK assets present.
        emitted.Should().Contain(p => p.EndsWith("infra/cdk/bin/app.ts", StringComparison.Ordinal));
        emitted.Should().Contain(p => p.EndsWith("infra/cdk/lib/task-manager-stack.ts", StringComparison.Ordinal),
            "stack file path should be rendered with the kebab-cased project name");

        // Helm chart with Chart.yaml + values + at least one template manifest.
        emitted.Should().Contain(p => p.EndsWith("infra/helm/Chart.yaml", StringComparison.Ordinal));
        emitted.Should().Contain(p => p.EndsWith("infra/helm/values.yaml", StringComparison.Ordinal));
        emitted.Should().Contain(p => p.EndsWith("infra/helm/templates/deployment.yaml", StringComparison.Ordinal));

        // Terraform.
        emitted.Should().Contain(p => p.EndsWith("infra/terraform/main.tf", StringComparison.Ordinal));

        // Helm Chart.yaml interpolates the project name (kebab-case).
        var chartPath = emitted.First(p => p.EndsWith("infra/helm/Chart.yaml", StringComparison.Ordinal));
        var chartContent = fs.File.ReadAllText(chartPath.Replace('/', Path.DirectorySeparatorChar));
        chartContent.Should().Contain("name: task-manager");

        // DEPLOYMENT.md interpolates the project name (PascalCase).
        var runbookPath = emitted.First(p => p.EndsWith("/DEPLOYMENT.md", StringComparison.Ordinal));
        var runbookContent = fs.File.ReadAllText(runbookPath.Replace('/', Path.DirectorySeparatorChar));
        runbookContent.Should().Contain("TaskManager");
    }

    [Fact]
    public async Task Tier2_DoesNotAppendTier3InfrastructureFiles()
    {
        // Regression guard: AppendTier3InfrastructureFiles must early-return for Tier != 3.
        var fs = BuildFileSystemWithV1DotNetTemplates();
        AddTier3InfrastructureTemplates(fs);
        var (orchestrator, queue) = BuildV1Orchestrator(fs);

        await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "tier2-no-iac-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities = [new SchemaEntity { Name = "Order", Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }] }],
            },
        });

        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        var outputDir = ctx!.OutputDirectory!;
        var emitted = fs.AllFiles
            .Where(p => p.StartsWith(outputDir, StringComparison.Ordinal))
            .Select(p => p.Replace('\\', '/'))
            .ToList();

        emitted.Should().NotContain(p => p.Contains("infra/cdk/", StringComparison.Ordinal));
        emitted.Should().NotContain(p => p.Contains("infra/helm/", StringComparison.Ordinal));
        emitted.Should().NotContain(p => p.Contains("infra/terraform/", StringComparison.Ordinal));
        emitted.Should().NotContain(p => p.EndsWith("/DEPLOYMENT.md", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adds a representative subset of the Tier3-Infrastructure templates to the
    /// mock file system: DEPLOYMENT.md (uses {{ProjectName}}), CDK app + stack
    /// (latter uses {{ProjectNameKebab}} in its filename), Helm chart + values +
    /// one templates/deployment.yaml manifest, and a Terraform main.tf.
    /// Keeps test fast without enumerating every real Tier 3 template.
    /// </summary>
    private static void AddTier3InfrastructureTemplates(MockFileSystem fs)
    {
        const string root = TemplatesRoot + "/Tier3-Infrastructure";
        // Note: paths are NOT interpolated strings — using $"{{...}}" would escape
        // the braces and the template renderer would never see the Handlebars tokens.
        const string kebabToken = "{{ProjectNameKebab}}";

        fs.AddDirectory(root);

        fs.AddFile(root + "/DEPLOYMENT.md",
            new MockFileData("# {{ProjectName}} Deployment Runbook\n"));
        fs.AddFile(root + "/infra/cdk/bin/app.ts",
            new MockFileData("// CDK app entrypoint for {{ProjectName}}\n"));
        fs.AddFile(root + "/infra/cdk/lib/" + kebabToken + "-stack.ts",
            new MockFileData("// CDK stack for {{ProjectName}}\n"));
        fs.AddFile(root + "/infra/helm/Chart.yaml",
            new MockFileData("apiVersion: v2\nname: " + kebabToken + "\nversion: 0.1.0\n"));
        fs.AddFile(root + "/infra/helm/values.yaml",
            new MockFileData("image:\n  repository: " + kebabToken + "\n"));
        fs.AddFile(root + "/infra/helm/templates/deployment.yaml",
            new MockFileData("apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: " + kebabToken + "\n"));
        fs.AddFile(root + "/infra/terraform/main.tf",
            new MockFileData("terraform { required_version = \">= 1.5\" }\n"));
    }


    // ── Wiring ────────────────────────────────────────────────────────────────

    private static (GenerationOrchestrator Sut, Channel<GenerationContext> Queue) BuildV1Orchestrator(
        MockFileSystem fs,
        IDeliveryService? delivery = null,
        V1StubLlmClient? llmClient = null)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        var templateProvider = new TemplateProvider(fs, TemplatesRoot);
        var promptBuilder = new PromptBuilderService();
        llmClient ??= new V1StubLlmClient();
        var reconstruction = new ReconstructionService();

        // V2-only InjectionEngine is wired but not invoked when UseSwissCheese=false.
        var injectionEngine = new InjectionEngine(
            templateProvider,
            promptBuilder,
            llmClient,
            NullLogger<InjectionEngine>.Instance,
            new InjectionEngineOptions { MaxConcurrency = 1, MaxAttemptsPerZone = 1 });

        var sut = new GenerationOrchestrator(
            templateProvider,
            reconstruction,
            llmClient,
            promptBuilder,
            injectionEngine,
            delivery ?? Substitute.For<IDeliveryService>(),
            new TierGatingService(),
            fs,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Generation:UseSwissCheese"] = "false",
                })
                .Build(),
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        return (sut, queue);
    }

    private static MockFileSystem BuildFileSystemWithV1DotNetTemplates()
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(TemplatesRoot);
        fs.AddDirectory($"{TemplatesRoot}/V1-DotNet-NextJs");

        // Schema-wide scaffold that survives reconstruction unchanged.
        fs.AddFile($"{TemplatesRoot}/V1-DotNet-NextJs/dotnet/Program.cs",
            new MockFileData("namespace {{ProjectName}};\n// app entry — preserved verbatim by V1 reconstruction\n"));

        // Placeholder dirs that V1 LLM output replaces via [[FILE:…]] blocks.
        fs.AddFile($"{TemplatesRoot}/V1-DotNet-NextJs/dotnet/Models/_placeholder.cs",
            new MockFileData("// will be replaced by LLM-generated entity records\n"));
        fs.AddFile($"{TemplatesRoot}/V1-DotNet-NextJs/dotnet/Repositories/_placeholder.cs",
            new MockFileData("// will be replaced by LLM-generated repositories\n"));

        return fs;
    }

    /// <summary>
    /// Returns a deterministic V1 one-shot LLM response with two <c>[[FILE:…]]</c>
    /// blocks — a Models/Product.cs record and a Repositories/ProductRepository.cs class.
    /// Independent of <c>userPrompt</c> contents so the orchestrator's prompt construction
    /// is not asserted (covered separately by <c>PromptBuilderTests</c>).
    /// </summary>
    private sealed class V1StubLlmClient : ILlmClient
    {
        public const int StubInputTokens = 120;
        public const int StubOutputTokens = 240;
        public const string StubModel = "v1-stub";

        private int _callCount;
        public int CallCount => _callCount;

        public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            const string body = """
                [[FILE:Models/Product.cs]]
                namespace GeneratedApp.Models;
                public record Product(Guid Id, string Name, decimal Price);
                [[END_FILE]]
                [[FILE:Repositories/ProductRepository.cs]]
                namespace GeneratedApp.Repositories;
                public sealed class ProductRepository
                {
                    public Task<Product?> GetByIdAsync(Guid id) => Task.FromResult<Product?>(null);
                }
                [[END_FILE]]
                """;
            return Task.FromResult(new LlmResponse(body, StubInputTokens, StubOutputTokens, StubModel));
        }
    }
}
