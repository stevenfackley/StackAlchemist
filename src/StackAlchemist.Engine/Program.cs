using DotNetEnv;
using Serilog;
using Serilog.Formatting.Compact;
using StackAlchemist.Engine.Endpoints;
using StackAlchemist.Engine.Hosting;
using StackAlchemist.Engine.Middleware;

// ── Load .env file ────────────────────────────────────────────────────────────
// TraversePath() walks up from cwd until it finds .env (e.g. solution root).
// NoClobber() means already-set environment variables (e.g. Docker, CI) take priority.
Env.NoClobber().TraversePath().Load();

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

// ── Configuration ─────────────────────────────────────────────────────────────
// Flat .env names → nested IConfiguration paths, then fail fast in Production on a missing secret.
builder.Configuration.AddEngineEnvironment();
if (builder.Environment.IsProduction())
    EngineEnvironment.RequireProductionSecrets(builder.Configuration);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services
    .AddEngineTelemetry(builder.Configuration, builder.Environment)
    .AddEngineCors(builder.Configuration)
    .AddEngineRateLimiting()
    .AddLlm(builder.Configuration)
    .AddGenerationPipeline(builder.Configuration, builder.Environment)
    .AddBilling()
    .AddEmail(builder.Configuration);

var app = builder.Build();

// ── Request pipeline ──────────────────────────────────────────────────────────
// Correlation ID must be first so every subsequent log entry carries the ID.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors(EngineServices.CorsPolicy);

app.UseEngineServiceKey();

// After the key check on purpose: a request without the service key is refused before it can
// spend a permit, and the X-Engine-User header the limiter partitions on is one the key vouches for.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "StackAlchemist.Engine",
}))
.WithName("GetHealth")
.WithSummary("Returns the liveness status for the engine.");

app.MapHealthChecks("/health");

app.MapGenerationEndpoints();
app.MapStripeEndpoints();

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
