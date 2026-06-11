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
using StackAlchemist.Engine;
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

// ── Request size limit ────────────────────────────────────────────────────────
// The largest legitimate payload is a full advanced-mode schema (well under 1 MB).
// Kestrel's 30 MB default invites memory-exhaustion via bloated JSON bodies;
// oversized requests are rejected with 413 before deserialization.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 5 * 1024 * 1024);

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
        cfg.WriteTo.Console(
            formatProvider: System.Globalization.CultureInfo.InvariantCulture,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
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
// Templates are a runtime asset shipped alongside the published engine (Dockerfile
// copies them to <BaseDirectory>/StackAlchemist.Templates). Resolution prefers an
// explicit Templates:Root override, then the container layout, then the dev fallbacks.
var templatesRoot = TemplatesRootResolver.Resolve(
    AppContext.BaseDirectory,
    Directory.GetCurrentDirectory(),
    builder.Configuration["Templates:Root"],
    Directory.Exists);

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

// Reconciliation support: the in-flight registry shields live jobs from the sweep,
// and the pending-write buffer holds critical writes that exhausted their retries.
builder.Services.AddSingleton<IInFlightGenerationRegistry, InFlightGenerationRegistry>();
builder.Services.AddSingleton<IPendingWriteBuffer, PendingWriteBuffer>();
builder.Services.AddSingleton(TimeProvider.System);

// Periodic reconciler: re-enqueues rows orphaned by a restart (the Channel above is
// in-memory, so any restart drops in-flight jobs) when attempt budget allows, fails
// the rest, and flushes buffered critical writes. Runs at startup, then every
// Generation:ReconcileIntervalMinutes.
builder.Services.AddHostedService<GenerationReconciliationService>();

// ── Swiss Cheese injection engine (per-zone parallel LLM dispatch) ───────────
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new InjectionEngineOptions
    {
        MaxConcurrency = config.GetValue("Generation:Injection:MaxConcurrency", 4),
        MaxAttemptsPerZone = config.GetValue("Generation:Injection:MaxAttemptsPerZone", 2),
    };
});
builder.Services.AddSingleton<IInjectionEngine, InjectionEngine>();

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
    IDeliveryService delivery,
    IConfiguration config,
    IHostApplicationLifetime lifetime,
    ILogger<Program> logger) =>
{
    // The frontend caps prompts at 2,000 chars; a direct API call has no such cap and
    // every unchecked character is paid LLM context. Fail the row (which also refunds
    // the free-quota slot — the trigger excludes failed rows) and reject.
    var maxPromptChars = config.GetValue("Generation:MaxPromptChars", 5_000);
    if (request.Prompt is { Length: var len } && len > maxPromptChars)
    {
        var msg = $"Prompt exceeds the {maxPromptChars:N0} character limit. Shorten the description and retry.";
        await delivery.UpdateStatusAsync(
            request.GenerationId, "failed", lifetime.ApplicationStopping, msg, ErrorCategorizer.Schema);
        return Results.BadRequest(new { error = msg });
    }

    // Dispatch codegen to the background on the app-lifetime token — NOT the request's
    // CancellationToken (which binds to HttpContext.RequestAborted). A client or CF-tunnel
    // disconnect must not cancel an in-flight generation: a one-shot whole-app LLM call can
    // outlast the ~100s proxy timeout, and the frontend tracks progress via Supabase Realtime,
    // not this HTTP response. So we fire-and-forget and return Accepted immediately. Safe
    // because every engine service is registered AddSingleton (no scoped disposal at request
    // end). EnqueueAsync owns its own failure handling (marks the row failed); the catch here
    // only guards against an unexpected throw before that runs.
    _ = Task.Run(async () =>
    {
        try
        {
            await orchestrator.EnqueueAsync(request, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background generation dispatch failed for {GenerationId}", request.GenerationId);
        }
    });

    return Results.Accepted(
        $"/api/generate/{request.GenerationId}",
        new GenerateResponse
        {
            JobId = request.GenerationId,
            Status = "generating_code",
            ProjectType = request.ProjectType,
        });
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
    IConfiguration config,
    ILogger<Program> logger,
    IHostApplicationLifetime lifetime) =>
{
    // Run on the app-lifetime token, NOT HttpContext.RequestAborted. The frontend aborts
    // this request at 45s; binding the LLM call to RequestAborted made that abort cancel the
    // extraction and flip the row to "failed" at ~46s — the schema-extraction error screen
    // the user kept hitting. This is one short LLM turn: let it finish and persist the schema
    // even if the client already walked away.
    var ct = lifetime.ApplicationStopping;

    logger.SchemaExtractRequested(request.GenerationId);

    // Length is the only pre-LLM scale check, by design: the post-LLM size validation
    // (20 entities / 30 rels / 30 fields) is authoritative, and guessing entity counts
    // from prose is unreliable in both directions. The cap bounds the paid LLM context.
    var maxPromptChars = config.GetValue("Generation:MaxPromptChars", 5_000);
    if (request.Prompt.Length > maxPromptChars)
    {
        var msg = $"Prompt exceeds the {maxPromptChars:N0} character limit. Shorten the description and retry.";
        // Failing the row matters: it un-sticks pending and refunds the free-quota
        // slot (the quota trigger excludes failed rows).
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, msg, ErrorCategorizer.Schema);
        return Results.BadRequest(new { error = msg });
    }

    // CAS: pending/failed → extracting_schema. A double-submit (network retry, two
    // tabs) loses the claim and must not trigger a second paid LLM call.
    if (!await delivery.TryBeginExtractionAsync(request.GenerationId, ct))
    {
        return Results.Conflict(new
        {
            error = "Schema extraction is already in progress or this generation has completed.",
        });
    }

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

        // A max_tokens cutoff means the schema JSON is incomplete — parsing it would
        // produce a misleading "invalid JSON" error; fail with the real cause instead.
        // (After token recording: truncated calls still consumed tokens.)
        LlmResponseGuard.ThrowIfTruncated(llmResponse, "extracting the schema");

        var schema = schemaExtractor.ParseExtractionResponse(llmResponse.Text);

        // Persist extracted schema to Supabase
        await delivery.UpdateSchemaAsync(request.GenerationId, schema, ct);

        logger.SchemaExtracted(request.GenerationId, schema.Entities.Count);

        return Results.Ok(new ExtractSchemaResponse
        {
            GenerationId = request.GenerationId,
            Schema = schema,
        });
    }
    catch (TruncatedLlmResponseException ex)
    {
        logger.SchemaExtractFailed(request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message, ErrorCategorizer.Schema);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (SchemaExtractionException ex)
    {
        logger.SchemaExtractFailed(request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message, ErrorCategorizer.Schema);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (SchemaValidationException ex)
    {
        logger.SchemaValidationFailed(request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message, ErrorCategorizer.Schema);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        // Any unforeseen failure must still flip the row out of "extracting_schema",
        // otherwise it sits there forever and the UI never resolves. CancellationToken.None:
        // a cancelled/aborted request must still record its terminal state.
        logger.SchemaExtractFailed(request.GenerationId, ex.Message);
        await delivery.UpdateStatusAsync(
            request.GenerationId, "failed", CancellationToken.None, ex.Message, ErrorCategorizer.Categorize(ex));
        return Results.Problem("Schema extraction failed unexpectedly.");
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
                ["tier"]         = req.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["projectType"]  = req.ProjectType.ToString(),
                ["prompt"]       = req.Prompt ?? "",
            },
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        logger.StripeSessionCreated(session.Id, req.GenerationId, req.Tier);

        return Results.Ok(new CreateCheckoutSessionResponse
        {
            SessionId = session.Id,
            Url       = session.Url,
            ProjectType = req.ProjectType,
        });
    }
    catch (StripeException ex)
    {
        logger.StripeSessionCreationFailed(ex, req.GenerationId);
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
        logger.StripeSignatureVerifyFailed(ex.Message);
        return Results.Unauthorized();
    }

    logger.StripeEventReceived(stripeEvent.Type, stripeEvent.Id);

    var result = await webhookHandler.HandleAsync(stripeEvent, ct);

    // A retryable side-effect failure (idempotency log down, RPC failure, enqueue
    // failure) must surface as 5xx — Stripe only redelivers on non-2xx responses.
    if (result.Retry)
        return Results.StatusCode(StatusCodes.Status500InternalServerError);

    if (!result.Processed)
        logger.StripeEventSkipped(stripeEvent.Id, result.Reason);

    return Results.Ok();
})
.WithName("StripeWebhook")
.WithSummary("Handles Stripe webhook events: checkout completion, payment failure, refund, and dispute.");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
