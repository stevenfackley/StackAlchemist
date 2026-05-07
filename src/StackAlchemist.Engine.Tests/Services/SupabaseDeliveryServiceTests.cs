using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SupabaseDeliveryService"/>.
/// HTTP calls are intercepted via a fake message handler — no real Supabase calls are made.
/// </summary>
public class SupabaseDeliveryServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(
        string? url = "https://proj.supabase.co",
        string? key = "service-role-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"]            = url,
                ["Supabase:ServiceRoleKey"] = key,
            })
            .Build();

    private SupabaseDeliveryService BuildSut(
        IConfiguration config,
        CapturingHttpHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(SupabaseDeliveryService.HttpClientName).Returns(httpClient);

        return new SupabaseDeliveryService(
            factory, config, NullLogger<SupabaseDeliveryService>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_WhenConfigured_SendsPatchRequest()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync("gen-123", GenerationState.Success, downloadUrl: "https://r2.example/proj.zip");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Contain("gen-123");
    }

    [Fact]
    public async Task UpdateStatusAsync_IncludesStatusInBody()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync("gen-abc", GenerationState.Building);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("\"building\"");
    }

    [Fact]
    public async Task UpdateStatusAsync_IncludesDownloadUrlWhenProvided()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync(
            "gen-xyz", GenerationState.Success,
            downloadUrl: "https://r2.example/project.zip");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("project.zip");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenSupabaseNotConfigured_DoesNotSendRequest()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(url: null, key: null), handler);

        // Should not throw and should not send any HTTP request
        await sut.UpdateStatusAsync("gen-noop", GenerationState.Success);

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenSupabaseReturnsError_DoesNotThrow()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.InternalServerError, "error body");
        var sut = BuildSut(BuildConfig(), handler);

        // Pipeline must not be interrupted by delivery failures
        var act = () => sut.UpdateStatusAsync("gen-err", GenerationState.Failed, errorMessage: "oops");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateStatusAsync_SetsCorrectAuthHeaders()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(key: "my-service-key"), handler);

        await sut.UpdateStatusAsync("gen-hdr", GenerationState.Generating);

        handler.LastRequest!.Headers.GetValues("apikey")
            .Should().ContainSingle().Which.Should().Be("my-service-key");
        handler.LastRequest.Headers.GetValues("Authorization")
            .Should().ContainSingle().Which.Should().StartWith("Bearer ");
    }

    [Fact]
    public async Task AppendBuildLogAsync_UsesRpcEndpoint()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.AppendBuildLogAsync("gen-log", "hello", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain("/rpc/append_build_log");
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("\"gen_id\":\"gen-log\"");
        body.Should().Contain("\"chunk\":\"hello\"");
    }

    [Fact]
    public async Task UpdateTokenUsageAsync_UsesRpcEndpoint()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateTokenUsageAsync("gen-usage", 12, 34, "claude-sonnet-4-6", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain("/rpc/increment_token_usage");
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("\"gen_id\":\"gen-usage\"");
        body.Should().Contain("\"input_delta\":12");
        body.Should().Contain("\"output_delta\":34");
    }

    // ── Test double ───────────────────────────────────────────────────────────

    private sealed class CapturingHttpHandler(
        HttpStatusCode status,
        string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
