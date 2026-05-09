using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AnthropicLlmClient"/>.
/// The HTTP call is intercepted via a fake message handler — no real API calls are made.
/// </summary>
public class AnthropicLlmClientTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(string? apiKey = "test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"]    = apiKey,
                ["Anthropic:Model"]     = "claude-sonnet-4-6",
                ["Anthropic:MaxTokens"] = "100",
            })
            .Build();

    private static AnthropicLlmClient BuildClient(
        IConfiguration config,
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
        };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AnthropicLlmClient.HttpClientName).Returns(httpClient);

        return new AnthropicLlmClient(
            factory,
            config,
            NullLogger<AnthropicLlmClient>.Instance);
    }

    private static HttpMessageHandler BuildHandler(string responseText)
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = responseText },
            },
            stop_reason = "end_turn",
            usage = new { input_tokens = 10, output_tokens = 5 },
        });

        return new FakeHttpHandler(HttpStatusCode.OK, payload);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_ValidResponse_ReturnsTextContent()
    {
        var client = BuildClient(BuildConfig(), BuildHandler("[[FILE:test.cs]]content[[END_FILE]]"));

        var result = await client.GenerateAsync("system", "user");

        result.Text.Should().Be("[[FILE:test.cs]]content[[END_FILE]]");
        result.InputTokens.Should().Be(10);
        result.OutputTokens.Should().Be(5);
        result.Model.Should().Be("claude-sonnet-4-6");
    }

    [Fact]
    public async Task GenerateAsync_MissingApiKey_ThrowsInvalidOperationException()
    {
        var client = BuildClient(BuildConfig(apiKey: null), BuildHandler("irrelevant"));

        var act = () => client.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Anthropic:ApiKey*");
    }

    [Fact]
    public async Task GenerateAsync_RetriesRetryableFailures_ThenReturnsText()
    {
        var handler = new SequencedHttpHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("rate limited", Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("rate limited", Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    content = new[] { new { type = "text", text = "ok" } },
                    stop_reason = "end_turn",
                    usage = new { input_tokens = 2, output_tokens = 3 },
                }), Encoding.UTF8, "application/json"),
            });
        var client = BuildClient(BuildConfig(), handler);

        var result = await client.GenerateAsync("system", "user");

        result.Text.Should().Be("ok");
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task GenerateAsync_ApiReturnsTooManyRetryableFailures_ThrowsHttpRequestException()
    {
        var handler = new SequencedHttpHandler(
            CreateResponse(HttpStatusCode.TooManyRequests, "rate limited"),
            CreateResponse(HttpStatusCode.TooManyRequests, "rate limited"),
            CreateResponse(HttpStatusCode.TooManyRequests, "rate limited"));
        var client = BuildClient(BuildConfig(), handler);

        var act = () => client.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task GenerateAsync_ResponseHasNoTextBlock_ThrowsInvalidOperationException()
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = Array.Empty<object>(),
            stop_reason = "end_turn",
        });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, payload);
        var client = BuildClient(BuildConfig(), handler);

        var act = () => client.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no text content*");
    }

    [Fact]
    public async Task GenerateAsync_SendsCorrectHeaders()
    {
        var captureHandler = new CapturingHttpHandler(HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                content = new[] { new { type = "text", text = "ok" } },
                stop_reason = "end_turn",
            }));

        var client = BuildClient(BuildConfig("my-secret-key"), captureHandler);
        await client.GenerateAsync("system", "user");

        captureHandler.LastRequest.Should().NotBeNull();
        captureHandler.LastRequest!.Headers.GetValues("x-api-key")
            .Should().ContainSingle().Which.Should().Be("my-secret-key");
        captureHandler.LastRequest.Headers.GetValues("anthropic-version")
            .Should().ContainSingle().Which.Should().Be("2023-06-01");
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(HttpStatusCode status, string body)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class CapturingHttpHandler(HttpStatusCode status, string body)
        : HttpMessageHandler
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

    private sealed class SequencedHttpHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
