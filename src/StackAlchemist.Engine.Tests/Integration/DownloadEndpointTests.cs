using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// <c>POST /api/download-url</c>: a new link for a delivered paid build, and for nothing else.
/// </summary>
public sealed class DownloadEndpointTests(NoAuthEngineFactory factory) : IClassFixture<NoAuthEngineFactory>
{
    private static readonly string GenerationId = Guid.NewGuid().ToString();

    private (HttpClient Client, IR2UploadService R2) Build(GenerationSnapshot? snapshot)
    {
        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationSnapshotAsync(GenerationId, Arg.Any<CancellationToken>()).Returns(snapshot);

        var r2 = Substitute.For<IR2UploadService>();
        r2.CreateDownloadUrl(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns("https://r2.test/fresh-link");

        var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDeliveryService>();
            services.AddSingleton(delivery);
            services.RemoveAll<IR2UploadService>();
            services.AddSingleton(r2);
        })).CreateClient();

        return (client, r2);
    }

    private static GenerationSnapshot Snapshot(string status, int tier) =>
        new(GenerationId, status, tier, "advanced", null, ProjectType.DotNetNextJs, null, null, 0, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_delivered_paid_build_gets_a_fifteen_minute_link(int tier)
    {
        var (client, r2) = Build(Snapshot("success", tier));

        var response = await client.PostAsJsonAsync("/api/download-url", new { generationId = GenerationId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("url").GetString().Should().Be("https://r2.test/fresh-link");
        r2.Received(1).CreateDownloadUrl(GenerationId, TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData("success", 0)] // a free preview has no archive
    [InlineData("failed", 2)] // refunded or failed: an earlier attempt may have left an archive behind
    [InlineData("building", 2)]
    [InlineData("pending", 3)]
    public async Task Anything_else_gets_404_and_no_link(string status, int tier)
    {
        var (client, r2) = Build(Snapshot(status, tier));

        var response = await client.PostAsJsonAsync("/api/download-url", new { generationId = GenerationId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        r2.DidNotReceiveWithAnyArgs().CreateDownloadUrl(default!, default);
    }

    [Fact]
    public async Task An_unknown_generation_gets_404()
    {
        var (client, r2) = Build(snapshot: null);

        var response = await client.PostAsJsonAsync("/api/download-url", new { generationId = GenerationId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        r2.DidNotReceiveWithAnyArgs().CreateDownloadUrl(default!, default);
    }

    [Theory]
    [InlineData("../other-bucket-key")]
    [InlineData("not-a-uuid")]
    [InlineData("")]
    [InlineData(null)]
    public async Task An_id_that_is_not_a_uuid_never_reaches_the_object_key(string? generationId)
    {
        var (client, r2) = Build(Snapshot("success", 2));

        var response = await client.PostAsJsonAsync("/api/download-url", new { generationId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        r2.DidNotReceiveWithAnyArgs().CreateDownloadUrl(default!, default);
    }
}
