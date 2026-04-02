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
builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();

builder.Services.AddHttpClient(AnthropicLlmClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient(SupabaseDeliveryService.HttpClientName);

builder.Services.AddHostedService<CompileWorkerService>();

var host = builder.Build();
host.Run();
