using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSubstitute;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Data;

/// <summary>
/// <see cref="StoreRegistration.AddGenerationStore"/>, the store selection the Engine and Worker
/// hosts share: Postgres with DATABASE_URL, the no-op delivery service without it outside
/// Production, and a refusal to start in Production without it.
/// </summary>
public sealed class StoreRegistrationTests
{
    private const string DatabaseUrl = "postgres://u:p@localhost:5432/x?sslmode=require";

    private static IHostEnvironment Env(string name)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    private static ServiceProvider Build(string? databaseUrl, string environment)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddGenerationStore(databaseUrl, Env(environment));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void DatabaseUrl_selects_the_Postgres_store(string environment)
    {
        using var sp = Build(DatabaseUrl, environment);

        sp.GetRequiredService<NpgsqlDataSource>().Should().NotBeNull();
        sp.GetRequiredService<IDeliveryService>().Should().BeOfType<PostgresDeliveryService>();
        sp.GetService<IBillingStore>().Should().BeOfType<PostgresBillingStore>();
        sp.GetRequiredService<IPendingWriteBuffer>().Should().BeOfType<PendingWriteBuffer>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_DatabaseUrl_outside_Production_the_no_op_store_is_registered_without_billing(string? databaseUrl)
    {
        using var sp = Build(databaseUrl, "Development");

        sp.GetService<NpgsqlDataSource>().Should().BeNull();
        sp.GetRequiredService<IDeliveryService>().Should().BeOfType<NoOpDeliveryService>();
        sp.GetService<IBillingStore>().Should().BeNull();
        sp.GetRequiredService<IPendingWriteBuffer>().Should().BeOfType<PendingWriteBuffer>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Without_DatabaseUrl_in_Production_registration_throws_and_registers_nothing(string? databaseUrl)
    {
        var services = new ServiceCollection();

        var register = () => services.AddGenerationStore(databaseUrl, Env("Production"));

        register.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("DATABASE_URL");
        services.Should().BeEmpty();
    }

    [Fact]
    public void A_malformed_DatabaseUrl_throws_rather_than_falling_back_to_the_no_op_store()
    {
        var services = new ServiceCollection();

        var register = () => services.AddGenerationStore("mysql://u:p@localhost/x", Env("Development"));

        register.Should().Throw<FormatException>();
        services.Should().BeEmpty();
    }
}
