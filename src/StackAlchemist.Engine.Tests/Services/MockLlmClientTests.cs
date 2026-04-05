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

        result.Should().Contain("[[FILE:");
        result.Should().Contain("[[END_FILE]]");
        result.Should().Contain("src/Models/Product.cs");
    }

    [Fact]
    public async Task GenerateAsync_ResponseContainsExpectedCoreArtifacts()
    {
        var sut = new MockLlmClient();

        var result = await sut.GenerateAsync("system", "user");

        result.Should().Contain("src/Controllers/ProductEndpoints.cs");
        result.Should().Contain("src/Repositories/ProductRepository.cs");
        result.Should().Contain("src/Migrations/001_initial_schema.sql");
    }
}
