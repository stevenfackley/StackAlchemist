using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Runs alone: Program.cs reads DATABASE_URL from the process environment, which is global
/// state, so these tests must not overlap other tests that build an Engine host.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Engine host with DATABASE_URL in the process environment";
}

/// <summary>
/// Host-level check of the store wiring in Program.cs: DATABASE_URL selects the qavren-db data
/// source and stores; without it, a non-Production host runs on <see cref="NoOpDeliveryService"/>
/// with no billing store, and a Production host refuses to start.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class EngineDataSourceTests : IDisposable
{
    private const string EnvVar = "DATABASE_URL";

    // Production's other fail-fast secrets, set so the DATABASE_URL check is the one that fires.
    private static readonly string[] ProductionSecrets =
        ["ANTHROPIC_API_KEY", "R2_ACCESS_KEY_ID", "STRIPE_WEBHOOK_SECRET", "ENGINE_SERVICE_KEY"];

    // Blank rather than removed: Program.cs loads the nearest .env with NoClobber, which fills a
    // removed variable back in from a local stub, while Program's Ev() reads whitespace as unset.
    private const string Blank = " ";

    private readonly Dictionary<string, string?> _original =
        ProductionSecrets.Append(EnvVar).ToDictionary(v => v, Environment.GetEnvironmentVariable);

    public void Dispose()
    {
        foreach (var (name, value) in _original)
            Environment.SetEnvironmentVariable(name, value);
    }

    [Fact]
    public void DatabaseUrl_registers_a_pooler_ready_data_source()
    {
        Environment.SetEnvironmentVariable(EnvVar, "postgres://u:p@localhost:5432/x?sslmode=require");
        using var factory = new EngineWebApplicationFactory();

        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

        var b = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        b.MaxPoolSize.Should().Be(10);
        b.MaxAutoPrepare.Should().Be(0);
        b.NoResetOnClose.Should().BeTrue();
        b.SslMode.Should().Be(SslMode.Require);
        b.Host.Should().Be("localhost");
        b.Database.Should().Be("x");

        factory.Services.GetRequiredService<IDeliveryService>().Should().BeOfType<PostgresDeliveryService>();
        factory.Services.GetService<IBillingStore>().Should().BeOfType<PostgresBillingStore>();
    }

    [Fact]
    public void Without_DatabaseUrl_the_no_op_store_is_registered_and_the_Stripe_paths_still_resolve()
    {
        Environment.SetEnvironmentVariable(EnvVar, Blank);
        using var factory = new EngineWebApplicationFactory();

        factory.Services.GetService<NpgsqlDataSource>().Should().BeNull();
        factory.Services.GetRequiredService<IDeliveryService>().Should().BeOfType<NoOpDeliveryService>();
        factory.Services.GetService<IBillingStore>().Should().BeNull();
        // The reconciler's buffer is registered with the store, so it is there without one too.
        factory.Services.GetRequiredService<IPendingWriteBuffer>().Should().BeOfType<PendingWriteBuffer>();
        // The Stripe paths still resolve: their factories pass the missing store as null.
        factory.Services.GetRequiredService<IStripeWebhookHandler>().Should().BeOfType<StripeWebhookHandler>();
        factory.Services.GetRequiredService<IRefundService>().Should().BeOfType<StripeRefundService>();
    }

    [Fact]
    public void Production_without_DatabaseUrl_refuses_to_start()
    {
        foreach (var name in ProductionSecrets)
            Environment.SetEnvironmentVariable(name, "set-for-the-test");
        Environment.SetEnvironmentVariable(EnvVar, Blank);
        using var factory = new ProductionEngineFactory();

        var build = () => factory.Services;

        build.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("DATABASE_URL");
    }

    private sealed class ProductionEngineFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
    }
}
