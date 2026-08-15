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

    #region Path Routing

    private static TemplateProvider RealZoneProvider() => new(
        new System.IO.Abstractions.TestingHelpers.MockFileSystem(), "/templates");

    private static Dictionary<string, string> V1TreeShape() => new()
    {
        ["dotnet/Program.cs"] = "// program\n",
        ["nextjs/src/types/index.ts"] = "export {};\n",
        ["Dockerfile"] = "FROM scratch\n",
    };

    [Fact]
    public void Reconstruct_WithPathOutsideTheRenderedTree_ThrowsInsteadOfOrphaningTheFile()
    {
        // The shipped bug: the prompt asked for src/types/index.ts while the tree keeps its
        // frontend at nextjs/src/. The path matched no zone, so the file was written at the
        // archive root — present in the zip, invisible to the app — and the build stayed green.
        var llmBlocks = new Dictionary<string, string>
        {
            ["src/types/index.ts"] = "export interface Customer { id: string }",
            ["src/lib/api.ts"] = "export const getCustomers = () => {};",
        };

        var act = () => _sut.Reconstruct(V1TreeShape(), llmBlocks, RealZoneProvider());

        act.Should().Throw<UnmappedLlmFileException>()
           .Which.UnmappedPaths.Should().BeEquivalentTo(["src/types/index.ts", "src/lib/api.ts"]);
    }

    [Fact]
    public void Reconstruct_UnmappedPathMessage_NamesThePathsAndTheValidRoots()
    {
        var act = () => _sut.Reconstruct(
            V1TreeShape(),
            new Dictionary<string, string> { ["src/app/page.tsx"] = "export default function P() {}" },
            RealZoneProvider());

        // The message is what reaches the customer's error_message and the retry prompt.
        act.Should().Throw<UnmappedLlmFileException>()
           .WithMessage("*src/app/page.tsx*")
           .And.Message.Should().Contain("dotnet").And.Contain("nextjs");
    }

    [Fact]
    public void Reconstruct_WithPathInsideTheRenderedTree_OverwritesOrAddsTheFile()
    {
        var llmBlocks = new Dictionary<string, string>
        {
            ["nextjs/src/types/index.ts"] = "export interface Customer { id: string }",
            ["dotnet/Models/Customer.cs"] = "namespace App.Models;\npublic record Customer;",
            ["README.md"] = "# InvoiceHub",
        };

        var result = _sut.Reconstruct(V1TreeShape(), llmBlocks, RealZoneProvider());

        result["nextjs/src/types/index.ts"].Should().Contain("interface Customer",
            "a real file at a real path replaces the template stub — that is how the customer gets types");
        result.Should().ContainKey("dotnet/Models/Customer.cs");
        result.Should().ContainKey("README.md", "a bare file name sits beside the template's own root files");
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("dotnet/../../../evil.cs")]
    [InlineData("C:/Windows/System32/evil.cs")]
    public void Reconstruct_TraversalPath_IsRejected(string path)
    {
        // These paths are model output and get Path.Combine'd with the output directory
        // before being written, so a traversal segment escapes the archive entirely.
        var act = () => _sut.Reconstruct(
            V1TreeShape(),
            new Dictionary<string, string> { [path] = "pwned" },
            RealZoneProvider());

        act.Should().Throw<UnmappedLlmFileException>();
    }

    [Fact]
    public void Reconstruct_ZoneBlock_FillsTheZoneAndIsNeverWrittenAsAFile()
    {
        var rendered = new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] =
                "var app = builder.Build();\n" +
                "[[LLM_INJECTION_START: RouteRegistrations]]\n" +
                "[[LLM_INJECTION_END: RouteRegistrations]]\n",
        };
        var llmBlocks = new Dictionary<string, string>
        {
            ["__zone__/RouteRegistrations"] = "app.MapCustomerEndpoints();",
        };

        var result = _sut.Reconstruct(rendered, llmBlocks, RealZoneProvider());

        result["dotnet/Program.cs"].Should().Contain("app.MapCustomerEndpoints();");
        result.Keys.Should().NotContain(k => k.StartsWith("__zone__", StringComparison.Ordinal),
            "a zone pseudo-path is an address, not a file");
    }

    [Fact]
    public void Reconstruct_FilePathNamingAZoneDirectory_IsNotSwallowedByTheZone()
    {
        // Zones used to be matched by directory substring: any path containing "Models/"
        // filled the Models zone AND was written as a file, so the same records could exist
        // twice (CS0101) or land somewhere unreadable. Routing is now by zone name only.
        var rendered = new Dictionary<string, string>
        {
            ["dotnet/Models/_placeholder.cs"] =
                "[[LLM_INJECTION_START: Models]]\n[[LLM_INJECTION_END: Models]]\n",
        };
        var llmBlocks = new Dictionary<string, string>
        {
            ["dotnet/Models/Customer.cs"] = "namespace App.Models;\npublic record Customer;",
        };

        var result = _sut.Reconstruct(rendered, llmBlocks, RealZoneProvider());

        result["dotnet/Models/Customer.cs"].Should().Contain("public record Customer;");
        result["dotnet/Models/_placeholder.cs"].Should().NotContain("public record Customer;",
            "the entity is a real file; duplicating it into the placeholder is a duplicate definition");
    }

    [Fact]
    public void Reconstruct_DriveRelativePath_IsRejected()
    {
        // "C:evil.cs" has no separator, so it is a single segment and the bare-file-name
        // allowance would wave it through — but Path.Combine(outputDir, "C:evil.cs") returns
        // "C:evil.cs", writing to the process's current directory on C: instead of the archive.
        var act = () => _sut.Reconstruct(
            V1TreeShape(),
            new Dictionary<string, string> { ["C:evil.cs"] = "pwned" },
            RealZoneProvider());

        act.Should().Throw<UnmappedLlmFileException>();
    }

    [Fact]
    public void Reconstruct_RootedPath_IsRejectedRatherThanSilentlyTrimmed()
    {
        // The check normalizes but the write does not: the block is stored under its original
        // path and later Path.Combine'd, and Path.Combine discards the base directory when the
        // second argument is rooted. Validating the trimmed form would green-light a write to
        // the root of the drive.
        var act = () => _sut.Reconstruct(
            V1TreeShape(),
            new Dictionary<string, string> { ["/nextjs/src/lib/api.ts"] = "export {};" },
            RealZoneProvider());

        act.Should().Throw<UnmappedLlmFileException>();
    }

    #endregion

    #region Build-Repair Routing

    /// <summary>An output directory shaped like a rendered V1 archive.</summary>
    private static string MakeTreeOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "sa-repair-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "dotnet", "Repositories"));
        Directory.CreateDirectory(Path.Combine(root, "nextjs", "src", "lib"));
        return root;
    }

    [Fact]
    public void ResolveRepairWrites_PathInsideTheTree_IsWritten()
    {
        var root = MakeTreeOnDisk();
        try
        {
            var blocks = new Dictionary<string, string>
            {
                ["dotnet/Repositories/CustomerRepository.cs"] = "// fixed",
                ["nextjs/src/lib/api.ts"] = "export {};",
            };

            var result = _sut.ResolveRepairWrites(blocks, root);

            result.Writes.Should().HaveCount(2);
            result.Rejected.Should().BeEmpty();
            result.RejectionNotice.Should().BeNull();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>
    /// The repair loop was the second, unguarded write site: it parsed the response and wrote
    /// every block straight to disk, so the exact failures Reconstruct rejects were all still
    /// reachable — just one build attempt later, on a generation already in trouble.
    /// </summary>
    [Theory]
    [InlineData("src/lib/api.ts", "orphans at the archive root, where nothing serves it")]
    [InlineData("__zone__/RouteRegistrations", "the zones are resolved by now — this lands in the zip as a literal file")]
    [InlineData("dotnet/../../../evil.cs", "a traversal segment escapes the output directory")]
    [InlineData("C:evil.cs", "a drive-relative path escapes the output directory")]
    public void ResolveRepairWrites_PathOutsideTheTree_IsRefusedNotWritten(string path, string because)
    {
        var root = MakeTreeOnDisk();
        try
        {
            var result = _sut.ResolveRepairWrites(
                new Dictionary<string, string> { [path] = "content" }, root);

            result.Writes.Should().BeEmpty(because);
            result.Rejected.Should().ContainSingle().Which.Should().Be(path);
            result.RejectionNotice.Should().Contain(path);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ResolveRepairWrites_MixedResponse_AppliesTheGoodBlocksAndRefusesTheRest()
    {
        var root = MakeTreeOnDisk();
        try
        {
            var blocks = new Dictionary<string, string>
            {
                ["dotnet/Repositories/CustomerRepository.cs"] = "// fixed",
                ["src/lib/api.ts"] = "export {};",
            };

            var result = _sut.ResolveRepairWrites(blocks, root);

            result.Writes.Should().ContainKey("dotnet/Repositories/CustomerRepository.cs")
                  .And.NotContainKey("src/lib/api.ts");
            result.Rejected.Should().ContainSingle().Which.Should().Be("src/lib/api.ts");
            result.RejectionNotice.Should().Contain("dotnet").And.Contain("nextjs",
                "the notice has to name the roots that WOULD have worked");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>
    /// Reporting, not throwing, is deliberate. Aborting the repair would take the job out
    /// through the worker's generic catch, which marks it Failed without reaching the
    /// compile-guarantee refund — costing a paying customer both the app and the money.
    /// </summary>
    [Fact]
    public void ResolveRepairWrites_WithEveryBlockRefused_ReportsRatherThanThrows()
    {
        var root = MakeTreeOnDisk();
        try
        {
            var act = () => _sut.ResolveRepairWrites(
                new Dictionary<string, string> { ["src/app/page.tsx"] = "x" }, root);

            act.Should().NotThrow();
            act().Rejected.Should().ContainSingle();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    #endregion
}
