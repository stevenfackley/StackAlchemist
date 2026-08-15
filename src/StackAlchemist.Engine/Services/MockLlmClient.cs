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
/// </summary>
public sealed class MockLlmClient : ILlmClient
{
    public Task<LlmResponse> GenerateAsync(
        string systemPrompt, string userPrompt, LlmCallOptions? options = null, CancellationToken ct = default)
    {
        // A minimal but compilable set of files for a "Product" entity.
        var response = """
            [[FILE:dotnet/Models/Product.cs]]
            namespace GeneratedApp.Models;

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
            using GeneratedApp.Infrastructure;
            using GeneratedApp.Models;

            namespace GeneratedApp.Repositories;

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
            using GeneratedApp.Models;
            using GeneratedApp.Repositories;

            namespace GeneratedApp.Controllers;

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
                  <h1 className="text-2xl font-bold">GeneratedApp</h1>
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
            response,
            InputTokens: 0,
            OutputTokens: 0,
            Model: "mock-llm"));
    }
}
