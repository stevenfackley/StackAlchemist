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
/// End-to-end smoke test for the V2 Swiss Cheese path.
///
/// Wires up the real TemplateProvider, real PromptBuilderService, real
/// InjectionEngine, and a stub ILlmClient that returns realistic per-zone
/// content. Verifies that with UseSwissCheese=true and a multi-entity schema:
///   1. The orchestrator loads V2 templates and renders per-entity files.
///   2. Every [[LLM_INJECTION_START/END]] zone is filled.
///   3. No marker lines remain in the final output.
///   4. Per-entity files exist for each schema entity.
/// </summary>
public class SwissCheeseEndToEndTests
{
    private const string TemplatesRoot = "/templates";

    [Fact]
    public async Task V2DotNetNextJs_WithTwoEntities_ProducesAllFiledFilesWithMarkersStripped()
    {
        var fs = BuildFileSystemWithV2DotNetTemplates();
        var (orchestrator, queue) = BuildSwissCheeseOrchestrator(fs);

        var response = await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "smoke-dotnet-" + Guid.NewGuid(),
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
                    new SchemaEntity
                    {
                        Name = "Order",
                        Fields =
                        [
                            new SchemaField { Name = "Id", Type = "uuid", Pk = true },
                            new SchemaField { Name = "Total", Type = "decimal" },
                        ],
                    },
                ],
            },
        });

        response.Status.Should().Be("building");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        ctx.Should().NotBeNull();

        var outputDir = ctx!.OutputDirectory!;
        var emitted = fs.AllFiles.Where(p => p.StartsWith(outputDir, StringComparison.Ordinal)).ToList();

        // Per-entity files: ProductRepository.cs, OrderRepository.cs, etc.
        emitted.Should().Contain(p => p.EndsWith("ProductRepository.cs", StringComparison.Ordinal));
        emitted.Should().Contain(p => p.EndsWith("OrderRepository.cs", StringComparison.Ordinal));

        // Schema-wide migration is a single file, not per-entity.
        emitted.Should().Contain(p => p.EndsWith("001_initial_schema.sql", StringComparison.Ordinal));

        // Critical invariant: NO marker lines should survive in any emitted file.
        foreach (var path in emitted)
        {
            var content = fs.File.ReadAllText(path);
            content.Should().NotContain("[[LLM_INJECTION_START",
                $"file {path} still has START marker — marker stripping failed");
            content.Should().NotContain("[[LLM_INJECTION_END",
                $"file {path} still has END marker — marker stripping failed");
        }

        // Per-entity repository should have the stub-generated content where zones used to be.
        var productRepo = fs.File.ReadAllText(emitted.First(p => p.EndsWith("ProductRepository.cs", StringComparison.Ordinal)));
        productRepo.Should().Contain("STUB:GetAllImpl-for-Product");
        productRepo.Should().Contain("STUB:CreateImpl-for-Product");
    }

    [Fact]
    public async Task V2_WithEmptySchema_StillRendersSchemaWideFiles()
    {
        var fs = BuildFileSystemWithV2DotNetTemplates();
        var (orchestrator, queue) = BuildSwissCheeseOrchestrator(fs);

        var response = await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = "smoke-empty-" + Guid.NewGuid(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema { Entities = [] },
        });

        response.Status.Should().Be("building");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        var outputDir = ctx!.OutputDirectory!;

        // Schema-wide files should still be produced.
        fs.AllFiles.Should().Contain(p => p.StartsWith(outputDir, StringComparison.Ordinal) && p.EndsWith("Program.cs", StringComparison.Ordinal));
        fs.AllFiles.Should().Contain(p => p.StartsWith(outputDir, StringComparison.Ordinal) && p.EndsWith("001_initial_schema.sql", StringComparison.Ordinal));

        // Per-entity files should NOT be produced when there are no entities.
        fs.AllFiles.Where(p => p.StartsWith(outputDir, StringComparison.Ordinal))
            .Should().NotContain(p => p.EndsWith("Repository.cs", StringComparison.Ordinal) && !p.EndsWith("IRepository.cs", StringComparison.Ordinal));
    }

    // ── Wiring helpers ────────────────────────────────────────────────────────

    private static (GenerationOrchestrator Sut, Channel<GenerationContext> Queue) BuildSwissCheeseOrchestrator(MockFileSystem fs)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        var templateProvider = new TemplateProvider(fs, TemplatesRoot);
        var promptBuilder = new PromptBuilderService();
        var llmClient = new ZoneStubLlmClient();
        var injectionEngine = new InjectionEngine(
            templateProvider,
            promptBuilder,
            llmClient,
            NullLogger<InjectionEngine>.Instance,
            new InjectionEngineOptions { MaxConcurrency = 4, MaxAttemptsPerZone = 2 });

        var delivery = Substitute.For<IDeliveryService>();
        var reconstruction = Substitute.For<IReconstructionService>();

        var sut = new GenerationOrchestrator(
            templateProvider,
            reconstruction,
            llmClient,
            promptBuilder,
            injectionEngine,
            delivery,
            fs,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Generation:UseSwissCheese"] = "true" })
                .Build(),
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        return (sut, queue);
    }

    private static MockFileSystem BuildFileSystemWithV2DotNetTemplates()
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(TemplatesRoot);
        fs.AddDirectory($"{TemplatesRoot}/V2-DotNet-NextJs");

        // Schema-wide file: Program.cs (no LLM zones, just {{#each Entities}}).
        fs.AddFile($"{TemplatesRoot}/V2-DotNet-NextJs/dotnet/Program.cs",
            new MockFileData("namespace {{ProjectName}}; // app entry\n"));

        // Schema-wide file: migration with one LLM zone.
        fs.AddFile($"{TemplatesRoot}/V2-DotNet-NextJs/dotnet/Migrations/001_initial_schema.sql",
            new MockFileData("CREATE EXTENSION uuid_ossp;\n[[LLM_INJECTION_START: ForeignKeyConstraints]]\n[[LLM_INJECTION_END: ForeignKeyConstraints]]\n"));

        // Per-entity file: Repository with two zones.
        fs.AddFile(
            $"{TemplatesRoot}/V2-DotNet-NextJs/dotnet/Repositories/{{{{EntityName}}}}Repository.cs",
            new MockFileData(
                "namespace {{ProjectName}}.Repositories;\n" +
                "public class {{EntityName}}Repository\n" +
                "{\n" +
                "    public IEnumerable<{{EntityName}}> GetAll()\n" +
                "    {\n" +
                "        [[LLM_INJECTION_START: GetAllImpl]]\n" +
                "        [[LLM_INJECTION_END: GetAllImpl]]\n" +
                "    }\n" +
                "    public {{EntityName}} Create({{EntityName}} input)\n" +
                "    {\n" +
                "        [[LLM_INJECTION_START: CreateImpl]]\n" +
                "        [[LLM_INJECTION_END: CreateImpl]]\n" +
                "    }\n" +
                "}\n"));

        // Per-entity file: pure-template Models (no LLM zones).
        fs.AddFile(
            $"{TemplatesRoot}/V2-DotNet-NextJs/dotnet/Models/{{{{EntityName}}}}.cs",
            new MockFileData("public record {{EntityName}}(Guid Id);\n"));

        return fs;
    }

    /// <summary>
    /// Stub ILlmClient that emits identifiable per-zone content so tests can verify
    /// the right zone got the right LLM output. Reads the zone name and (optional)
    /// "Entity:" line from the prompt to construct a unique stub string.
    /// </summary>
    private sealed class ZoneStubLlmClient : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            var zoneName = ExtractZone(userPrompt);
            var entityName = ExtractEntity(userPrompt);
            var marker = entityName is null
                ? $"// STUB:{zoneName}"
                : $"// STUB:{zoneName}-for-{entityName}";
            return Task.FromResult(new LlmResponse(marker, InputTokens: 50, OutputTokens: 20, Model: "zone-stub"));
        }

        private static string ExtractZone(string prompt)
        {
            const string marker = "Zone name: `";
            var i = prompt.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return "Unknown";
            i += marker.Length;
            var end = prompt.IndexOf('`', i);
            return end > i ? prompt[i..end] : "Unknown";
        }

        private static string? ExtractEntity(string prompt)
        {
            const string marker = "Entity: ";
            var i = prompt.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return null;
            i += marker.Length;
            var end = prompt.IndexOfAny(['\r', '\n'], i);
            return end > i ? prompt[i..end] : null;
        }
    }
}
