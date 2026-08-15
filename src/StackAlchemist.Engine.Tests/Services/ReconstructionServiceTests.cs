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

    #region Injection Marker Stripping

    [Fact]
    public void Reconstruct_StripsInjectionMarkersFromFilledZones()
    {
        // Prod bug: the V1 one-shot path injected LLM content into the zone and shipped the
        // [[LLM_INJECTION_*]] lines along with it. In Program.cs that is a C# syntax error,
        // so every Tier-2 archive failed `dotnet build` no matter how good the LLM output was.
        var rendered = new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] =
                "var app = builder.Build();\n" +
                "[[LLM_INJECTION_START: RouteRegistrations]]\n" +
                "[[LLM_INJECTION_END: RouteRegistrations]]\n" +
                "app.Run();\n",
        };
        var llmBlocks = new Dictionary<string, string>
        {
            ["__zone__/RouteRegistrations"] = "app.MapGet(\"/health\", () => Results.Ok());",
        };

        var result = _sut.Reconstruct(rendered, llmBlocks, new TemplateProvider(
            new System.IO.Abstractions.TestingHelpers.MockFileSystem(), "/templates"));

        var program = result["dotnet/Program.cs"];
        program.Should().NotContain("[[LLM_INJECTION_");
        program.Should().Contain("app.MapGet(\"/health\"");
        program.Should().Contain("var app = builder.Build();");
        program.Should().Contain("app.Run();");
    }

    [Fact]
    public void Reconstruct_WithNoLlmBlocks_CollapsesZonesSoTheBareTemplateIsStillValid()
    {
        // The LLM pass is additive: an empty response must degrade to the plain template,
        // not to a file full of marker lines.
        var rendered = new Dictionary<string, string>
        {
            ["nextjs/src/app/page.tsx"] =
                "<main>\n" +
                "  [[LLM_INJECTION_START: HomePageContent]]\n" +
                "  [[LLM_INJECTION_END: HomePageContent]]\n" +
                "</main>\n",
        };

        var result = _sut.Reconstruct(rendered, [], new TemplateProvider(
            new System.IO.Abstractions.TestingHelpers.MockFileSystem(), "/templates"));

        result["nextjs/src/app/page.tsx"].Should().Be("<main>\n</main>\n");
    }

    #endregion
}
