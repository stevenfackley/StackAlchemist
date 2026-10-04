using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Data;

/// <summary>
/// The data-layer registrations the Engine and Worker hosts share, so the two cannot drift apart
/// (#426: the Worker once registered a delivery service whose dependencies only the Engine provided).
/// </summary>
public static class StoreRegistration
{
    /// <summary>
    /// Registers the store selected by <paramref name="databaseUrl"/> (the raw DATABASE_URL):
    /// <list type="bullet">
    /// <item>Set: the qavren-db data source with <see cref="PostgresDeliveryService"/> and
    /// <see cref="PostgresBillingStore"/>.</item>
    /// <item>Unset outside Production: <see cref="NoOpDeliveryService"/>, and no
    /// <see cref="IBillingStore"/>. The Stripe webhook and refund take the billing store as
    /// optional and run without an idempotency log.</item>
    /// <item>Unset in Production: throws, because a host without a store would accept work it
    /// cannot record.</item>
    /// </list>
    /// <see cref="IPendingWriteBuffer"/> is registered in every case: the Postgres service buffers
    /// failed terminal writes in it, and the Engine's reconciler drains it.
    /// <para>
    /// Callers pass the environment variable itself, not a value read back from IConfiguration, so a
    /// stray <c>ConnectionStrings__Db</c> or appsettings entry cannot select the store while
    /// bypassing the pooler options <see cref="PostgresUrl"/> adds.
    /// </para>
    /// </summary>
    /// <exception cref="FormatException">DATABASE_URL is set but is not a postgres URI.</exception>
    /// <exception cref="InvalidOperationException">DATABASE_URL is unset in Production.</exception>
    public static IServiceCollection AddGenerationStore(
        this IServiceCollection services, string? databaseUrl, IHostEnvironment environment)
    {
        var connectionString = PostgresUrl.ToNpgsqlConnectionString(databaseUrl);
        if (connectionString is null && environment.IsProduction())
            throw new InvalidOperationException(
                "Required environment variable 'DATABASE_URL' is not set. " +
                "Set it to the qavren-db URL before starting in Production; there is no store otherwise.");

        services.AddSingleton<IPendingWriteBuffer, PendingWriteBuffer>();

        if (connectionString is null)
        {
            services.AddSingleton<IDeliveryService, NoOpDeliveryService>();
            return services;
        }

        services.AddNpgsqlDataSource(connectionString, dsb => dsb.ConnectionStringBuilder.MaxPoolSize = 10);
        services.AddSingleton<IDeliveryService, PostgresDeliveryService>();
        services.AddSingleton<IBillingStore, PostgresBillingStore>();
        return services;
    }
}
