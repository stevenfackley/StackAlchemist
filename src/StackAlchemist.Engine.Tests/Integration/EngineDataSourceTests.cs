using FluentAssertions;
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
/// Host-level check of the qavren-db wiring in Program.cs: DATABASE_URL selects the Postgres
/// data source and stores, and its absence leaves the Supabase-only host untouched.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class EngineDataSourceTests : IDisposable
{
    private const string EnvVar = "DATABASE_URL";
    private const string SupabaseUrlVar = "NEXT_PUBLIC_SUPABASE_URL";
    private const string SupabaseKeyVar = "SUPABASE_SERVICE_ROLE_KEY";

    // Blank rather than removed: Program.cs loads the nearest .env with NoClobber, which fills a
    // removed variable back in from a local stub, while Program's Ev() reads whitespace as unset.
    private const string Blank = " ";

    private readonly Dictionary<string, string?> _original =
        new[] { EnvVar, SupabaseUrlVar, SupabaseKeyVar }.ToDictionary(v => v, Environment.GetEnvironmentVariable);

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

    // Assumes no .env up the tree sets DATABASE_URL: Program.cs loads the nearest one via DotNetEnv's TraversePath().
    [Fact]
    public void Without_DatabaseUrl_no_data_source_is_registered()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        using var factory = new EngineWebApplicationFactory();

        factory.Services.GetService<NpgsqlDataSource>().Should().BeNull();
        factory.Services.GetRequiredService<IDeliveryService>().Should().BeOfType<SupabaseDeliveryService>();
    }

    [Fact]
    public void Without_DatabaseUrl_the_Supabase_pair_registers_the_Supabase_billing_store()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        Environment.SetEnvironmentVariable(SupabaseUrlVar, "http://127.0.0.1:1");
        Environment.SetEnvironmentVariable(SupabaseKeyVar, "service-role-key");
        using var factory = new EngineWebApplicationFactory();

        factory.Services.GetService<IBillingStore>().Should().BeOfType<SupabaseBillingStore>();
    }

    [Theory]
    [InlineData(Blank, Blank)]
    [InlineData(Blank, "service-role-key")]
    [InlineData("http://127.0.0.1:1", Blank)]
    public void Without_DatabaseUrl_or_the_whole_Supabase_pair_no_billing_store_is_registered(string url, string key)
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        Environment.SetEnvironmentVariable(SupabaseUrlVar, url);
        Environment.SetEnvironmentVariable(SupabaseKeyVar, key);
        using var factory = new EngineWebApplicationFactory();

        factory.Services.GetService<IBillingStore>().Should().BeNull();
        // The Stripe paths still resolve: their factories pass the missing store as null.
        factory.Services.GetRequiredService<IStripeWebhookHandler>().Should().BeOfType<StripeWebhookHandler>();
        factory.Services.GetRequiredService<IRefundService>().Should().BeOfType<StripeRefundService>();
    }
}
