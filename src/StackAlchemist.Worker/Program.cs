using System.Threading.Channels;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Job queue — shared Channel with Engine (in-process for Phase 3)
var channel = Channel.CreateUnbounded<GenerationContext>();
builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

// Services
builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
builder.Services.AddSingleton<IReconstructionService, ReconstructionService>();

// Background worker
builder.Services.AddHostedService<CompileWorkerService>();

var host = builder.Build();
host.Run();
