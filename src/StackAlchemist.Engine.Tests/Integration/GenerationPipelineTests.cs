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

    // ── Stubs for tests #2-#5 land in subsequent PRs ──────────────────────────
    // PR 2: Tier1_SkipsCodeGeneration (requires wiring ITierGatingService into orchestrator)
    // PR 3: WithBuildFailure_RetriesSuccessfully + MaxRetriesExceeded_MarksAsFailed
    // PR 4: Tier3_IncludesIaCAndHelmCharts

    // ── Wiring ────────────────────────────────────────────────────────────────

    private static (GenerationOrchestrator Sut, Channel<GenerationContext> Queue) BuildV1Orchestrator(
        MockFileSystem fs,
        IDeliveryService? delivery = null)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        var templateProvider = new TemplateProvider(fs, TemplatesRoot);
        var promptBuilder = new PromptBuilderService();
        var llmClient = new V1StubLlmClient();
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

        public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
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
