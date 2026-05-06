using System.Collections.Concurrent;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class InjectionEngineTests
{
    private static InjectionEngine BuildEngine(
        Func<string, Task<LlmResponse>> llmHandler,
        InjectionEngineOptions? options = null)
    {
        var fs = new MockFileSystem();
        var templateProvider = new TemplateProvider(fs, "/templates");
        var promptBuilder = new PromptBuilderService();
        var llmClient = new StubLlmClient(llmHandler);
        return new InjectionEngine(templateProvider, promptBuilder, llmClient, NullLogger<InjectionEngine>.Instance, options);
    }

    private static TemplateVariables OneEntityVars(string entityName = "Product") => new()
    {
        ProjectName = "MyApp",
        ProjectNameKebab = "my-app",
        ProjectNameLower = "myapp",
        DbConnectionString = "Host=localhost",
        FrontendUrl = "http://localhost:3000",
        Entities =
        [
            new TemplateEntity
            {
                Name = entityName,
                NameLower = entityName.ToLowerInvariant(),
                TableName = entityName.ToLowerInvariant() + "s",
                Fields =
                [
                    new TemplateField { Name = "Id", NameLower = "id", Type = "Guid", SqlType = "UUID", IsPrimaryKey = true },
                    new TemplateField { Name = "Name", NameLower = "name", Type = "string", SqlType = "TEXT" },
                ],
            },
        ],
    };

    private static GenerationSchema OneEntitySchema(string entityName = "Product") => new()
    {
        Entities =
        [
            new SchemaEntity
            {
                Name = entityName,
                Fields =
                [
                    new SchemaField { Name = "Id", Type = "uuid", Pk = true },
                    new SchemaField { Name = "Name", Type = "string" },
                ],
            },
        ],
    };

    [Fact]
    public async Task FillZonesAsync_WithNoZones_ReturnsRenderedFilesUnchanged()
    {
        var rendered = new Dictionary<string, string>
        {
            ["Models/Product.cs"] = "public record Product { public Guid Id { get; init; } }",
            ["Program.cs"] = "var app = builder.Build();",
        };
        var engine = BuildEngine(_ => throw new InvalidOperationException("LLM should not be called"));

        var result = (await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null)).FilledTemplates;

        result.Should().BeEquivalentTo(rendered);
    }

    [Fact]
    public async Task FillZonesAsync_FillsEachZoneAndStripsMarkers()
    {
        var rendered = new Dictionary<string, string>
        {
            ["Repositories/ProductRepository.cs"] = """
                public class ProductRepository
                {
                    public async Task<IEnumerable<Product>> GetAllAsync()
                    {
                        [[LLM_INJECTION_START: GetAllImpl]]
                        [[LLM_INJECTION_END: GetAllImpl]]
                    }
                    public async Task<Product> CreateAsync(Create r)
                    {
                        [[LLM_INJECTION_START: CreateImpl]]
                        [[LLM_INJECTION_END: CreateImpl]]
                    }
                }
                """,
        };

        var engine = BuildEngine(prompt =>
        {
            // Return zone-specific content based on the zone name in the prompt.
            var content = prompt.Contains("Zone name: `GetAllImpl`")
                ? "return await conn.QueryAsync<Product>(\"SELECT * FROM products\");"
                : "throw new NotImplementedException();";
            return Task.FromResult(new LlmResponse(content, 0, 0, "stub"));
        });

        var result = (await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null)).FilledTemplates;

        var output = result["Repositories/ProductRepository.cs"];
        output.Should().Contain("return await conn.QueryAsync<Product>");
        output.Should().Contain("throw new NotImplementedException();");
        output.Should().NotContain("LLM_INJECTION_START");
        output.Should().NotContain("LLM_INJECTION_END");
    }

    [Fact]
    public async Task FillZonesAsync_StripsMarkdownFencesAndFileBlockMarkers()
    {
        var rendered = new Dictionary<string, string>
        {
            ["repo.cs"] = """
                method:
                [[LLM_INJECTION_START: Body]]
                [[LLM_INJECTION_END: Body]]
                """,
        };

        var llmOutput = """
            ```csharp
            [[FILE:repo.cs]]
            return 42;
            [[END_FILE]]
            ```
            """;

        var engine = BuildEngine(_ => Task.FromResult(new LlmResponse(llmOutput, 0, 0, "stub")));

        var result = (await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null)).FilledTemplates;

        var output = result["repo.cs"];
        output.Should().Contain("return 42;");
        output.Should().NotContain("```");
        output.Should().NotContain("[[FILE:");
        output.Should().NotContain("[[END_FILE]]");
        output.Should().NotContain("LLM_INJECTION_");
    }

    [Fact]
    public async Task FillZonesAsync_RetriesOnEmptyResponseThenSucceeds()
    {
        var rendered = new Dictionary<string, string>
        {
            ["repo.cs"] = "[[LLM_INJECTION_START: Body]]\n[[LLM_INJECTION_END: Body]]",
        };

        var attempts = 0;
        var engine = BuildEngine(_ =>
        {
            attempts++;
            var text = attempts == 1 ? "" : "return 42;";
            return Task.FromResult(new LlmResponse(text, 0, 0, "stub"));
        });

        var result = (await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null)).FilledTemplates;

        attempts.Should().Be(2);
        result["repo.cs"].Should().Contain("return 42;");
    }

    [Fact]
    public async Task FillZonesAsync_FailsAfterMaxAttempts()
    {
        var rendered = new Dictionary<string, string>
        {
            ["repo.cs"] = "[[LLM_INJECTION_START: Body]]\n[[LLM_INJECTION_END: Body]]",
        };

        var engine = BuildEngine(_ => throw new InvalidOperationException("503 service unavailable"));

        var act = () => engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null);

        await act.Should().ThrowAsync<InjectionFailedException>()
            .WithMessage("*Body*repo.cs*after 2 attempts*");
    }

    [Fact]
    public async Task FillZonesAsync_RespectsConcurrencyLimit()
    {
        var rendered = new Dictionary<string, string>();
        for (var i = 0; i < 10; i++)
        {
            rendered[$"file_{i}.cs"] = $"[[LLM_INJECTION_START: Zone{i}]]\n[[LLM_INJECTION_END: Zone{i}]]";
        }

        var inFlight = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var engine = BuildEngine(async prompt =>
        {
            int currentInFlight;
            lock (lockObj)
            {
                inFlight++;
                currentInFlight = inFlight;
                if (currentInFlight > maxObserved) maxObserved = currentInFlight;
            }
            await Task.Delay(20);
            lock (lockObj) { inFlight--; }
            return new LlmResponse("// done", 0, 0, "stub");
        }, options: new InjectionEngineOptions { MaxConcurrency = 3 });

        await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null);

        maxObserved.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task FillZonesAsync_PassesEntityContextForPerEntityFile()
    {
        var rendered = new Dictionary<string, string>
        {
            ["Repositories/ProductRepository.cs"] = "[[LLM_INJECTION_START: Body]]\n[[LLM_INJECTION_END: Body]]",
        };

        var capturedPrompt = "";
        var engine = BuildEngine(prompt =>
        {
            capturedPrompt = prompt;
            return Task.FromResult(new LlmResponse("// ok", 0, 0, "stub"));
        });

        await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null);

        capturedPrompt.Should().Contain("Entity: Product");
        capturedPrompt.Should().Contain("Table: `products`");
    }

    private sealed class StubLlmClient(Func<string, Task<LlmResponse>> handler) : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
            => handler(userPrompt);
    }
}
