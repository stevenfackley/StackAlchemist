using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackAlchemist.Engine.Services;
using StackAlchemist.Worker;

namespace StackAlchemist.Worker.Tests;

/// <summary>
/// Builds the Worker host's container from <see cref="WorkerServices.AddWorkerServices"/>, the
/// method its Program.cs calls, with every registration validated at build time. Regression test
/// for #426: the host registered <see cref="PostgresDeliveryService"/> without the
/// <see cref="IPendingWriteBuffer"/> it needs, and nothing built the container to notice.
/// </summary>
public sealed class WorkerHostTests
{
    private const string DatabaseUrl = "postgres://u:p@localhost:5432/x?sslmode=require";

    private static IHost BuildHost(string environment, string? databaseUrl)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        // Validate every registration (the hosted CompileWorkerService included), whatever the
        // environment's default.
        builder.ConfigureContainer(new DefaultServiceProviderFactory(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
        builder.AddWorkerServices(databaseUrl);
        return builder.Build();
    }

    [Fact]
    public void With_DatabaseUrl_the_host_builds_and_resolves_the_Postgres_delivery_service()
    {
        using var host = BuildHost(Environments.Production, DatabaseUrl);

        host.Services.GetRequiredService<IDeliveryService>().Should().BeOfType<PostgresDeliveryService>();
        host.Services.GetService<IBillingStore>().Should().BeOfType<PostgresBillingStore>();
        host.Services.GetServices<IHostedService>().Should().ContainSingle()
            .Which.Should().BeOfType<CompileWorkerService>();
    }

    [Fact]
    public void Without_DatabaseUrl_outside_Production_the_host_builds_on_the_no_op_store()
    {
        using var host = BuildHost(Environments.Development, databaseUrl: null);

        host.Services.GetRequiredService<IDeliveryService>().Should().BeOfType<NoOpDeliveryService>();
        host.Services.GetService<IBillingStore>().Should().BeNull();
        host.Services.GetRequiredService<IRefundService>().Should().BeOfType<StripeRefundService>();
    }

    [Fact]
    public void Without_DatabaseUrl_in_Production_the_host_refuses_to_build()
    {
        var build = () => BuildHost(Environments.Production, databaseUrl: null);

        build.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("DATABASE_URL");
    }
}
