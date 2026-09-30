// StackAlchemist.Worker — Phase 4 note
//
// All compile pipeline services (ICompileService, CompileService, CompileWorkerService)
// have been promoted to StackAlchemist.Engine.Services so they run in-process with the
// Engine host, sharing the same Channel<GenerationContext>.
//
// This host is preserved as a deployment option for future scale-out:
//   • Replace Channel with Redis Streams or RabbitMQ
//   • Deploy Worker separately from Engine
//   • Register ICompileService, IR2UploadService, IDeliveryService here

using System.Threading.Channels;
using Npgsql;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

var builder = Host.CreateApplicationBuilder(args);

// Compile pipeline (mirrors Engine registration for standalone mode)
var channel = Channel.CreateUnbounded<GenerationContext>();
builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
builder.Services.AddSingleton<IReconstructionService, ReconstructionService>();
builder.Services.AddSingleton<IR2UploadService, CloudflareR2UploadService>();

// Same selection as the Engine host: qavren-db when DATABASE_URL is set.
var dbConnectionString = PostgresUrl.ToNpgsqlConnectionString(Environment.GetEnvironmentVariable("DATABASE_URL"));
if (dbConnectionString is not null)
{
    builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(dbConnectionString).Build());
    builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>(); // Task 5 flips this to PostgresDeliveryService
}
else
{
    builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();
}

builder.Services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient(SupabaseDeliveryService.HttpClientName);
builder.Services.AddHttpClient(ResendEmailService.HttpClientName);

// CompileWorkerService depends on IEmailService — Resend when configured, NoOp otherwise.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Resend:ApiKey"]))
    builder.Services.AddSingleton<IEmailService, ResendEmailService>();
else
    builder.Services.AddSingleton<IEmailService, NoOpEmailService>();

builder.Services.AddHostedService<CompileWorkerService>();

var host = builder.Build();
host.Run();
