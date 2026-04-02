using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class ReconstructionServiceTests
{
    private readonly ReconstructionService _sut = new();

    #region Happy Path Tests

    [Fact]
    public void Parse_WithSingleValidBlock_ReturnsSingleFile()
    {
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            using Microsoft.AspNetCore.Mvc;

            namespace MyApp.Controllers;

            public class ProductsController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetAll() => Ok(new[] { "Product1", "Product2" });
            }
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result.Should().ContainKey("src/Controllers/ProductsController.cs");
        result.Should().HaveCount(1);
        result["src/Controllers/ProductsController.cs"].Should().Contain("ProductsController");
    }

    [Fact]
    public void Parse_WithMultipleValidBlocks_ReturnsAllFiles()
    {
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController { }
            [[END_FILE]]
            [[FILE:src/Controllers/OrdersController.cs]]
            public class OrdersController { }
            [[END_FILE]]
            [[FILE:src/Repositories/ProductRepository.cs]]
            public class ProductRepository { }
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result.Should().HaveCount(3);
        result.Keys.Should().Contain("src/Controllers/ProductsController.cs");
        result.Keys.Should().Contain("src/Controllers/OrdersController.cs");
        result.Keys.Should().Contain("src/Repositories/ProductRepository.cs");
    }

    #endregion

    #region Malformed Output Tests

    [Fact]
    public void Parse_WithMissingEndDelimiter_ThrowsMalformedOutputException()
    {
        // First block is valid, second is missing END_FILE
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController { }
            [[END_FILE]]

            [[FILE:src/Repositories/ProductRepository.cs]]
            public class ProductRepository { }
            """;

        var act = () => _sut.Parse(input);

        act.Should().Throw<MalformedLlmOutputException>()
           .WithMessage("*missing*END_FILE*");
    }

    [Fact]
    public void Parse_WithTruncatedResponse_ThrowsTruncatedResponseException()
    {
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController
            {
                public IActionResult GetAll()
                {
                    // Response cut off here by token lim
            """;

        var act = () => _sut.Parse(input);

        act.Should().Throw<TruncatedLlmResponseException>();
    }

    [Fact]
    public void Parse_WithMarkdownFencing_StripsMarkdownAndExtractsContent()
    {
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            ```csharp
            public class ProductsController { }
            ```
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result["src/Controllers/ProductsController.cs"]
            .Should().NotContain("```")
            .And.Contain("ProductsController");
    }

    [Fact]
    public void Parse_WithDuplicateFilePaths_LastOneWins()
    {
        var input = """
            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController_V1 { }
            [[END_FILE]]
            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController_V2 { }
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result.Should().HaveCount(1);
        result["src/Controllers/ProductsController.cs"]
            .Should().Contain("V2")
            .And.NotContain("V1");
    }

    [Fact]
    public void Parse_WithEmptyFileBlock_ReturnsEmptyString()
    {
        var input = """
            [[FILE:src/placeholder.cs]]
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result["src/placeholder.cs"].Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithPreambleText_IgnoresContentBeforeFirstFileBlock()
    {
        var input = """
            Here are the generated files for your project:

            [[FILE:src/Controllers/ProductsController.cs]]
            public class ProductsController { }
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_WithWhitespaceInFilePath_TrimsPath()
    {
        var input = """
            [[FILE:  src/Controllers/ProductsController.cs  ]]
            public class ProductsController { }
            [[END_FILE]]
            """;

        var result = _sut.Parse(input);

        result.Should().ContainKey("src/Controllers/ProductsController.cs");
    }

    [Fact]
    public void Parse_WithBomCharacters_StripsBomFromContent()
    {
        var bom = "\uFEFF";
        var input = $"[[FILE:src/Controllers/ProductsController.cs]]\n{bom}public class ProductsController {{ }}\n[[END_FILE]]";

        var result = _sut.Parse(input);

        result["src/Controllers/ProductsController.cs"]
            .Should().NotStartWith("\uFEFF");
    }

    [Fact]
    public void Parse_WithMixedLineEndings_NormalizesToUnixLineEndings()
    {
        var input = "[[FILE:test.cs]]\r\npublic class Test { }\r\n[[END_FILE]]";

        var result = _sut.Parse(input);

        result["test.cs"].Should().NotContain("\r\n");
        result["test.cs"].Should().Contain("public class Test { }");
    }

    #endregion

    #region Golden File Tests

    [Theory]
    [InlineData("single-entity-valid.txt", 3)]
    [InlineData("multi-entity-valid.txt", 12)]
    [InlineData("entity-with-relationships.txt", 7)]
    public void Parse_GoldenFile_ProducesExpectedFileCount(string fixtureName, int expectedFileCount)
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", fixtureName);

        if (!File.Exists(fixturePath))
        {
            Assert.Fail($"Golden file {fixtureName} not found at {fixturePath}");
            return;
        }

        var input = File.ReadAllText(fixturePath);
        var result = _sut.Parse(input);

        result.Should().HaveCount(expectedFileCount);
    }

    [Fact]
    public void Parse_MalformedDelimiters_ThrowsMalformedOutputException()
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", "malformed-delimiters.txt");
        if (!File.Exists(fixturePath)) { Assert.Fail("fixture missing"); return; }

        var input = File.ReadAllText(fixturePath);
        var act = () => _sut.Parse(input);

        act.Should().Throw<MalformedLlmOutputException>();
    }

    [Fact]
    public void Parse_TruncatedResponse_ThrowsTruncatedResponseException()
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", "truncated-response.txt");
        if (!File.Exists(fixturePath)) { Assert.Fail("fixture missing"); return; }

        var input = File.ReadAllText(fixturePath);
        var act = () => _sut.Parse(input);

        act.Should().Throw<TruncatedLlmResponseException>();
    }

    [Fact]
    public void Parse_EmptyFileBlock_Fixture_ParsesCorrectly()
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", "empty-file-block.txt");
        if (!File.Exists(fixturePath)) { Assert.Fail("fixture missing"); return; }

        var input = File.ReadAllText(fixturePath);
        var result = _sut.Parse(input);

        result.Should().HaveCount(3);
        result.Should().ContainKey("src/placeholder.cs");
        result["src/placeholder.cs"].Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExtraMarkdownWrapping_StripsAllFences()
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", "extra-markdown-wrapping.txt");
        if (!File.Exists(fixturePath)) { Assert.Fail("fixture missing"); return; }

        var input = File.ReadAllText(fixturePath);
        var result = _sut.Parse(input);

        result.Should().HaveCount(2);
        foreach (var content in result.Values)
        {
            content.Should().NotContain("```");
        }
    }

    [Fact]
    public void Parse_DuplicateFileBlocks_LastOneWins()
    {
        var fixturePath = Path.Combine("Fixtures", "LlmResponses", "duplicate-file-blocks.txt");
        if (!File.Exists(fixturePath)) { Assert.Fail("fixture missing"); return; }

        var input = File.ReadAllText(fixturePath);
        var result = _sut.Parse(input);

        // ProductsController appears twice; the second (corrected) version should win
        result["src/Controllers/ProductsController.cs"].Should().Contain("Second version");
    }

    #endregion
}
