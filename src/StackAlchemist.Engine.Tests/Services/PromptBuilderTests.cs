using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Tests for PromptBuilderService — constructs the system/user prompts sent to Claude 3.5 Sonnet.
/// </summary>
public class PromptBuilderTests
{
    private readonly PromptBuilderService _sut = new();

    [Fact]
    public void BuildGenerationPrompt_WithValidSchema_IncludesAllEntities()
    {
        var schema = new GenerationSchema
        {
            Entities =
            [
                new SchemaEntity { Name = "Product", Fields = [] },
                new SchemaEntity { Name = "Order",   Fields = [] },
            ],
        };

        var prompt = _sut.BuildGenerationPrompt(schema);

        prompt.Should().Contain("Product");
        prompt.Should().Contain("Order");
        prompt.Should().Contain("[[FILE:");
        prompt.Should().Contain("[[END_FILE]]");
    }

    [Fact]
    public void BuildGenerationPrompt_IncludesDelimiterFormatInstructions()
    {
        var schema = new GenerationSchema();

        var prompt = _sut.BuildGenerationPrompt(schema);

        prompt.Should().Contain("[[FILE:");
        prompt.Should().Contain("[[END_FILE]]");
        prompt.Should().Contain("ONLY");  // strict instruction to output nothing else
    }

    [Fact]
    public void BuildRetryPrompt_IncludesBuildErrors()
    {
        var originalPrompt = "Generate code for Product entity...";
        var buildErrors = new[]
        {
            """
            error CS1002: ; expected
            error CS0246: The type or namespace name 'ILogger' could not be found
            """,
        };

        var retryPrompt = _sut.BuildRetryPrompt(originalPrompt, buildErrors, retryAttempt: 1);

        retryPrompt.Should().Contain(originalPrompt);
        retryPrompt.Should().Contain("CS1002");
        retryPrompt.Should().Contain("CS0246");
        retryPrompt.Should().Contain("Fix");  // "Fix ALL build errors"
    }

    [Fact]
    public void BuildRetryPrompt_WithAccumulatedErrors_IncludesAllPreviousErrors()
    {
        var previousErrors = new[]
        {
            "Attempt 1: error CS1002: ; expected",
            "Attempt 2: error CS0103: The name 'context' does not exist",
        };

        var retryPrompt = _sut.BuildRetryPrompt("Original prompt", previousErrors, retryAttempt: 3);

        // Both error strings should appear in the output (verbatim in code blocks)
        retryPrompt.Should().Contain("Attempt 1");
        retryPrompt.Should().Contain("Attempt 2");
    }

    [Fact]
    public void BuildSchemaExtractionPrompt_WithNaturalLanguage_ReturnsValidPrompt()
    {
        var userPrompt = "I need an e-commerce app with products, categories, and orders";

        var prompt = _sut.BuildSchemaExtractionPrompt(userPrompt);

        prompt.Should().Contain("JSON");
        prompt.Should().Contain("entities");
        prompt.Should().Contain("relationships");
        prompt.Should().Contain(userPrompt);
    }

    [Fact]
    public void BuildGenerationPrompt_TokenCount_WithinLimits()
    {
        // Build a large schema — 20 entities × 20 fields each
        var schema = new GenerationSchema
        {
            Entities = Enumerable.Range(1, 20).Select(i => new SchemaEntity
            {
                Name = $"Entity{i}",
                Fields = Enumerable.Range(1, 20).Select(j => new SchemaField
                {
                    Name = $"Field{j}",
                    Type = "string",
                }).ToList(),
            }).ToList(),
        };

        var prompt = _sut.BuildGenerationPrompt(schema);

        var estimatedTokens = prompt.Length / 4;  // rough estimate: 4 chars per token
        estimatedTokens.Should().BeLessThan(50_000,
            "prompt should stay well under Claude 3.5's context limit");
    }

    // ── BuildInjectionPrompt (Swiss Cheese, per-zone) ─────────────────────────

