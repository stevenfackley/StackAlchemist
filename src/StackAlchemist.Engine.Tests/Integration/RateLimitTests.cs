using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using StackAlchemist.Engine.Middleware;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The rate limits are per user, not one window for the whole product, and a request without the
/// service key never spends a permit. Every request here carries an oversized prompt: the limiter
/// runs before the handler, so the permit is spent, and the 400 means no generation is started.
/// Each test builds its own host, so each starts with fresh windows.
/// </summary>
public sealed class RateLimitTests
{
    private const int GeneratePermitsPerMinute = 5;
    private const string UserA = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
    private const string UserB = "9c8d7e6f-1a2b-4c3d-8e4f-5a6b7c8d9e0f";

    private static Task<HttpResponseMessage> Generate(HttpClient client, string? user = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(new
            {
                generationId = Guid.NewGuid().ToString(),
                mode = "simple",
                tier = 0,
                prompt = new string('x', 5_001),
            }),
        };
        if (user is not null) request.Headers.Add(EngineCaller.UserHeader, user);
        if (key is not null) request.Headers.Add("X-Engine-Key", key);
        return client.SendAsync(request);
    }

    private static async Task SpendWindow(HttpClient client, string? user = null, string? key = null)
    {
        for (var i = 0; i < GeneratePermitsPerMinute; i++)
            (await Generate(client, user, key)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OneUserSpendingTheirWindow_DoesNotLimitAnother()
    {
        using var factory = new NoAuthEngineFactory();
        var client = factory.CreateClient();

        await SpendWindow(client, UserA);

        (await Generate(client, UserA)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Generate(client, UserB)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestsThatNameNoUser_ShareOneWindow()
    {
        using var factory = new NoAuthEngineFactory();
        var client = factory.CreateClient();

        await SpendWindow(client);

        (await Generate(client)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        // A named user is not caught by the shared window.
        (await Generate(client, UserA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AUserHeaderThatIsNotAnId_DoesNotBuyAFreshWindow()
    {
        using var factory = new NoAuthEngineFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < GeneratePermitsPerMinute; i++)
            (await Generate(client, user: $"made-up-{i}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Generate(client, user: "made-up-again")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RequestsWithoutTheServiceKey_AreRefusedWithoutSpendingAPermit()
    {
        const string serviceKey = "rate-limit-test-key";
        using var noAuth = new NoAuthEngineFactory();
        using var factory = noAuth.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Engine:ServiceKey"] = serviceKey })));
        var client = factory.CreateClient();

        // Twice the window, unauthenticated, naming the user whose window they would spend.
        for (var i = 0; i < GeneratePermitsPerMinute * 2; i++)
            (await Generate(client, UserA)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await SpendWindow(client, UserA, serviceKey);
    }

    [Theory]
    [InlineData("3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c", "3f2b8c1a5d4e4f6a9b7c8d9e0f1a2b3c")]
    [InlineData("3F2B8C1A-5D4E-4F6A-9B7C-8D9E0F1A2B3C", "3f2b8c1a5d4e4f6a9b7c8d9e0f1a2b3c")]
    [InlineData("not-an-id", EngineCaller.SharedPartition)]
    [InlineData("", EngineCaller.SharedPartition)]
    [InlineData(null, EngineCaller.SharedPartition)]
    public void PartitionKey_IsTheUserIdInOneForm_OrTheSharedBucket(string? header, string expected)
    {
        var context = new DefaultHttpContext();
        if (header is not null) context.Request.Headers[EngineCaller.UserHeader] = header;

        EngineCaller.PartitionKey(context).Should().Be(expected);
    }
}
