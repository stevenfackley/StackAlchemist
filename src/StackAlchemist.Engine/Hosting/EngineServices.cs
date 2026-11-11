using System.IO.Abstractions;
using System.Threading.Channels;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Stripe;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Engine.Telemetry;

namespace StackAlchemist.Engine.Hosting;

/// <summary>The Engine's service registrations, grouped by what they are for. Program.cs calls each once.</summary>
public static class EngineServices
{
    public const string CorsPolicy = "Frontend";

    public static IServiceCollection AddEngineTelemetry(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var toConsole = environment.IsDevelopment() || string.IsNullOrWhiteSpace(otlpEndpoint);
        var toOtlp = !string.IsNullOrWhiteSpace(otlpEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("StackAlchemist.Engine"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (toConsole) tracing.AddConsoleExporter();
                if (toOtlp) tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(Meters.GenerationMeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (toConsole) metrics.AddConsoleExporter();
                if (toOtlp) metrics.AddOtlpExporter();
            });

        return services;
    }

    /// <summary>
    /// Allow Next.js frontend origin(s). Engine is called server-side from Next.js server actions,
    /// so CORS is mainly defensive; add browser origins here too in case direct client calls are
    /// introduced.
    /// </summary>
    public static IServiceCollection AddEngineCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["Engine:AllowedOrigins"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return services.AddCors(options =>
            options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()));
    }

