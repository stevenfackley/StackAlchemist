using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StackAlchemist.Engine.Tests.Integration;

public sealed class EngineHostTests : IClassFixture<EngineWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EngineHostTests(EngineWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsOkPayload()
    {
        var response = await _client.GetAsync("/healthz");
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().NotBeNull();
        payload!["status"].Should().Be("ok");
        payload["service"].Should().Be("StackAlchemist.Engine");
    }

    [Fact]
    public async Task Healthz_EchoesSuppliedCorrelationHeader()
    {
        const string correlationId = "test-correlation-id";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Fact]
    public async Task Healthz_MintsCorrelationHeaderWhenMissing()
    {
        var response = await _client.GetAsync("/healthz");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        var correlationId = values!.Single();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }
}

public sealed class EngineWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
