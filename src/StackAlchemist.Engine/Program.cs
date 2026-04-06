using System.IO.Abstractions;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using System.Threading.Channels;
using DotNetEnv;
using Microsoft.AspNetCore.RateLimiting;
using Stripe;
using Stripe.Checkout;
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
    // Engine hardening
    ["Engine:ServiceKey"]             = Ev("ENGINE_SERVICE_KEY"),
    ["Engine:AllowedOrigins"]         = Ev("NEXT_PUBLIC_APP_URL") ?? "http://localhost:3000",
}.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

// ── Production startup validation ────────────────────────────────────────────
// Fail-fast on missing critical secrets so misconfigured deployments surface immediately.
if (builder.Environment.IsProduction())
{
    var required = new[]
    {
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
builder.Services.AddOpenApi();

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

var app = builder.Build();

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
        var projectType = Enum.TryParse<ProjectType>(
            session.Metadata?.GetValueOrDefault("projectType"),
            ignoreCase: true,
            out var parsedProjectType)
            ? parsedProjectType
            : ProjectType.DotNetNextJs;
        var mode = prompt is not null ? "simple" : "advanced";
        GenerationSchema? schema = null;
        GenerationPersonalization? personalization = null;

        // Recover the original generation payload so paid advanced-mode jobs keep their
        // saved schema and platform selection after redirecting through Stripe Checkout.
        var supabaseUrl = config["Supabase:Url"];
        var serviceRoleKey = config["Supabase:ServiceRoleKey"];
        if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            try
            {
                var generationEndpoint =
                    $"{supabaseUrl.TrimEnd('/')}/rest/v1/generations?id=eq.{generationId}&select=mode,prompt,schema_json,project_type,personalization_json";

                using var httpClient = new HttpClient();
                using var generationRequest = new HttpRequestMessage(HttpMethod.Get, generationEndpoint);
                generationRequest.Headers.Add("apikey", serviceRoleKey);
                generationRequest.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

                using var generationResponse = await httpClient.SendAsync(generationRequest, ct);
                generationResponse.EnsureSuccessStatusCode();

                var payload = await generationResponse.Content.ReadAsStringAsync(ct);
                using var document = System.Text.Json.JsonDocument.Parse(payload);

                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                    document.RootElement.GetArrayLength() > 0)
                {
                    var row = document.RootElement[0];

                    if (row.TryGetProperty("mode", out var modeElement) &&
                        modeElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        mode = modeElement.GetString() ?? mode;
                    }

                    if (row.TryGetProperty("prompt", out var promptElement) &&
                        promptElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                        string.IsNullOrWhiteSpace(prompt))
                    {
                        prompt = promptElement.GetString();
                    }

                    if (row.TryGetProperty("project_type", out var projectTypeElement) &&
                        projectTypeElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                        Enum.TryParse<ProjectType>(projectTypeElement.GetString(), ignoreCase: true, out var storedProjectType))
                    {
                        projectType = storedProjectType;
                    }

                    if (row.TryGetProperty("schema_json", out var schemaElement) &&
                        schemaElement.ValueKind != System.Text.Json.JsonValueKind.Null &&
                        schemaElement.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    {
                        schema = System.Text.Json.JsonSerializer.Deserialize<GenerationSchema>(schemaElement.GetRawText());
                    }

                    if (row.TryGetProperty("personalization_json", out var personalizationElement) &&
                        personalizationElement.ValueKind != System.Text.Json.JsonValueKind.Null &&
                        personalizationElement.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    {
                        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        personalization = System.Text.Json.JsonSerializer.Deserialize<GenerationPersonalization>(personalizationElement.GetRawText(), opts);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reload generation payload for {GenerationId}", generationId);
            }
        }

        // ── Insert transaction row into Supabase ─────────────────────────────
        if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            try
            {
                var txEndpoint = $"{supabaseUrl.TrimEnd('/')}/rest/v1/transactions";
                var txPayload = new
                {
                    stripe_session_id = session.Id,
                    tier              = tier,
                    amount            = session.AmountTotal ?? 0,
                    status            = "completed",
                };

                using var httpClient = new HttpClient();
                var txReq = new HttpRequestMessage(HttpMethod.Post, txEndpoint)
                {
                    Content = JsonContent.Create(txPayload),
                };
                txReq.Headers.Add("apikey", serviceRoleKey);
                txReq.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
                txReq.Headers.Add("Prefer", "return=minimal");
                await httpClient.SendAsync(txReq, ct);

                logger.LogInformation(
                    "Transaction row inserted for Stripe session {SessionId}", session.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to insert transaction for session {SessionId}", session.Id);
                // Non-fatal: still enqueue generation
            }
        }

        await orchestrator.EnqueueAsync(new GenerateRequest
        {
            GenerationId  = generationId,
            Mode          = mode,
            Tier          = tier,
            ProjectType   = projectType,
            Prompt        = prompt,
            Schema        = schema,
            Personalization = personalization,
        }, ct);
    }

    return Results.Ok();
})
.WithName("StripeWebhook")
.WithSummary("Handles Stripe checkout.session.completed events and enqueues generation jobs.");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
