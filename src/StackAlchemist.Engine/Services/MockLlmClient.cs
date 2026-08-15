using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Returns a hardcoded valid LLM response for running the pipeline without an API key.
///
/// The paths below are the contract, not decoration: they are the ones
/// <c>Prompts/V1-generation.md</c> asks for and the ones the real
/// <c>V1-DotNet-NextJs</c> tree actually has. This client used to emit
/// <c>src/Models/…</c> / <c>src/types/index.ts</c> — paths that exist nowhere in that tree —
/// so anyone exercising the pipeline offline saw the orphaned-<c>src/</c> layout and took it
/// for correct output.
///
/// The root namespace is read out of the prompt for the same reason. It is NOT a constant:
/// <c>GenerationOrchestrator.BuildVariables</c> derives it from the schema, so the csproj
/// rendered for an invoicing app is <c>InvoiceHub</c>, not <c>GeneratedApp</c>, and files
/// declaring <c>namespace GeneratedApp.Repositories;</c> reference <c>GeneratedApp.Models</c>
/// and <c>GeneratedApp.Infrastructure</c> — namespaces that exist in no tree ever rendered
/// (CS0234 / CS0246). That used to be harmless only because those files landed at the archive
/// root where nothing compiled them; now that they are routed into <c>dotnet/</c> they are
/// compiled, and <c>Program.cs</c> makes this client the fallback whenever
/// <c>ANTHROPIC_API_KEY</c> is unset — so a key misconfiguration would turn every generation
/// into a build failure, three retries, and a Compile Guarantee refund. Reading the namespace
/// back out of the prompt is also what a real model does with the same instruction, which is
/// the point of a stand-in.
/// </summary>
public sealed partial class MockLlmClient : ILlmClient
{
    /// <summary>Placeholder substituted with the resolved root namespace before returning.</summary>
    private const string RootNamespaceToken = "__ROOT_NS__";

    /// <summary>Used when the prompt states no namespace — the same fallback the orchestrator uses.</summary>
    private const string DefaultRootNamespace = "GeneratedApp";

