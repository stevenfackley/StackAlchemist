using System.IO.Abstractions;
using System.Threading.Channels;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// File system abstraction
builder.Services.AddSingleton<IFileSystem>(new FileSystem());

// Template provider — resolve templates relative to the solution's Templates directory
var templatesRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StackAlchemist.Templates"));
if (!Directory.Exists(templatesRoot))
{
    // Fallback for running from project root
    templatesRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
        "..", "StackAlchemist.Templates"));
}

builder.Services.AddSingleton<ITemplateProvider>(sp =>
    new TemplateProvider(sp.GetRequiredService<IFileSystem>(), templatesRoot));

// Reconstruction service
builder.Services.AddSingleton<IReconstructionService, ReconstructionService>();

// LLM client — mock for Phase 3, swap to real Anthropic SDK in Phase 4
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();

// Job queue — in-process Channel for Phase 3, swap to persistent queue in Phase 4
var channel = Channel.CreateUnbounded<GenerationContext>();
builder.Services.AddSingleton(channel.Writer);
builder.Services.AddSingleton(channel.Reader);

// Orchestrator
builder.Services.AddSingleton<IGenerationOrchestrator, GenerationOrchestrator>();

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ─── Endpoints ──────────────────────────────────────────────────────────────

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "StackAlchemist.Engine",
}))
.WithName("GetHealth")
.WithSummary("Returns the liveness status for the engine.");

app.MapPost("/api/generate", async (
    GenerateRequest request,
    IGenerationOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var response = await orchestrator.EnqueueAsync(request, ct);
    return Results.Accepted($"/api/generate/{response.JobId}", response);
})
.WithName("Generate")
.WithSummary("Enqueue a code generation job.");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