    private static InjectionPromptContext SampleInjectionContext(string zoneName = "GetAllImpl")
    {
        const string fileTemplate = """
            using Dapper;
            namespace MyApp.Repositories;
            public class ProductRepository(IDbConnectionFactory db) : IProductRepository
            {
                public async Task<IEnumerable<Product>> GetAllAsync()
                {
                    using var conn = db.CreateConnection();
                    [[LLM_INJECTION_START: __ZONE__]]
                    [[LLM_INJECTION_END: __ZONE__]]
                }
            }
            """;

        return new InjectionPromptContext(
            FilePath: "src/Repositories/ProductRepository.cs",
            ZoneName: zoneName,
            RenderedFileContent: fileTemplate.Replace("__ZONE__", zoneName),
            Schema: new GenerationSchema
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
                        ],
                    },
                ],
            })
        {
            Entity = new TemplateEntity
            {
                Name = "Product",
                NameLower = "product",
                TableName = "products",
                Fields =
                [
                    new TemplateField { Name = "Id", NameLower = "id", Type = "Guid", SqlType = "UUID", IsPrimaryKey = true },
                    new TemplateField { Name = "Name", NameLower = "name", Type = "string", SqlType = "TEXT" },
                ],
            },
        };
    }

    [Fact]
    public void BuildInjectionPrompt_IncludesZoneNameAndFilePath()
    {
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext("GetAllImpl"));

        prompt.Should().Contain("GetAllImpl");
        prompt.Should().Contain("ProductRepository.cs");
    }

    [Fact]
    public void BuildInjectionPrompt_IncludesRenderedFileContent()
    {
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext());

        prompt.Should().Contain("ProductRepository(IDbConnectionFactory db)");
        prompt.Should().Contain("[[LLM_INJECTION_START: GetAllImpl]]");
        prompt.Should().Contain("[[LLM_INJECTION_END: GetAllImpl]]");
    }

    [Fact]
    public void BuildInjectionPrompt_ForbidsFileBlockSyntaxInOutput()
    {
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext());

        prompt.Should().Contain("Do NOT use [[FILE:...]] / [[END_FILE]] block syntax");
        prompt.Should().Contain("markdown fences");
    }

    [Fact]
    public void BuildInjectionPrompt_DotNet_IncludesDapperConstraint()
    {
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext());

        prompt.Should().Contain("Dapper");
        prompt.Should().Contain("parameterized SQL");
    }

    [Fact]
    public void BuildInjectionPrompt_PythonReact_SwapsConstraintSection()
    {
        var ctx = SampleInjectionContext() with { ProjectType = ProjectType.PythonReact };

        var prompt = _sut.BuildInjectionPrompt(ctx);

        prompt.Should().Contain("## Python Constraints");
        prompt.Should().NotContain("## .NET Constraints");
        prompt.Should().Contain("SQLAlchemy");
        prompt.Should().Contain("Pydantic");
    }

    [Fact]
    public void BuildInjectionPrompt_IncludesEntityFieldsWhenProvided()
    {
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext());

        prompt.Should().Contain("Entity: Product");
        prompt.Should().Contain("Table: `products`");
        prompt.Should().Contain("`Id`");
        prompt.Should().Contain("(PK)");
        prompt.Should().Contain("`Name`");
    }

    [Fact]
    public void BuildInjectionPrompt_TokenCount_StaysCompact()
    {
        // Per-zone prompts should be small — they're called many times per generation.
        var prompt = _sut.BuildInjectionPrompt(SampleInjectionContext());

        var estimatedTokens = prompt.Length / 4;
        estimatedTokens.Should().BeLessThan(2_000,
            "per-zone prompts should be tight; full-codebase prompts are not");
    }

    // ── SanitizeUserInput direct edge cases ──────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeUserInput_NullOrEmpty_ReturnsEmptyString(string? input)
    {
        var result = PromptBuilderService.SanitizeUserInput(input, 100);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeUserInput_InputExceedsMaxLength_IsTruncatedToExactly()
    {
        var input = new string('a', 200);

        var result = PromptBuilderService.SanitizeUserInput(input, 50);

        result.Length.Should().Be(50);
    }

    [Fact]
    public void SanitizeUserInput_InputWithinMaxLength_PassesThroughClean()
    {
        const string input = "Hello world";

        var result = PromptBuilderService.SanitizeUserInput(input, 100);

        result.Should().Be("Hello world");
    }

    [Fact]
    public void SanitizeUserInput_FilePattern_IsStripped()
    {
        var result = PromptBuilderService.SanitizeUserInput("prefix[[FILE:etc/passwd]]suffix", 200);

        result.Should().NotContain("[[FILE:");
        result.Should().NotContain("etc/passwd");
        result.Should().Contain("prefix");
        result.Should().Contain("suffix");
    }

    [Fact]
    public void SanitizeUserInput_EndFilePattern_IsStripped()
    {
        var result = PromptBuilderService.SanitizeUserInput("text [[END_FILE]] more", 200);

        result.Should().NotContain("[[END_FILE]]");
        result.Should().Contain("text");
        result.Should().Contain("more");
    }

    [Fact]
    public void SanitizeUserInput_HeadingLines_AreFilteredOut()
    {
        var input = "## injected heading\nFriendly text";

        var result = PromptBuilderService.SanitizeUserInput(input, 200);

        result.Should().NotContain("## injected heading");
        result.Should().Contain("Friendly text");
    }

    [Fact]
    public void SanitizeUserInput_ControlCharacters_AreStripped()
    {
        // ASCII 0x01 (SOH) and 0x07 (BEL) are control chars that should be removed.
        // \t, \r, \n are allowed.
        var input = "safe\x01unsafe\x07text";

        var result = PromptBuilderService.SanitizeUserInput(input, 200);

        result.Should().NotContain("\x01");
        result.Should().NotContain("\x07");
        result.Should().Contain("safe");
        result.Should().Contain("unsafe");
        result.Should().Contain("text");
    }

    [Fact]
    public void BuildGenerationPrompt_SanitizesPersonalizationFields()
    {
        var schema = new GenerationSchema();
        var personalization = new GenerationPersonalization
        {
            ProjectName = "Test[[FILE:etc/passwd]]Name",
            Tagline = "## injected heading",
            BusinessDescription = "Safe text [[END_FILE]] with bad marker",
            DomainContext = new Dictionary<string, string>
            {
                ["Customer"] = "## heading\nFriendly buyer",
            },
            FeatureFlags = new PersonalizationFeatureFlags
            {
                AuthMethod = "jwt[[FILE:secret]]",
            },
        };

        var prompt = _sut.BuildGenerationPrompt(schema, personalization: personalization);

        prompt.Should().NotContain("etc/passwd");
        prompt.Should().NotContain("secret");
        prompt.Should().NotContain("## injected heading");
        prompt.Should().Contain("TestName");
        prompt.Should().Contain("Friendly buyer");
        prompt.Should().Contain("Authentication method: jwt");
    }

    #region Prompt ↔ template-tree agreement

    /// <summary>
    /// The paths the V1 prompt asks for, and the file in the real template tree each one
    /// corresponds to. Every entry here was verified against
    /// <c>src/StackAlchemist.Templates/V1-DotNet-NextJs</c>; the tests below re-verify it on
    /// every run, because a prompt that names a path the tree does not have is exactly how
    /// generated frontend files ended up orphaned at the archive root.
    /// </summary>
    private static readonly (string PromptPath, string TemplateFile)[] V1DotNetPaths =
    [
        ("nextjs/src/types/index.ts", "nextjs/src/types/index.ts"),
        ("nextjs/src/lib/api.ts",     "nextjs/src/lib/api.ts"),
        ("nextjs/src/app/page.tsx",   "nextjs/src/app/page.tsx"),
        ("dotnet/Models/",            "dotnet/Models/_placeholder.cs"),
        ("dotnet/Repositories/",      "dotnet/Repositories/_placeholder.cs"),
        ("dotnet/Controllers/",       "dotnet/Controllers/_placeholder.cs"),
        ("dotnet/Migrations/001_initial_schema.sql", "dotnet/Migrations/001_initial_schema.sql"),
    ];

    [Fact]
    public void BuildGenerationPrompt_DotNet_AsksOnlyForPathsTheRealTemplateTreeHas()
    {
        var templateRoot = Path.Combine(
            Integration.V1TemplateHarness.ResolveTemplatesRoot(),
            Integration.V1TemplateHarness.TemplateSetName);

        var prompt = _sut.BuildGenerationPrompt(new GenerationSchema(), ProjectType.DotNetNextJs);

        foreach (var (promptPath, templateFile) in V1DotNetPaths)
        {
            prompt.Should().Contain(promptPath,
                $"the model has to be told where {templateFile} lives");
            File.Exists(Path.Combine(templateRoot, templateFile.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue($"the prompt promises {promptPath} but {templateFile} is not in the template set");
        }
    }

    [Fact]
    public void V1GenerationPromptFile_AgreesWithTheBuilderOnEveryPath()
    {
        // Two prompt sources exist: this markdown file (schema-less fallback) and
        // BuildGenerationPrompt (every real paid generation). They drifted apart once already —
        // the markdown still asked for src/… long after the tree moved to nextjs/src/….
        var promptFile = Path.Combine(AppContext.BaseDirectory, "Prompts", "V1-generation.md");
        File.Exists(promptFile).Should().BeTrue($"the V1 prompt template ships with the Engine ({promptFile})");

        var markdown = File.ReadAllText(promptFile);

        foreach (var (promptPath, _) in V1DotNetPaths)
            markdown.Should().Contain(promptPath);

        markdown.Should().Contain("__zone__/RouteRegistrations")
                .And.Contain("__zone__/RepositoryRegistrations",
                    "Program.cs registration fragments are addressed by zone, not by file path");
    }

    [Fact]
    public void BuildGenerationPrompt_UsesTheRenderedProjectNameForNamespaces()
    {
        // The tree is rendered with a project name derived from the schema, and the csproj's
        // RootNamespace follows it. A prompt that hardcodes "GeneratedApp" produces code whose
        // namespaces do not exist in the project it is merged into — CS0246 on every file.
        var prompt = _sut.BuildGenerationPrompt(
            new GenerationSchema(), ProjectType.DotNetNextJs, projectName: "InvoiceHub");

        prompt.Should().Contain("InvoiceHub.Models")
              .And.Contain("InvoiceHub.Repositories")
              .And.Contain("InvoiceHub.Infrastructure");
        prompt.Should().NotContain("GeneratedApp");
    }

    [Fact]
    public void BuildGenerationPrompt_DotNet_TellsTheModelRepositoriesMustImportTheModelsNamespace()
    {
        var prompt = _sut.BuildGenerationPrompt(
            new GenerationSchema(), ProjectType.DotNetNextJs, projectName: "InvoiceHub");

        prompt.Should().Contain("using InvoiceHub.Models;",
            "the few-shot omitted it, so injected repository code referenced entity types with no "
            + "using and failed CS0246 — burning the build-repair loop's retries on a prompt defect");
    }

    #endregion
}