    public Task<LlmResponse> GenerateAsync(
        string systemPrompt, string userPrompt, LlmCallOptions? options = null, CancellationToken ct = default)
    {
        // A minimal but compilable set of files for a "Product" entity.
        var response = """
            [[FILE:dotnet/Models/Product.cs]]
            namespace __ROOT_NS__.Models;

            public record Product
            {
                public Guid Id { get; init; }
                public string Name { get; init; } = string.Empty;
                public decimal Price { get; init; }
                public DateTime CreatedAt { get; init; }
            }

            public record CreateProductRequest(string Name, decimal Price);
            [[END_FILE]]
            [[FILE:dotnet/Repositories/ProductRepository.cs]]
            using Dapper;
            using __ROOT_NS__.Infrastructure;
            using __ROOT_NS__.Models;

            namespace __ROOT_NS__.Repositories;

            public interface IProductRepository
            {
                Task<IEnumerable<Product>> GetAllAsync();
                Task<Product?> GetByIdAsync(Guid id);
                Task<Product> CreateAsync(CreateProductRequest request);
            }

            public sealed class ProductRepository(IDbConnectionFactory db) : IProductRepository
            {
                public async Task<IEnumerable<Product>> GetAllAsync()
                {
                    using var conn = db.CreateConnection();
                    return await conn.QueryAsync<Product>("SELECT * FROM products ORDER BY created_at DESC");
                }

                public async Task<Product?> GetByIdAsync(Guid id)
                {
                    using var conn = db.CreateConnection();
                    return await conn.QueryFirstOrDefaultAsync<Product>(
                        "SELECT * FROM products WHERE id = @Id", new { Id = id });
                }

                public async Task<Product> CreateAsync(CreateProductRequest request)
                {
                    using var conn = db.CreateConnection();
                    var id = Guid.NewGuid();
                    await conn.ExecuteAsync(
                        "INSERT INTO products (id, name, price, created_at) VALUES (@Id, @Name, @Price, @CreatedAt)",
                        new { Id = id, request.Name, request.Price, CreatedAt = DateTime.UtcNow });
                    return (await GetByIdAsync(id))!;
                }
            }
            [[END_FILE]]
            [[FILE:dotnet/Controllers/ProductEndpoints.cs]]
            using __ROOT_NS__.Models;
            using __ROOT_NS__.Repositories;

            namespace __ROOT_NS__.Controllers;

            public static class ProductEndpoints
            {
                public static void MapProductEndpoints(this WebApplication app)
                {
                    var group = app.MapGroup("/api/v1/products").WithTags("Product");

                    group.MapGet("/", async (IProductRepository repo) =>
                        Results.Ok(await repo.GetAllAsync()));

                    group.MapGet("/{id:guid}", async (Guid id, IProductRepository repo) =>
                        await repo.GetByIdAsync(id) is { } product
                            ? Results.Ok(product)
                            : Results.NotFound());

                    group.MapPost("/", async (CreateProductRequest request, IProductRepository repo) =>
                    {
                        var product = await repo.CreateAsync(request);
                        return Results.Created($"/api/v1/products/{product.Id}", product);
                    });
                }
            }
            [[END_FILE]]
            [[FILE:__zone__/RepositoryRegistrations]]
            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            [[END_FILE]]
            [[FILE:__zone__/RouteRegistrations]]
            app.MapProductEndpoints();
            [[END_FILE]]
            [[FILE:dotnet/Migrations/001_initial_schema.sql]]
            CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

            CREATE TABLE products (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                name TEXT NOT NULL,
                price NUMERIC(10,2) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            ALTER TABLE products ENABLE ROW LEVEL SECURITY;
            [[END_FILE]]
            [[FILE:nextjs/src/types/index.ts]]
            export interface Product {
              id: string;
              name: string;
              price: number;
              createdAt: string;
            }

            export type CreateProductInput = Omit<Product, "id" | "createdAt">;
            [[END_FILE]]
            [[FILE:nextjs/src/lib/api.ts]]
            import type { CreateProductInput, Product } from "@/types";

            const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

            export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
              const res = await fetch(`${API_URL}${path}`, {
                headers: { "Content-Type": "application/json", ...init?.headers },
                ...init,
              });
              if (!res.ok) throw new Error(`API error ${res.status}: ${await res.text()}`);
              return res.json() as Promise<T>;
            }

            export async function getProducts(): Promise<Product[]> {
              return apiFetch<Product[]>("/api/v1/products");
            }

            export async function createProduct(data: CreateProductInput): Promise<Product> {
              return apiFetch<Product>("/api/v1/products", {
                method: "POST",
                body: JSON.stringify(data),
              });
            }
            [[END_FILE]]
            [[FILE:nextjs/src/app/page.tsx]]
            export default function HomePage() {
              return (
                <main className="min-h-screen p-8">
                  <h1 className="text-2xl font-bold">__ROOT_NS__</h1>
                  <ul className="mt-6 space-y-2">
                    <li>
                      <a className="text-blue-600 underline" href="/api/v1/products">
                        Products
                      </a>
                    </li>
                  </ul>
                </main>
              );
            }
            [[END_FILE]]
            """;

        return Task.FromResult(new LlmResponse(
            response.Replace(RootNamespaceToken, ResolveRootNamespace(systemPrompt, userPrompt), StringComparison.Ordinal),
            InputTokens: 0,
            OutputTokens: 0,
            Model: "mock-llm"));
    }

    /// <summary>
    /// Pulls the root namespace out of the generation prompt — the single line both prompt
    /// sources emit verbatim (<c>PromptBuilderService.AppendDotNetNextJsLayout</c> writes it,
    /// and <c>Prompts/V1-generation.md</c> carries it as a substituted <c>{{PROJECT_NAME}}</c>).
    /// Falls back to the orchestrator's own default when the prompt states none, which is the
    /// name the template would have been rendered with in that case anyway.
    /// </summary>
    internal static string ResolveRootNamespace(params string?[] prompts)
    {
        foreach (var prompt in prompts)
        {
            if (string.IsNullOrEmpty(prompt))
                continue;

            var match = RootNamespaceRegex().Match(prompt);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return DefaultRootNamespace;
    }

    [GeneratedRegex(@"Root namespace:\s*`([A-Za-z_][A-Za-z0-9_]*)`")]
    private static partial Regex RootNamespaceRegex();
}
