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

    // ── Indentation (StackAlchemist#450) ─────────────────────────────────────
    // A fill keeps its relative indentation; its absolute indentation is the START marker's.

    private static async Task<string> FillOneFileAsync(
        string path, string template, Func<string, string> fillForZone, ProjectType projectType = ProjectType.PythonReact)
    {
        var engine = BuildEngine(prompt =>
        {
            var zone = System.Text.RegularExpressions.Regex.Match(prompt, "Zone name: `(?<z>[^`]+)`").Groups["z"].Value;
            return Task.FromResult(new LlmResponse(fillForZone(zone), 0, 0, "stub"));
        });

        var result = await engine.FillZonesAsync(
            new Dictionary<string, string> { [path] = template },
            OneEntitySchema(), OneEntityVars(), projectType, null);

        return result.FilledTemplates[path];
    }

    [Fact]
    public async Task FillZonesAsync_Python_FirstLineOfAFlushFillLandsAtTheDefBodyIndentation()
    {
        var template = """
            def get_all(db):
                [[LLM_INJECTION_START: GetAllImpl]]
                raise NotImplementedError()
                [[LLM_INJECTION_END: GetAllImpl]]


            def other():
                pass
            """;

        var output = await FillOneFileAsync("app/repositories/product.py", template,
            _ => "return db.query(Product).all()");

        output.Should().Be("""
            def get_all(db):
                return db.query(Product).all()


            def other():
                pass
            """);
    }

    [Fact]
    public async Task FillZonesAsync_Python_AlreadyIndentedMultiLineFillIsNotDoubleIndented()
    {
        // The prompt tells the model to match the file's indentation, so a well-behaved model
        // answers at the def body's column. Line 1 used to lose those four spaces to Trim().
        var template = """
            def delete(db, id):
                [[LLM_INJECTION_START: DeleteImpl]]
                [[LLM_INJECTION_END: DeleteImpl]]

            """;

        var output = await FillOneFileAsync("app/repositories/product.py", template, _ => """
                rows = db.query(Product).filter(Product.id == id).delete()
                db.commit()
                return rows > 0
            """);

        output.Should().Be("""
            def delete(db, id):
                rows = db.query(Product).filter(Product.id == id).delete()
                db.commit()
                return rows > 0

            """);
    }

    [Fact]
    public async Task FillZonesAsync_Python_NestedBlocksKeepTheirRelativeIndentation()
    {
        var template = """
            class ProductRepository:
                def update(self, db, id, payload):
                    [[LLM_INJECTION_START: UpdateImpl]]
                    [[LLM_INJECTION_END: UpdateImpl]]

            """;

        // Model answered flush-left; the nesting inside the fill is what must survive.
        var output = await FillOneFileAsync("app/repositories/product.py", template, _ => """
            item = db.get(Product, id)
            if item is None:
                return None

            for key, value in payload.model_dump().items():
                setattr(item, key, value)
            db.commit()
            return item
            """);

        output.Should().Be("""
            class ProductRepository:
                def update(self, db, id, payload):
                    item = db.get(Product, id)
                    if item is None:
                        return None

                    for key, value in payload.model_dump().items():
                        setattr(item, key, value)
                    db.commit()
                    return item

            """);
        output.Should().NotMatchRegex(@"(?m)^[ \t]+$", "a whitespace-only line is flake8 W293");
    }

    [Fact]
    public async Task FillZonesAsync_TabIndentedTemplate_GetsTabsNotSpaces()
    {
        var template = "def get(db, id):\n\t[[LLM_INJECTION_START: GetByIdImpl]]\n\t[[LLM_INJECTION_END: GetByIdImpl]]\n";

        var output = await FillOneFileAsync("app/repositories/product.py", template,
            _ => "\titem = db.get(Product, id)\n\tif item is None:\n\t\treturn None\n\treturn item");

        output.Should().Be("def get(db, id):\n\titem = db.get(Product, id)\n\tif item is None:\n\t\treturn None\n\treturn item\n");
    }

    [Fact]
    public async Task FillZonesAsync_SpaceIndentedTemplate_TabFlushFillGetsTheMarkersSpaces()
    {
        // Absolute indentation always comes from the template; the fill only contributes nesting.
        var template = "class Product:\n    [[LLM_INJECTION_START: Fields]]\n    [[LLM_INJECTION_END: Fields]]\n";

        var output = await FillOneFileAsync("app/schemas/product.py", template, _ => "\tname: str\n\tprice: float");

        output.Should().Be("class Product:\n    name: str\n    price: float\n");
    }

    [Fact]
    public async Task FillZonesAsync_FencedIndentedFill_KeepsTheFirstLinesIndentation()
    {
        // The old fence regex's `\s*` after "```python" also swallowed line 1's indentation.
        var template = """
            def get_all(db):
                [[LLM_INJECTION_START: GetAllImpl]]
                [[LLM_INJECTION_END: GetAllImpl]]

            """;

        var output = await FillOneFileAsync("app/repositories/product.py", template, _ => """
            ```python
            [[FILE:app/repositories/product.py]]
                items = db.query(Product).all()
                return items
            [[END_FILE]]
            ```
            """);

        output.Should().Be("""
            def get_all(db):
                items = db.query(Product).all()
                return items

            """);
    }

    [Fact]
    public async Task FillZonesAsync_CSharpMethodBody_EveryLineAtTheMarkersIndentation()
    {
        // Brace languages don't need this to compile, but the rule is the same for every file.
        var template = """
            public class ProductRepository
            {
                public async Task<Product?> GetByIdAsync(Guid id)
                {
                    [[LLM_INJECTION_START: GetByIdImpl]]
                    [[LLM_INJECTION_END: GetByIdImpl]]
                }
            }
            """;

        var output = await FillOneFileAsync("Repositories/ProductRepository.cs", template, _ => """
            await using var conn = await _db.OpenConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Product>(
                "SELECT * FROM products WHERE id = @id", new { id });
            """, ProjectType.DotNetNextJs);

        output.Should().Be("""
            public class ProductRepository
            {
                public async Task<Product?> GetByIdAsync(Guid id)
                {
                    await using var conn = await _db.OpenConnectionAsync();
                    return await conn.QuerySingleOrDefaultAsync<Product>(
                        "SELECT * FROM products WHERE id = @id", new { id });
                }
            }
            """);
    }

    [Fact]
    public async Task FillZonesAsync_TwoZonesInOneFile_EachIndentedToItsOwnMarker()
    {
        var template = """
            [[LLM_INJECTION_START: Imports]]
            [[LLM_INJECTION_END: Imports]]


            class Product(Base):
                [[LLM_INJECTION_START: Columns]]
                [[LLM_INJECTION_END: Columns]]

            """;

        var output = await FillOneFileAsync("app/models/product.py", template, zone => zone == "Imports"
            ? "    from sqlalchemy import String"
            : "name = Column(String, nullable=False)\nprice = Column(Numeric(10, 2))");

        output.Should().Be("""
            from sqlalchemy import String


            class Product(Base):
                name = Column(String, nullable=False)
                price = Column(Numeric(10, 2))

            """);
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

    [Fact]
    public async Task FillZonesAsync_ParallelDispatch_BeatsSerialBaseline()
    {
        // 24 zones × 100ms simulated LLM latency. Serial would take 2400ms.
        // Parallelism is asserted structurally (observed in-flight overlap) rather
        // than by wall-clock speedup — timing thresholds flake on shared CI runners.
        const int zoneCount = 24;
        const int simulatedLatencyMs = 100;
        const int concurrency = 6;

        var rendered = new Dictionary<string, string>();
        for (var i = 0; i < zoneCount; i++)
        {
            rendered[$"file_{i}.cs"] = $"[[LLM_INJECTION_START: Z{i}]]\n[[LLM_INJECTION_END: Z{i}]]";
        }

        var inFlight = 0;
        var maxInFlight = 0;
        var engine = BuildEngine(async _ =>
        {
            var current = Interlocked.Increment(ref inFlight);
            int observedMax;
            while (current > (observedMax = Volatile.Read(ref maxInFlight)))
            {
                Interlocked.CompareExchange(ref maxInFlight, current, observedMax);
            }

            try
            {
                await Task.Delay(simulatedLatencyMs);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }

            return new LlmResponse("// done", 0, 0, "stub");
        }, options: new InjectionEngineOptions { MaxConcurrency = concurrency });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null);
        sw.Stop();

        var serialBaselineMs = zoneCount * simulatedLatencyMs;
        var theoreticalParallelMs = (int)Math.Ceiling((double)zoneCount / concurrency) * simulatedLatencyMs;

        maxInFlight.Should().BeGreaterThan(1,
            "dispatch must overlap zone calls instead of running them serially");
        maxInFlight.Should().BeLessThanOrEqualTo(concurrency,
            $"in-flight calls must be capped at MaxConcurrency ({concurrency})");
        sw.ElapsedMilliseconds.Should().BeLessThan(serialBaselineMs,
            $"even a noisy runner must beat the fully-serial {serialBaselineMs}ms baseline");
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(theoreticalParallelMs,
            $"physics floor: at least {theoreticalParallelMs}ms for {zoneCount} calls at concurrency {concurrency}");
    }

    [Fact]
    public async Task FillZonesAsync_TokenAccounting_AggregatesAcrossZones()
    {
        var rendered = new Dictionary<string, string>();
        for (var i = 0; i < 5; i++)
        {
            rendered[$"file_{i}.cs"] = $"[[LLM_INJECTION_START: Z{i}]]\n[[LLM_INJECTION_END: Z{i}]]";
        }

        var engine = BuildEngine(_ => Task.FromResult(
            new LlmResponse("// ok", InputTokens: 100, OutputTokens: 50, Model: "claude-sonnet-5-5")));

        var result = await engine.FillZonesAsync(rendered, OneEntitySchema(), OneEntityVars(), ProjectType.DotNetNextJs, null);

        result.ZonesFilled.Should().Be(5);
        result.TotalInputTokens.Should().Be(500);
        result.TotalOutputTokens.Should().Be(250);
        result.Model.Should().Be("claude-sonnet-5-5");
    }

    private sealed class StubLlmClient(Func<string, Task<LlmResponse>> handler) : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, LlmCallOptions? options = null, CancellationToken ct = default)
            => handler(userPrompt);
    }
}
