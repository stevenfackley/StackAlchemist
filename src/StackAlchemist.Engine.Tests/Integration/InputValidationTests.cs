using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Disables the X-Engine-Key middleware: the repo .env (loaded by Program via
/// TraversePath) can carry a real ENGINE_SERVICE_KEY on dev machines, which would
/// 401 these requests before validation runs.
/// </summary>
public sealed class NoAuthEngineFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Engine:ServiceKey"] = "",
            }));
    }
}

/// <summary>
/// Endpoint-level input validation. Note: the Kestrel MaxRequestBodySize (5 MB → 413)
/// cannot be exercised here — WebApplicationFactory's TestServer bypasses Kestrel —
/// so these tests cover the application-level prompt-length checks.
/// </summary>
public sealed class InputValidationTests(NoAuthEngineFactory factory)
    : IClassFixture<NoAuthEngineFactory>
{
    [Fact]
    public async Task ExtractSchema_OversizedPrompt_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/extract-schema", new
        {
            generationId = Guid.NewGuid().ToString(),
            prompt = new string('x', 5_001),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("character limit");
    }

    [Fact]
    public async Task Generate_OversizedPrompt_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/generate", new
        {
            generationId = Guid.NewGuid().ToString(),
            mode = "simple",
            tier = 0,
            prompt = new string('x', 5_001),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("character limit");
    }

    [Fact]
    public async Task Generate_NormalPrompt_StillAccepted()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/generate", new
        {
            generationId = Guid.NewGuid().ToString(),
            mode = "simple",
            tier = 0,
            prompt = "Build a task tracking app",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}

/// <summary>
/// <c>/api/generate</c> with a billing store that knows of no payment: the paid tiers are refused
/// at the endpoint, and the free tier is not.
/// </summary>
public sealed class UnpaidGenerateTests(NoAuthEngineFactory factory) : IClassFixture<NoAuthEngineFactory>
{
    private HttpClient UnpaidClient() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            var billing = Substitute.For<IBillingStore>();
            billing.FindCompletedTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((EligibleTransaction?)null);
            services.RemoveAll<IBillingStore>();
            services.AddSingleton(billing);
        })).CreateClient();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Generate_PaidTierWithoutPayment_Returns402(int tier)
    {
        var response = await UnpaidClient().PostAsJsonAsync("/api/generate", new
        {
            generationId = Guid.NewGuid().ToString(),
            mode = "simple",
            tier,
            prompt = "Build a task tracking app",
        });

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await response.Content.ReadAsStringAsync()).Should().Contain("no completed payment");
    }

    [Fact]
    public async Task Generate_FreeTier_IsStillAccepted()
    {
        var response = await UnpaidClient().PostAsJsonAsync("/api/generate", new
        {
            generationId = Guid.NewGuid().ToString(),
            mode = "simple",
            tier = 0,
            prompt = "Build a task tracking app",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