    /// <summary>
    /// LLM clients + BYOK routing. The provider clients are always registered; RoutingLlmClient
    /// (the ILlmClient) picks the right one per generation from the resolved LlmCallOptions:
    ///   • null options → global default (Anthropic when ANTHROPIC_API_KEY is set, else the mock)
    ///   • Anthropic model → Anthropic Messages API (BYOK key when present, else the global key)
    ///   • openai/… or openrouter/… → OpenAI-compatible Chat Completions (BYOK required)
    /// LlmCredentialResolver reads the owning user's profile and decrypts the stored key engine-side —
    /// the plaintext key never crosses the wire, so this works for BOTH the free /api/generate path and
    /// the webhook-triggered paid path (both run through GenerationOrchestrator.EnqueueAsync).
    /// </summary>
    public static IServiceCollection AddLlm(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com");
            // Non-streaming calls with up to 20K output tokens (thinking included) on Sonnet 5.5.
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        // BYOK providers (OpenAI-compatible Chat Completions). Base addresses end in "/v1/" so the
        // client posts to the relative "chat/completions" path.
        services.AddHttpClient(OpenAiCompatibleLlmClient.OpenAiHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddHttpClient(OpenAiCompatibleLlmClient.OpenRouterHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<AnthropicLlmClient>();
        services.AddSingleton<OpenAiCompatibleLlmClient>();
        services.AddSingleton<MockLlmClient>();
        services.AddSingleton<ILlmClient, RoutingLlmClient>();
        services.AddSingleton<ByokKeyProtector>();
        services.AddSingleton<ILlmCredentialResolver, LlmCredentialResolver>();
        if (string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"]))
        {
            Log.Warning("ANTHROPIC_API_KEY not set — generations without a BYOK key will use MockLlmClient output.");
        }

        return services;
    }

    /// <summary>
    /// Everything between an accepted request and a delivered build: templates, the store, the
    /// Compile Guarantee build, the in-process queue with its worker and reconciler, and the
    /// orchestrator.
    /// </summary>
    public static IServiceCollection AddGenerationPipeline(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // ── File system abstraction ──────────────────────────────────────────────────
        services.AddSingleton<IFileSystem>(new FileSystem());

        // ── Template provider ────────────────────────────────────────────────────────
        // Templates are a runtime asset shipped alongside the published engine (Dockerfile
        // copies them to <BaseDirectory>/StackAlchemist.Templates). Resolution prefers an
        // explicit Templates:Root override, then the container layout, then the dev fallbacks.
        var templatesRoot = TemplatesRootResolver.Resolve(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            configuration["Templates:Root"],
            Directory.Exists);

        services.AddSingleton<ITemplateProvider>(sp =>
            new TemplateProvider(sp.GetRequiredService<IFileSystem>(), templatesRoot));

        // ── Core engine services ─────────────────────────────────────────────────────
        services.AddSingleton<IReconstructionService, ReconstructionService>();
        services.AddSingleton<ITierGatingService, TierGatingService>();
        services.AddSingleton<ISchemaExtractionService, SchemaExtractionService>();
        services.AddSingleton<IPromptBuilderService, PromptBuilderService>();

        // ── Phase 4 delivery services ─────────────────────────────────────────────────
        services.AddSingleton<IR2UploadService, CloudflareR2UploadService>();
        // The store: qavren-db when DATABASE_URL is set; outside Production without it, a no-op delivery
        // service and no billing store (the Stripe webhook and compile-guarantee refund then run without an
        // idempotency log). Production without it fails here. Also registers the pending-write buffer.
        services.AddGenerationStore(EngineEnvironment.Get("DATABASE_URL"), environment);

        // ── Compile service ───────────────────────────────────────────────────────────
        // Compile Guarantee builds run LLM-generated code. In the engine image they run as the
        // unprivileged build user, in a throwaway copy of the tree (#454; docker/engine/). Production
        // refuses to start without that rather than run generated code as root next to every secret.
        var (buildSandboxSettings, buildSandboxReason) = BuildSandboxSettings.FromEnvironment();
        if (buildSandboxSettings is null && environment.IsProduction())
            throw new InvalidOperationException(
                $"The Compile Guarantee build sandbox is unavailable: {buildSandboxReason}. " +
                "Production runs builds only inside it; run the engine image as built.");
        if (buildSandboxSettings is not null)
            services.AddSingleton(sp => new BuildSandbox(buildSandboxSettings, sp.GetRequiredService<ILogger<BuildSandbox>>()));

        // Per-step deadline for every build command (#455). <= 0 disables it.
        services.AddSingleton(sp =>
        {
            var minutes = sp.GetRequiredService<IConfiguration>().GetValue(
                "Generation:BuildStepTimeoutMinutes", (int)BuildStrategyOptions.DefaultStepTimeout.TotalMinutes);
            return new BuildStrategyOptions
            {
                StepTimeout = minutes > 0 ? TimeSpan.FromMinutes(minutes) : Timeout.InfiniteTimeSpan,
                Sandbox = sp.GetService<BuildSandbox>(),
            };
        });
        services.AddSingleton<IBuildStrategy, DotNetBuildStrategy>();
        services.AddSingleton<IBuildStrategy, PythonReactBuildStrategy>();
        services.AddSingleton<ICompileService, CompileService>();

        // ── In-process job queue (Channel) + background compile worker ───────────────
        // Phase 4: Engine and Worker run in the same process sharing this Channel.
        // Future: swap Channel for a persistent queue (Redis Streams, RabbitMQ) and
        //         deploy the Worker as a separate host.
        var channel = Channel.CreateUnbounded<GenerationContext>();
        services.AddSingleton(channel.Writer);
        services.AddSingleton(channel.Reader);

        // Register the compile worker as an in-process background service.
        services.AddHostedService<CompileWorkerService>();

        // Reconciliation support: the in-flight registry shields live jobs from the sweep. The
        // pending-write buffer it flushes is registered with the store (AddGenerationStore above).
        services.AddSingleton<IInFlightGenerationRegistry, InFlightGenerationRegistry>();
        services.AddSingleton(TimeProvider.System);

        // Periodic reconciler: re-enqueues rows orphaned by a restart (the Channel above is
        // in-memory, so any restart drops in-flight jobs) when attempt budget allows, fails
        // the rest, and flushes buffered critical writes. Runs at startup, then every
        // Generation:ReconcileIntervalMinutes.
        services.AddHostedService<GenerationReconciliationService>();

        // ── Swiss Cheese injection engine (per-zone parallel LLM dispatch) ───────────
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new InjectionEngineOptions
            {
                MaxConcurrency = config.GetValue("Generation:Injection:MaxConcurrency", 4),
                MaxAttemptsPerZone = config.GetValue("Generation:Injection:MaxAttemptsPerZone", 2),
            };
        });
        services.AddSingleton<IInjectionEngine, InjectionEngine>();

        // ── Orchestrator ──────────────────────────────────────────────────────────────
        services.AddSingleton<IGenerationOrchestrator, GenerationOrchestrator>();

        return services;
    }

    /// <summary>The Stripe webhook handler, the payment check in front of /api/generate, and refunds.</summary>
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        // Factories, because the billing store is optional and DI does not inject a missing service as null.
        services.AddSingleton<IStripeWebhookHandler>(sp => new StripeWebhookHandler(
            sp.GetRequiredService<IGenerationOrchestrator>(), sp.GetService<IBillingStore>(),
            sp.GetRequiredService<IEmailService>(), sp.GetRequiredService<ILogger<StripeWebhookHandler>>()));

        // ── Payment check in front of /api/generate ───────────────────────────────────
        services.AddSingleton<IPaidGenerationGate>(sp => new PaidGenerationGate(
            sp.GetRequiredService<IDeliveryService>(), sp.GetService<IBillingStore>(),
            sp.GetRequiredService<ILogger<PaidGenerationGate>>()));

        // ── Compile Guarantee automatic refund ────────────────────────────────────────
        // Stripe.net's RefundService reads the API key from the ambient StripeConfiguration
        // static at call time (same pattern the /api/stripe/create-session handler uses),
        // so the parameterless constructor is all the DI registration needs.
        services.AddSingleton<RefundService>();
        services.AddSingleton<IRefundService>(sp => new StripeRefundService(
            sp.GetService<IBillingStore>(), sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<RefundService>(), sp.GetRequiredService<ILogger<StripeRefundService>>()));

        // Refund + email for a paid build that ended with no retry left. The orchestrator and the
        // reconciler take it as an optional dependency; the compile worker builds its own.
        services.AddSingleton<IFailedBuildRefunder>(sp => new FailedBuildRefunder(
            sp.GetRequiredService<IRefundService>(), sp.GetRequiredService<IDeliveryService>(),
            sp.GetRequiredService<IEmailService>(), sp.GetRequiredService<ILogger<FailedBuildRefunder>>()));

        return services;
    }

    /// <summary>Transactional email — Resend when configured, NoOp otherwise.</summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(ResendEmailService.HttpClientName);

        if (!string.IsNullOrWhiteSpace(configuration["Resend:ApiKey"]))
        {
            services.AddSingleton<IEmailService, ResendEmailService>();
        }
        else
        {
            services.AddSingleton<IEmailService, NoOpEmailService>();
            Log.Warning("RESEND_API_KEY not set — transactional emails are disabled (NoOpEmailService).");
        }

        return services;
    }
}
