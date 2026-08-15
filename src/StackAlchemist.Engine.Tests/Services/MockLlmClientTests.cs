using FluentAssertions;
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
}
