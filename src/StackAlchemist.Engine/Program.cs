using System.IO.Abstractions;
using System.Threading.Channels;
using DotNetEnv;
using Stripe;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

// ── Load .env file ────────────────────────────────────────────────────────────
// TraversePath() walks up from cwd until it finds .env (e.g. solution root).
// NoClobber() means already-set environment variables (e.g. Docker, CI) take priority.
Env.NoClobber().TraversePath().Load();

// ── Map flat .env names → nested IConfiguration paths ────────────────────────
// The .env file uses SCREAMING_SNAKE_CASE; ASP.NET Core config uses Colon:Hierarchy.
// We bridge them here so services can keep reading config["Anthropic:ApiKey"] etc.
static string? Ev(string key)
{
    var v = Environment.GetEnvironmentVariable(key);
    return string.IsNullOrWhiteSpace(v) ? null : v;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    // LLM
    ["Anthropic:ApiKey"]              = Ev("ANTHROPIC_API_KEY"),
    // Cloudflare R2
    ["CloudflareR2:AccountId"]        = Ev("R2_ACCOUNT_ID"),
    ["CloudflareR2:AccessKeyId"]      = Ev("R2_ACCESS_KEY_ID"),
    ["CloudflareR2:SecretAccessKey"]  = Ev("R2_SECRET_ACCESS_KEY"),
    ["CloudflareR2:BucketName"]       = Ev("R2_BUCKET_NAME"),
    // Supabase
    ["Supabase:Url"]                  = Ev("NEXT_PUBLIC_SUPABASE_URL"),
    ["Supabase:ServiceRoleKey"]       = Ev("SUPABASE_SERVICE_ROLE_KEY"),
    // Stripe
    ["Stripe:PublishableKey"]         = Ev("NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY"),
    ["Stripe:SecretKey"]              = Ev("STRIPE_SECRET_KEY"),
    ["Stripe:WebhookSecret"]          = Ev("STRIPE_WEBHOOK_SECRET"),
}.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.Timeout = TimeSpan.FromMinutes(5); // LLM calls can be long
});

builder.Services.AddHttpClient(SupabaseDeliveryService.HttpClientName);

// ── File system abstraction ──────────────────────────────────────────────────
builder.Services.AddSingleton<IFileSystem>(new FileSystem());

// ── Template provider ────────────────────────────────────────────────────────
// Resolve templates relative to the solution's Templates directory.
var templatesRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StackAlchemist.Templates"));
if (!Directory.Exists(templatesRoot))
{
    // Fallback for running from project root
    templatesRoot = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "..", "StackAlchemist.Templates"));
}

builder.Services.AddSingleton<ITemplateProvider>(sp =>
    new TemplateProvider(sp.GetRequiredService<IFileSystem>(), templatesRoot));

// ── Core engine services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IReconstructionService, ReconstructionService>();
builder.Services.AddSingleton<ITierGatingService, TierGatingService>();
builder.Services.AddSingleton<ISchemaExtractionService, SchemaExtractionService>();
builder.Services.AddSingleton<IPromptBuilderService, PromptBuilderService>();

// ── LLM client — AnthropicLlmClient when API key is set, MockLlmClient otherwise ──
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"];
if (!string.IsNullOrWhiteSpace(anthropicApiKey))
{
    builder.Services.AddSingleton<ILlmClient, AnthropicLlmClient>();
}
else
{
    builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
}

// ── Phase 4 delivery services ─────────────────────────────────────────────────
builder.Services.AddSingleton<IR2UploadService, CloudflareR2UploadService>();
builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();

// ── Compile service ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICompileService, CompileService>();

// ── In-process job queue (Channel) + background compile worker ───────────────
// Phase 4: Engine and Worker run in the same process sharing this Channel.
// Future: swap Channel for a persistent queue (Redis Streams, RabbitMQ) and
//         deploy the Worker as a separate host.
var channel = Channel.CreateUnbounded<GenerationContext>();
builder.Services.AddSingleton(channel.Writer);
builder.Services.AddSingleton(channel.Reader);

// Register the compile worker as an in-process background service.
builder.Services.AddHostedService<CompileWorkerService>();

// ── Orchestrator ──────────────────────────────────────────────────────────────
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

// ─── Endpoints ────────────────────────────────────────────────────────────────

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

// ── Schema extraction (Simple Mode) ──────────────────────────────────────
app.MapPost("/api/extract-schema", async (
    ExtractSchemaRequest request,
    IPromptBuilderService promptBuilder,
    ILlmClient llmClient,
    ISchemaExtractionService schemaExtractor,
    IDeliveryService delivery,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    logger.LogInformation("Schema extraction requested for generation {Id}", request.GenerationId);

    // Update status → extracting_schema
    await delivery.UpdateStatusAsync(request.GenerationId, "extracting_schema", ct);

    try
    {
        var systemPrompt = promptBuilder.BuildSchemaExtractionPrompt(request.Prompt);
        var llmResponse = await llmClient.GenerateAsync(
            systemPrompt,
            request.Prompt,
            ct);

        var schema = schemaExtractor.ParseExtractionResponse(llmResponse);

        // Persist extracted schema to Supabase
        await delivery.UpdateSchemaAsync(request.GenerationId, schema, ct);

        logger.LogInformation(
            "Schema extracted for {Id}: {Count} entities",
            request.GenerationId, schema.Entities.Count);

        return Results.Ok(new ExtractSchemaResponse
        {
            GenerationId = request.GenerationId,
            Schema = schema,
        });
    }
    catch (SchemaExtractionException ex)
    {
        logger.LogWarning("Schema extraction failed for {Id}: {Msg}", request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (SchemaValidationException ex)
    {
        logger.LogWarning("Schema validation failed for {Id}: {Msg}", request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ExtractSchema")
.WithSummary("Extract a structured schema from a natural-language prompt via LLM.");

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

    // Idempotency — the Supabase generations table deduplicates on generationId.
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

        var prompt = session.Metadata?.GetValueOrDefault("prompt");
        var generationId = session.Metadata?.GetValueOrDefault("generationId")
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
