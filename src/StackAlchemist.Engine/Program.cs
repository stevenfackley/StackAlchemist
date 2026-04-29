using System.IO.Abstractions;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using System.Threading.Channels;
using DotNetEnv;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Stripe;
using Stripe.Checkout;
using StackAlchemist.Engine.Middleware;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Engine.Telemetry;

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

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext()
       .Enrich.WithMachineName()
       .Enrich.WithThreadId()
       // Suppress chatty Microsoft/System noise — only warnings and above
       .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
       .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
       .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning);

    if (ctx.HostingEnvironment.IsProduction())
    {
        // Structured JSON for log aggregators (Seq, Datadog, CloudWatch, etc.)
        cfg.WriteTo.Console(new CompactJsonFormatter());
    }
    else
    {
        // Human-readable for local dev
        cfg.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
    }
});

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    // LLM
    ["Anthropic:ApiKey"]              = Ev("ANTHROPIC_API_KEY"),
    ["Anthropic:Model"]               = Ev("ANTHROPIC_MODEL"),
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
    // Engine hardening
    ["Engine:ServiceKey"]             = Ev("ENGINE_SERVICE_KEY"),
    ["Engine:AllowedOrigins"]         = Ev("NEXT_PUBLIC_APP_URL") ?? "http://localhost:3000",
    // Resend transactional email
    ["Resend:ApiKey"]                 = Ev("RESEND_API_KEY"),
    ["Resend:FromEmail"]              = Ev("RESEND_FROM_EMAIL"),
}.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

// ── Production startup validation ────────────────────────────────────────────
// Fail-fast on missing critical secrets so misconfigured deployments surface immediately.
if (builder.Environment.IsProduction())
{
    var required = new[]
    {
        ("ANTHROPIC_API_KEY",          builder.Configuration["Anthropic:ApiKey"]),
        ("SUPABASE_SERVICE_ROLE_KEY",  builder.Configuration["Supabase:ServiceRoleKey"]),
        ("R2_ACCESS_KEY_ID",           builder.Configuration["CloudflareR2:AccessKeyId"]),
        ("STRIPE_WEBHOOK_SECRET", builder.Configuration["Stripe:WebhookSecret"]),
        ("ENGINE_SERVICE_KEY",    builder.Configuration["Engine:ServiceKey"]),
    };
    foreach (var (name, value) in required)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Required environment variable '{name}' is not set. " +
                "Set it via user-secrets or environment before starting in Production.");
    }
}

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var stackAlchemistOtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("StackAlchemist.Engine"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(stackAlchemistOtlpEndpoint))
            tracing.AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(stackAlchemistOtlpEndpoint))
            tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(Meters.GenerationMeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(stackAlchemistOtlpEndpoint))
            metrics.AddConsoleExporter();

        if (!string.IsNullOrWhiteSpace(stackAlchemistOtlpEndpoint))
            metrics.AddOtlpExporter();
    });

