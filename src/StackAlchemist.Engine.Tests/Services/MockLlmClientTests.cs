using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class MockLlmClientTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsDelimitedFileBlocks()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("system", "user");

        result.Text.Should().Contain("[[FILE:");
        result.Text.Should().Contain("[[END_FILE]]");
        result.Text.Should().Contain("dotnet/Models/Product.cs");
        result.Model.Should().Be("mock-llm");
    }

    [Fact]
    public async Task GenerateAsync_ResponseContainsExpectedCoreArtifacts()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("system", "user");

        result.Text.Should().Contain("dotnet/Controllers/ProductEndpoints.cs");
        result.Text.Should().Contain("dotnet/Repositories/ProductRepository.cs");
        result.Text.Should().Contain("dotnet/Migrations/001_initial_schema.sql");
    }

    /// <summary>
    /// The offline client is the response every no-API-key run of the pipeline sees, so its
    /// paths have to be the ones the real V1 tree has. It used to emit <c>src/Models/…</c> and
    /// <c>src/types/index.ts</c> — the same wrong paths the prompt asked for — which made the
    /// orphaned-<c>src/</c> layout look like correct output to anyone testing locally.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_EmitsOnlyPathsInsideTheGeneratedProjectTree()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("system", "user");

        var paths = System.Text.RegularExpressions.Regex
            .Matches(result.Text, @"\[\[FILE:\s*(.+?)\s*\]\]")
            .Select(m => m.Groups[1].Value)
            .ToList();

        paths.Should().NotBeEmpty();
        paths.Should().OnlyContain(
            p => p.StartsWith("dotnet/", StringComparison.Ordinal)
              || p.StartsWith("nextjs/", StringComparison.Ordinal)
              || p.StartsWith("__zone__/", StringComparison.Ordinal),
            "anything else lands at the archive root, where nothing compiles or serves it");
    }

    /// <summary>
    /// The tree's RootNamespace is schema-derived, so <c>GeneratedApp</c> is a guess that is
    /// wrong for every generation whose schema names anything else. Hardcoding it produced files
    /// declaring <c>namespace GeneratedApp.Repositories;</c> and importing
    /// <c>GeneratedApp.Infrastructure</c> / <c>GeneratedApp.Models</c> — namespaces the project
    /// does not contain (CS0234, then CS0246 on <c>IDbConnectionFactory</c>).
    ///
    /// The fix is the one a real model performs: read the namespace off the prompt, which states
    /// it precisely so the answer does not have to guess.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_UsesTheRootNamespaceTheGenerationPromptStates()
    {
        var sut = new MockLlmClient();
        var prompt = new PromptBuilderService().BuildGenerationPrompt(
            new GenerationSchema { Entities = [new SchemaEntity { Name = "Invoice" }] },
            projectName: "InvoiceHub");

        var result = await sut.GenerateAsync(prompt, "user");

        result.Text.Should().Contain("namespace InvoiceHub.Models;")
              .And.Contain("namespace InvoiceHub.Repositories;")
              .And.Contain("using InvoiceHub.Infrastructure;")
              .And.Contain("using InvoiceHub.Models;");
        result.Text.Should().NotContain("GeneratedApp",
            "a namespace the rendered project does not declare is CS0234 on every file that uses it");
    }

    /// <summary>
    /// Same fallback the orchestrator uses, so a prompt that states nothing still lines up with
    /// the tree it would have rendered.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WithNoNamespaceInThePrompt_FallsBackToTheOrchestratorDefault()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("Fix the compilation errors.", "user");

        result.Text.Should().Contain("namespace GeneratedApp.Models;");
        result.Text.Should().NotContain("__ROOT_NS__", "the placeholder must never reach the caller");
    }
}
