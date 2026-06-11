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
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(SupabaseDeliveryService.HttpClientName).Returns(httpClient);

        return new SupabaseDeliveryService(
            factory, config, new PendingWriteBuffer(), NullLogger<SupabaseDeliveryService>.Instance);
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

        var body = handler.LastBody!;
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

        var body = handler.LastBody!;
        body.Should().Contain("project.zip");
    }

    [Fact]
    public async Task UpdateStatusAsync_IncludesErrorCategoryOnFailure()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync(
            "gen-cat", GenerationState.Failed,
            errorMessage: "Build failed after 3 retries.",
            errorCategory: ErrorCategorizer.Build);

        var body = handler.LastBody!;
        body.Should().Contain("\"error_category\":\"build\"");
        body.Should().Contain("\"error_message\"");
    }

    [Fact]
    public async Task UpdateStatusAsync_OmitsErrorCategoryWhenNotProvided()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.NoContent);
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync("gen-nocat", GenerationState.Building);

        handler.LastBody!.Should().NotContain("error_category");
    }

    [Fact]
    public async Task GetGenerationSnapshotAsync_ParsesRow()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK,
            "[{\"id\":\"gen-snap\",\"status\":\"pending\",\"tier\":2,\"mode\":\"advanced\"," +
            "\"prompt\":null,\"project_type\":\"DotNetNextJs\",\"schema_json\":null," +
            "\"personalization_json\":null,\"attempt_count\":1,\"updated_at\":\"2026-06-11T10:00:00Z\"}]");
        var sut = BuildSut(BuildConfig(), handler);

        var snapshot = await sut.GetGenerationSnapshotAsync("gen-snap", CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.Id.Should().Be("gen-snap");
        snapshot.Tier.Should().Be(2);
        snapshot.Status.Should().Be("pending");
        snapshot.ProjectType.Should().Be(ProjectType.DotNetNextJs);
        snapshot.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task GetGenerationSnapshotAsync_ReturnsNullWhenRowMissing()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[]");
        var sut = BuildSut(BuildConfig(), handler);

        var snapshot = await sut.GetGenerationSnapshotAsync("gen-missing", CancellationToken.None);

        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task GetGenerationSnapshotAsync_ReturnsNullWhenNotConfigured()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[]");
        var sut = BuildSut(BuildConfig(url: null, key: null), handler);

        var snapshot = await sut.GetGenerationSnapshotAsync("gen-noop", CancellationToken.None);

        snapshot.Should().BeNull();
        handler.LastRequest.Should().BeNull();
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
        var body = handler.LastBody!;
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
        var body = handler.LastBody!;
        body.Should().Contain("\"gen_id\":\"gen-usage\"");
        body.Should().Contain("\"input_delta\":12");
        body.Should().Contain("\"output_delta\":34");
    }

    // ── Test double ───────────────────────────────────────────────────────────

    private SupabaseDeliveryService BuildSutWithBuffer(
        IConfiguration config,
        HttpMessageHandler handler,
        PendingWriteBuffer buffer)
    {
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(SupabaseDeliveryService.HttpClientName).Returns(httpClient);

        return new SupabaseDeliveryService(
            factory, config, buffer, NullLogger<SupabaseDeliveryService>.Instance);
    }

    [Fact]
    public async Task CriticalWrite_RetriesTransientFailuresWithBackoff()
    {
        var handler = new SequencedDeliveryHandler(
            (HttpStatusCode.ServiceUnavailable, ""),
            (HttpStatusCode.ServiceUnavailable, ""),
            (HttpStatusCode.NoContent, ""));
        var sut = BuildSut(BuildConfig(), handler);

        await sut.UpdateStatusAsync("gen-backoff", GenerationState.Success);

        handler.CallCount.Should().Be(3, "transient 5xx failures are retried until the budget (5) allows");
    }

    [Fact]
    public async Task CriticalWrite_NonTransient4xx_FailsFastAndBuffers()
    {
        // A CHECK violation never succeeds on retry: exactly one attempt, then the
        // payload lands in the pending-write buffer for the reconciler to inspect.
        var handler = new SequencedDeliveryHandler(
            (HttpStatusCode.BadRequest, "check constraint violation"));
        var buffer = new PendingWriteBuffer();
        var sut = BuildSutWithBuffer(BuildConfig(), handler, buffer);

        await sut.UpdateStatusAsync("gen-4xx", GenerationState.Failed, errorMessage: "oops");

        handler.CallCount.Should().Be(1, "non-transient 4xx must not burn the retry budget");
        buffer.Count.Should().Be(1);
        buffer.TryDequeue(out var write).Should().BeTrue();
        write!.GenerationId.Should().Be("gen-4xx");
        write.Payload["status"].Should().Be("failed");
    }

    [Fact]
    public async Task TryClaimForRequeueAsync_SendsConditionalPatchAndIncrementsAttempt()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[{\"id\":\"gen-claim\"}]");
        var sut = BuildSut(BuildConfig(), handler);
        var row = new GenerationSnapshot(
            "gen-claim", "generating_code", 2, "advanced", null, null, null, null, 1,
            DateTimeOffset.UtcNow.AddMinutes(-30));

        var claimed = await sut.TryClaimForRequeueAsync(row, TimeSpan.FromMinutes(15), CancellationToken.None);

        claimed.Should().BeTrue();
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("id=eq.gen-claim");
        url.Should().Contain("status=eq.generating_code");
        url.Should().Contain("attempt_count=eq.1");
        url.Should().Contain("updated_at=lt.");
        handler.LastBody.Should().Contain("\"attempt_count\":2");
        handler.LastBody.Should().Contain("\"pending\"");
    }

    [Fact]
    public async Task TryClaimForRequeueAsync_EmptyResponse_ReturnsFalse()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[]");
        var sut = BuildSut(BuildConfig(), handler);
        var row = new GenerationSnapshot(
            "gen-lost", "pending", 0, null, null, null, null, null, 0,
            DateTimeOffset.UtcNow.AddMinutes(-30));

        var claimed = await sut.TryClaimForRequeueAsync(row, TimeSpan.FromMinutes(15), CancellationToken.None);

        claimed.Should().BeFalse("an empty representation means another claimer won the CAS");
    }

    [Fact]
    public async Task TryBeginExtractionAsync_ClaimsPendingOrFailedRow()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[{\"id\":\"gen-ex\"}]");
        var sut = BuildSut(BuildConfig(), handler);

        var ok = await sut.TryBeginExtractionAsync("gen-ex", CancellationToken.None);

        ok.Should().BeTrue();
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("status=in.(pending,failed)");
        handler.LastBody.Should().Contain("\"extracting_schema\"");
    }

    [Fact]
    public async Task TryBeginExtractionAsync_AlreadyInProgress_ReturnsFalse()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[]");
        var sut = BuildSut(BuildConfig(), handler);

        var ok = await sut.TryBeginExtractionAsync("gen-busy", CancellationToken.None);

        ok.Should().BeFalse("the row is mid-extraction or terminal — a double-submit must not re-fire the LLM");
    }

    [Fact]
    public async Task TryBeginExtractionAsync_WhenNotConfigured_ProceedsWithoutRequest()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "[]");
        var sut = BuildSut(BuildConfig(url: null, key: null), handler);

        var ok = await sut.TryBeginExtractionAsync("gen-dev", CancellationToken.None);

        ok.Should().BeTrue("local dev without Supabase must keep working");
        handler.LastRequest.Should().BeNull();
    }

    private sealed class SequencedDeliveryHandler(params (HttpStatusCode Status, string Body)[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var (status, body) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CapturingHttpHandler(
        HttpStatusCode status,
        string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            // Capture the body now: HttpClient disposes the request content once SendAsync
            // returns (PatchGenerationAsync wraps it in `using`), so reading it later throws.
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
