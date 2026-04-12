using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Returns a hardcoded valid LLM response for testing the pipeline without an API key.
/// Will be replaced by a real Anthropic SDK client in Phase 4.
/// </summary>
public sealed class MockLlmClient : ILlmClient
{
    public Task<LlmResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // Return a minimal but compilable set of files for a "Product" entity
        var response = """
            [[FILE:src/Models/Product.cs]]
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
            [[FILE:src/Repositories/ProductRepository.cs]]
            using Dapper;

            namespace GeneratedApp.Repositories;

            public interface IProductRepository
            {
                Task<IEnumerable<Product>> GetAllAsync();
                Task<Product?> GetByIdAsync(Guid id);
                Task<Product> CreateAsync(CreateProductRequest request);
            }

            public class ProductRepository : IProductRepository
            {
                private readonly IDbConnectionFactory _db;
                public ProductRepository(IDbConnectionFactory db) => _db = db;

                public async Task<IEnumerable<Product>> GetAllAsync()
                {
                    using var conn = _db.CreateConnection();
                    return await conn.QueryAsync<Product>("SELECT * FROM products ORDER BY created_at DESC");
                }

                public async Task<Product?> GetByIdAsync(Guid id)
                {
                    using var conn = _db.CreateConnection();
                    return await conn.QueryFirstOrDefaultAsync<Product>(
                        "SELECT * FROM products WHERE id = @Id", new { Id = id });
                }

                public async Task<Product> CreateAsync(CreateProductRequest request)
                {
                    using var conn = _db.CreateConnection();
                    var id = Guid.NewGuid();
                    await conn.ExecuteAsync(
                        "INSERT INTO products (id, name, price, created_at) VALUES (@Id, @Name, @Price, @CreatedAt)",
                        new { Id = id, request.Name, request.Price, CreatedAt = DateTime.UtcNow });
                    return (await GetByIdAsync(id))!;
                }
            }
            [[END_FILE]]
            [[FILE:src/Controllers/ProductEndpoints.cs]]
            using Microsoft.AspNetCore.Http.HttpResults;

            namespace GeneratedApp.Controllers;

            public static class ProductEndpoints
            {
                public static void Map(WebApplication app)
                {
                    var group = app.MapGroup("/api/v1/products").WithTags("Products");

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
            [[FILE:src/Migrations/001_initial_schema.sql]]
            CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

            CREATE TABLE products (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                name TEXT NOT NULL,
                price NUMERIC(10,2) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            ALTER TABLE products ENABLE ROW LEVEL SECURITY;
            [[END_FILE]]
            [[FILE:src/types/index.ts]]
            export interface Product {
              id: string;
              name: string;
              price: number;
              createdAt: string;
            }

            export interface CreateProductInput {
              name: string;
              price: number;
            }
            [[END_FILE]]
            [[FILE:src/lib/api.ts]]
            const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';

            async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
              const res = await fetch(`${API_URL}${path}`, init);
              if (!res.ok) throw new Error(`API error: ${res.status}`);
              return res.json() as Promise<T>;
            }

            export const getProducts = () => apiFetch<Product[]>('/api/v1/products');
            export const getProduct = (id: string) => apiFetch<Product>(`/api/v1/products/${id}`);
            export const createProduct = (data: CreateProductInput) =>
              apiFetch<Product>('/api/v1/products', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data),
              });
            [[END_FILE]]
            [[FILE:src/app/page.tsx]]
            export default function Home() {
              return (
                <main className="p-8">
                  <h1 className="text-3xl font-bold">GeneratedApp</h1>
                  <nav className="mt-4">
                    <a href="/products" className="text-blue-500 underline">Products</a>
                  </nav>
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
