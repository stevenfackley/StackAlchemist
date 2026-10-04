// StackAlchemist.Worker — Phase 4 note
//
// All compile pipeline services (ICompileService, CompileService, CompileWorkerService)
// have been promoted to StackAlchemist.Engine.Services so they run in-process with the
// Engine host, sharing the same Channel<GenerationContext>.
//
// This host is preserved as a deployment option for future scale-out:
//   • Replace Channel with Redis Streams or RabbitMQ
//   • Deploy Worker separately from Engine
//
// Its registrations live in WorkerServices.AddWorkerServices, which Worker.Tests also builds.

using StackAlchemist.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Read from the environment, not IConfiguration, exactly as the Engine host reads it.
builder.AddWorkerServices(Environment.GetEnvironmentVariable("DATABASE_URL"));

var host = builder.Build();
host.Run();
