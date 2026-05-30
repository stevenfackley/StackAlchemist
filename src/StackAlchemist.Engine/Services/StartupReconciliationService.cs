namespace StackAlchemist.Engine.Services;

/// <summary>
/// One-shot startup sweep that fails any generation rows orphaned in a non-terminal
/// state by a previous engine restart. The job queue is an in-process, in-memory
/// channel, so a restart drops every in-flight job; without this sweep those rows sit
/// at "pending"/"generating_code"/"building" forever and the user's UI never resolves.
/// Controlled by <c>Generation:StaleReconcileMinutes</c> (default 15); set ≤ 0 to disable.
/// </summary>
public sealed partial class StartupReconciliationService(
    IDeliveryService deliveryService,
    IConfiguration configuration,
    ILogger<StartupReconciliationService> logger) : BackgroundService
{
    private const int DefaultStaleMinutes = 15;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = int.TryParse(configuration["Generation:StaleReconcileMinutes"], out var m)
            ? m
            : DefaultStaleMinutes;

        if (minutes <= 0)
        {
            LogReconcileDisabled(logger);
            return;
        }

        try
        {
            // FailStaleNonTerminalAsync swallows its own errors and logs the row count,
            // so there is nothing to inspect on the return value here.
            await deliveryService.FailStaleNonTerminalAsync(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
        catch (Exception ex)
        {
            // Defensive: a startup sweep must never take the host down.
            LogReconcileCrashed(logger, ex);
        }
    }

    [LoggerMessage(EventId = 520, Level = LogLevel.Information, Message = "Stale-generation reconciliation disabled (Generation:StaleReconcileMinutes ≤ 0)")]
    private static partial void LogReconcileDisabled(ILogger logger);

    [LoggerMessage(EventId = 521, Level = LogLevel.Error, Message = "Stale-generation reconciliation crashed")]
    private static partial void LogReconcileCrashed(ILogger logger, Exception ex);
}
