using System.IO.Abstractions;
using System.Threading.Channels;
using Stripe;
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

// ── Phase 3 services ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<ITierGatingService, TierGatingService>();
builder.Services.AddSingleton<ISchemaExtractionService, SchemaExtractionService>();
builder.Services.AddSingleton<IPromptBuilderService, PromptBuilderService>();

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

// ── Stripe webhook ────────────────────────────────────────────────────────────
// Reads the raw body before routing so Stripe signature verification always
// receives the unmodified bytes.
app.MapPost("/api/webhooks/stripe", async (
    HttpRequest req,
    IGenerationOrchestrator orchestrator,
    IConfiguration config,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
    var signature = req.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

    string json;
    using (var reader = new StreamReader(req.Body))
        json = await reader.ReadToEndAsync(ct);

    Stripe.Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(
            json,
            signature,
            webhookSecret,
            throwOnApiVersionMismatch: false);
    }
    catch (StripeException ex)
    {
        logger.LogWarning("Stripe webhook signature verification failed: {Msg}", ex.Message);
        return Results.Unauthorized();
    }

    // Idempotency — downstream logic should deduplicate on stripeEvent.Id
    logger.LogInformation("Stripe event received: {Type} / {Id}", stripeEvent.Type, stripeEvent.Id);

    if (stripeEvent.Type == "checkout.session.completed"
        && stripeEvent.Data.Object is Stripe.Checkout.Session session)
    {
        if (!int.TryParse(
            session.Metadata?.GetValueOrDefault("tier") ?? "2",
            out var tier))
        {
            tier = 2;
        }

        var prompt        = session.Metadata?.GetValueOrDefault("prompt");
        var generationId  = session.Metadata?.GetValueOrDefault("generationId")
                            ?? Guid.NewGuid().ToString();

        await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId = generationId,
            Mode         = prompt is not null ? "simple" : "advanced",
            Tier         = tier,
            Prompt       = prompt,
        }, ct);
    }

    return Results.Ok();
})
.WithName("StripeWebhook")
.WithSummary("Handles Stripe checkout.session.completed events and enqueues generation jobs.");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
