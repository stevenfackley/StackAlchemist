using System.Threading.Channels;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using Stripe;

namespace StackAlchemist.Worker;

/// <summary>
/// The Worker host's service registrations. Program.cs and Worker.Tests both build the container
/// from this method, so a dependency the host forgets to register fails a test instead of the
/// first deploy (#426).
/// </summary>
public static class WorkerServices
{
    /// <summary>
    /// Registers the compile pipeline and the store selected by <paramref name="databaseUrl"/>
    /// (the raw DATABASE_URL), exactly as the Engine host selects it.
    /// </summary>
    public static IHostApplicationBuilder AddWorkerServices(this IHostApplicationBuilder builder, string? databaseUrl)
    {
        var services = builder.Services;

        // Compile pipeline (mirrors Engine registration for standalone mode)
        var channel = Channel.CreateUnbounded<GenerationContext>();
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);

        services.AddSingleton<IBuildStrategy, DotNetBuildStrategy>();
        services.AddSingleton<IBuildStrategy, PythonReactBuildStrategy>();
        services.AddSingleton<ICompileService, CompileService>();
        services.AddSingleton<ILlmClient, MockLlmClient>();
        services.AddSingleton<IReconstructionService, ReconstructionService>();
        services.AddSingleton<IR2UploadService, CloudflareR2UploadService>();

        // The same store as the Engine host: qavren-db when DATABASE_URL is set, the no-op delivery
        // service outside Production otherwise. Includes the pending-write buffer the Postgres
        // delivery service needs.
        services.AddGenerationStore(databaseUrl, builder.Environment);
        services.AddSingleton<IInFlightGenerationRegistry, InFlightGenerationRegistry>();

        // Compile Guarantee refund, registered as the Engine registers it: a factory, because the
        // billing store is optional and DI does not inject a missing service as null.
        services.AddSingleton<RefundService>();
        services.AddSingleton<IRefundService>(sp => new StripeRefundService(
            sp.GetService<IBillingStore>(), sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<RefundService>(), sp.GetRequiredService<ILogger<StripeRefundService>>()));

        services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddHttpClient(ResendEmailService.HttpClientName);

        // CompileWorkerService depends on IEmailService — Resend when configured, NoOp otherwise.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Resend:ApiKey"]))
            services.AddSingleton<IEmailService, ResendEmailService>();
        else
            services.AddSingleton<IEmailService, NoOpEmailService>();

        services.AddHostedService<CompileWorkerService>();
        return builder;
    }
}
