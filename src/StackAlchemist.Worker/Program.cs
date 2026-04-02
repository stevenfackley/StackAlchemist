// StackAlchemist Worker Service
// Compile-Guarantee Worker: takes generated code from the queue,
// runs dotnet build + npm build, and retries with error context on failure.
// Stub — full implementation in a future phase.

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WorkerHeartbeatService>();

var host = builder.Build();
host.Run();

sealed class WorkerHeartbeatService(ILogger<WorkerHeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StackAlchemist worker started in stub mode.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
