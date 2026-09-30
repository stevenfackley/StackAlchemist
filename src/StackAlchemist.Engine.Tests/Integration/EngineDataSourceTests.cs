using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
/// data source, and its absence leaves the Supabase-only host untouched.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class EngineDataSourceTests : IDisposable
{
    private const string EnvVar = "DATABASE_URL";
    private readonly string? _original = Environment.GetEnvironmentVariable(EnvVar);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvVar, _original);

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
    }

    // Assumes no .env up the tree sets DATABASE_URL: Program.cs loads the nearest one via DotNetEnv's TraversePath().
    [Fact]
    public void Without_DatabaseUrl_no_data_source_is_registered()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        using var factory = new EngineWebApplicationFactory();

        factory.Services.GetService<NpgsqlDataSource>().Should().BeNull();
    }
}