// ── CORS ──────────────────────────────────────────────────────────────────────
// Allow Next.js frontend origin(s). Engine is called server-side from Next.js
// server actions, so CORS is mainly defensive; add browser origins here too
// in case direct client calls are introduced.
var allowedOrigins = (builder.Configuration["Engine:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ── Rate limiting ─────────────────────────────────────────────────────────────
// Per-IP fixed-window limits on expensive endpoints.
builder.Services.AddRateLimiter(rl =>
{
    // Generation: 5 per minute per IP — generous for real users, blocks spamming
    rl.AddFixedWindowLimiter("generate", o =>
    {
        o.Window          = TimeSpan.FromMinutes(1);
        o.PermitLimit     = 5;
        o.QueueLimit      = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Schema extraction: 15 per minute per IP
    rl.AddFixedWindowLimiter("extract", o =>
    {
        o.Window          = TimeSpan.FromMinutes(1);
        o.PermitLimit     = 15;
        o.QueueLimit      = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Stripe session creation: 3 per minute per IP
    rl.AddFixedWindowLimiter("stripe-session", o =>
    {
        o.Window          = TimeSpan.FromMinutes(1);
        o.PermitLimit     = 3;
        o.QueueLimit      = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rl.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded. Please wait before retrying." }, token);
    };
});

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.Timeout = TimeSpan.FromMinutes(5); // LLM calls can be long
});

builder.Services.AddHttpClient(SupabaseDeliveryService.HttpClientName);
builder.Services.AddHttpClient(StripeWebhookHandler.HttpClientName);
builder.Services.AddHttpClient(ResendEmailService.HttpClientName);

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
    Log.Warning("MockLlmClient is active. Generated applications will use mock output instead of Anthropic.");
}

// ── Phase 4 delivery services ─────────────────────────────────────────────────
builder.Services.AddSingleton<IR2UploadService, CloudflareR2UploadService>();
builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();

// ── Compile service ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IBuildStrategy, DotNetBuildStrategy>();
builder.Services.AddSingleton<IBuildStrategy, PythonReactBuildStrategy>();
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

// ── Stripe webhook handler ────────────────────────────────────────────────────
builder.Services.AddSingleton<IStripeWebhookHandler, StripeWebhookHandler>();

// ── Transactional email — Resend when configured, NoOp otherwise ─────────────
if (!string.IsNullOrWhiteSpace(builder.Configuration["Resend:ApiKey"]))
{
    builder.Services.AddSingleton<IEmailService, ResendEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, NoOpEmailService>();
    Log.Warning("RESEND_API_KEY not set — transactional emails are disabled (NoOpEmailService).");
}

var app = builder.Build();

// Correlation ID must be first so every subsequent log entry carries the ID.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("Frontend");
app.UseRateLimiter();

// ── Service key auth ──────────────────────────────────────────────────────────
// Protects /api/* (except /api/webhooks/stripe which uses Stripe signature auth)
// against unauthenticated callers. When ENGINE_SERVICE_KEY is unset the check
// is skipped so local dev / CI works without extra config.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/webhooks"))
    {
        var configuredKey = context.RequestServices
            .GetRequiredService<IConfiguration>()["Engine:ServiceKey"];

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var providedKey = context.Request.Headers["X-Engine-Key"].FirstOrDefault();
            if (providedKey != configuredKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                return;
            }
        }
    }

    await next(context);
});

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

app.MapHealthChecks("/health");

app.MapPost("/api/generate", async (
    GenerateRequest request,
    IGenerationOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var response = await orchestrator.EnqueueAsync(request, ct);
    return Results.Accepted($"/api/generate/{response.JobId}", response);
})
.WithName("Generate")
.WithSummary("Enqueue a code generation job.")
.RequireRateLimiting("generate");

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
        await delivery.UpdateTokenUsageAsync(
            request.GenerationId,
            llmResponse.InputTokens,
            llmResponse.OutputTokens,
            llmResponse.Model,
            ct);

        var schema = schemaExtractor.ParseExtractionResponse(llmResponse.Text);

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
.WithSummary("Extract a structured schema from a natural-language prompt via LLM.")
.RequireRateLimiting("extract");

// ── Stripe Checkout Session creation (paid tiers 1–3) ────────────────────────
// Called by the Next.js server action before redirecting the user to Stripe.
// Returns the hosted Checkout URL so the frontend can redirect without any
// client-side Stripe SDK dependency.
app.MapPost("/api/stripe/create-session", async (
    CreateCheckoutSessionRequest req,
    IConfiguration config,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var secretKey = config["Stripe:SecretKey"];
    if (string.IsNullOrWhiteSpace(secretKey))
        return Results.BadRequest(new { error = "Stripe is not configured on this server." });

    StripeConfiguration.ApiKey = secretKey;

    // Amount in cents for each tier
    var tierPricing = new Dictionary<int, (long Cents, string Name, string Description)>
    {
        [1] = (29_900, "StackAlchemist Blueprint",      "Schema JSON, OpenAPI spec, SQL migration scripts"),
        [2] = (59_900, "StackAlchemist Boilerplate",    "Full compilable .NET + Next.js codebase — Compile Guarantee"),
        [3] = (99_900, "StackAlchemist Infrastructure", "Codebase + AWS CDK, Helm Charts, Deployment Runbook"),
    };

    if (!tierPricing.TryGetValue(req.Tier, out var pricing))
        return Results.BadRequest(new { error = $"Invalid tier: {req.Tier}. Must be 1, 2, or 3." });

    try
    {
        var options = new SessionCreateOptions
        {
            Mode       = "payment",
            SuccessUrl = req.SuccessUrl,
            CancelUrl  = req.CancelUrl,
            LineItems  =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency   = "usd",
                        UnitAmount = pricing.Cents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name        = pricing.Name,
                            Description = pricing.Description,
                        },
                    },
                    Quantity = 1,
                },
            ],
            Metadata = new Dictionary<string, string>
            {
                ["generationId"] = req.GenerationId,
                ["tier"]         = req.Tier.ToString(),
                ["projectType"]  = req.ProjectType.ToString(),
                ["prompt"]       = req.Prompt ?? "",
            },
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        logger.LogInformation(
            "Stripe session {SessionId} created for generation {GenId} (tier {Tier})",
            session.Id, req.GenerationId, req.Tier);

        return Results.Ok(new CreateCheckoutSessionResponse
        {
            SessionId = session.Id,
            Url       = session.Url,
            ProjectType = req.ProjectType,
        });
    }
    catch (StripeException ex)
    {
        logger.LogError(ex, "Stripe session creation failed for generation {GenId}", req.GenerationId);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateStripeSession")
.WithSummary("Creates a Stripe Checkout Session for a paid tier and returns the hosted URL.")
.RequireRateLimiting("stripe-session");

// ── Stripe webhook ────────────────────────────────────────────────────────────
// Reads the raw body before routing so Stripe signature verification always
// receives the unmodified bytes.
app.MapPost("/api/webhooks/stripe", async (
    HttpRequest req,
    IStripeWebhookHandler webhookHandler,
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

    logger.LogInformation("Stripe event received: {Type} / {Id}", stripeEvent.Type, stripeEvent.Id);

    var result = await webhookHandler.HandleAsync(stripeEvent, ct);
    if (!result.Processed)
        logger.LogInformation("Stripe event {Id} skipped: {Reason}", stripeEvent.Id, result.Reason);

    return Results.Ok();
})
.WithName("StripeWebhook")
.WithSummary("Handles Stripe webhook events: checkout completion, payment failure, refund, and dispute.");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
