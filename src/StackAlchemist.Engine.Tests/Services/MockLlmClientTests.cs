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
        result.Text.Should().Contain("src/Models/Product.cs");
        result.Model.Should().Be("mock-llm");
    }

    [Fact]
    public async Task GenerateAsync_ResponseContainsExpectedCoreArtifacts()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("system", "user");

        result.Text.Should().Contain("src/Controllers/ProductEndpoints.cs");
        result.Text.Should().Contain("src/Repositories/ProductRepository.cs");
        result.Text.Should().Contain("src/Migrations/001_initial_schema.sql");
    }
}
