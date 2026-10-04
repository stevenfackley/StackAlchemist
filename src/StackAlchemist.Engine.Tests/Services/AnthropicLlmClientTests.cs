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
                ["Anthropic:Model"]     = "claude-sonnet-5-5",
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
        result.Model.Should().Be("claude-sonnet-5-5");
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

    private static HttpResponseMessage RateLimitedResponse()
    {
        // Retry-After: 0 zeroes out the backoff so the deep retry budget doesn't
        // make this test sleep for real.
        var response = CreateResponse(HttpStatusCode.TooManyRequests, "rate limited");
        response.Headers.Add("Retry-After", "0");
        return response;
    }

    [Fact]
    public async Task GenerateAsync_RateLimitedPersistently_ThrowsLlmRateLimitExceptionAfterFiveTries()
    {
        // 429/529 get a deeper budget (5 tries) than hard 5xx failures, and exhaustion
        // surfaces as a typed exception so the failure categorizes as rate_limit.
        var handler = new SequencedHttpHandler(
            RateLimitedResponse(),
            RateLimitedResponse(),
            RateLimitedResponse(),
            RateLimitedResponse(),
            RateLimitedResponse());
        var client = BuildClient(BuildConfig(), handler);

        var act = () => client.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<StackAlchemist.Engine.Models.LlmRateLimitException>();
        handler.CallCount.Should().Be(5);
    }

    [Fact]
    public async Task GenerateAsync_RateLimitedThenRecovers_SucceedsOnFifthTry()
    {
        var handler = new SequencedHttpHandler(
            RateLimitedResponse(),
            RateLimitedResponse(),
            RateLimitedResponse(),
            RateLimitedResponse(),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    content = new[] { new { type = "text", text = "recovered" } },
                    stop_reason = "end_turn",
                    usage = new { input_tokens = 2, output_tokens = 3 },
                }), Encoding.UTF8, "application/json"),
            });
        var client = BuildClient(BuildConfig(), handler);

        var result = await client.GenerateAsync("system", "user");

        result.Text.Should().Be("recovered");
        handler.CallCount.Should().Be(5);
    }

    [Fact]
    public async Task GenerateAsync_ServerErrorsPersistently_ThrowsHttpRequestExceptionAfterThreeTries()
    {
        // Hard 5xx failures keep the original 3-try budget and untyped exception.
        var handler = new SequencedHttpHandler(
            CreateResponse(HttpStatusCode.InternalServerError, "boom"),
            CreateResponse(HttpStatusCode.InternalServerError, "boom"),
            CreateResponse(HttpStatusCode.InternalServerError, "boom"));
        var client = BuildClient(BuildConfig(), handler);

        var act = () => client.GenerateAsync("system", "user");

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public void GetRetryDelay_CapsLongRetryAfterHeader()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("Retry-After", "600");

        var delay = AnthropicLlmClient.GetRetryDelay(response, attempt: 0);

        delay.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GenerateAsync_PropagatesStopReason()
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "partial output" } },
            stop_reason = "max_tokens",
            usage = new { input_tokens = 10, output_tokens = 100 },
        });
        var client = BuildClient(BuildConfig(), new FakeHttpHandler(HttpStatusCode.OK, payload));

        var result = await client.GenerateAsync("system", "user");

        result.StopReason.Should().Be("max_tokens");
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

    // ── Request shape per model (Claude Sonnet 5.5 default) ──────────────────

    private static IConfiguration ConfigFor(string model, string? effort = null, string? maxTokens = "100") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"]    = "test-key",
                ["Anthropic:Model"]     = model,
                ["Anthropic:MaxTokens"] = maxTokens,
                ["Anthropic:Effort"]    = effort,
            })
            .Build();

    private static async Task<(JsonElement Body, BodyCapturingHttpHandler Handler)> SendAndCapture(IConfiguration config)
    {
        var handler = new BodyCapturingHttpHandler(JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "ok" } },
            stop_reason = "end_turn",
        }));
        await BuildClient(config, handler).GenerateAsync("system", "user");
        return (JsonDocument.Parse(handler.LastBody!).RootElement, handler);
    }

    [Fact]
    public async Task GenerateAsync_SonnetFiveFive_SendsMediumEffortAndDefaultFallbacks()
    {
        var (body, handler) = await SendAndCapture(ConfigFor("claude-sonnet-5-5"));

        body.GetProperty("model").GetString().Should().Be("claude-sonnet-5-5");
        body.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("medium");
        body.GetProperty("fallbacks").GetString().Should().Be("default");
        body.TryGetProperty("thinking", out _).Should().BeFalse("adaptive thinking is the default; disabled is a 400");
        body.TryGetProperty("temperature", out _).Should().BeFalse("non-default sampling params are a 400");
        handler.LastBetaHeader.Should().Be("server-side-fallback-2026-07-01");
    }

    [Fact]
    public async Task GenerateAsync_EffortComesFromConfig()
    {
        var (body, _) = await SendAndCapture(ConfigFor("claude-opus-5-5", effort: "high"));

        body.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("high");
        body.GetProperty("fallbacks").GetString().Should().Be("default");
    }

    [Fact]
    public async Task GenerateAsync_UnknownEffortValue_FallsBackToMedium()
    {
        var (body, _) = await SendAndCapture(ConfigFor("claude-sonnet-5-5", effort: "turbo"));

        body.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("medium");
    }

    [Fact]
    public async Task GenerateAsync_HaikuFourFive_SendsNeitherEffortNorFallbacks()
    {
        // Effort errors on Haiku 4.5, and it has no server-side fallback.
        var (body, handler) = await SendAndCapture(ConfigFor("claude-haiku-4-5"));

        body.TryGetProperty("output_config", out _).Should().BeFalse();
        body.TryGetProperty("fallbacks", out _).Should().BeFalse();
        handler.LastBetaHeader.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_SonnetFourSix_SendsEffortButNoFallbacks()
    {
        var (body, handler) = await SendAndCapture(ConfigFor("claude-sonnet-4-6"));

        body.GetProperty("output_config").GetProperty("effort").GetString().Should().Be("medium");
        body.TryGetProperty("fallbacks", out _).Should().BeFalse();
        handler.LastBetaHeader.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_NoMaxTokensConfigured_DefaultsToTwentyThousand()
    {
        var (body, _) = await SendAndCapture(ConfigFor("claude-sonnet-5-5", maxTokens: null));

        body.GetProperty("max_tokens").GetInt32().Should().Be(20_000);
    }

    // ── Response parsing ─────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_ReadsTextBlocksByType_SkippingThinking()
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = new object[]
            {
                new { type = "thinking", thinking = "", signature = "sig" },
                new { type = "text", text = "[[FILE:a.cs]]a" },
                new { type = "text", text = "[[END_FILE]]" },
            },
            stop_reason = "end_turn",
            usage = new { input_tokens = 1, output_tokens = 2 },
        });
        var client = BuildClient(BuildConfig(), new FakeHttpHandler(HttpStatusCode.OK, payload));

        var result = await client.GenerateAsync("system", "user");

        result.Text.Should().Be("[[FILE:a.cs]]a[[END_FILE]]");
    }

    [Fact]
    public async Task GenerateAsync_AfterServerSideFallback_UsesOnlyTheFallbackModelsTextAndModel()
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = "claude-sonnet-5",
            content = new object[]
            {
                new { type = "text", text = "declined partial" },
                new { type = "fallback", from = new { model = "claude-sonnet-5-5" }, to = new { model = "claude-sonnet-5" } },
                new { type = "text", text = "fallback answer" },
            },
            stop_reason = "end_turn",
            usage = new { input_tokens = 3, output_tokens = 4 },
        });
        var client = BuildClient(BuildConfig(), new FakeHttpHandler(HttpStatusCode.OK, payload));

        var result = await client.GenerateAsync("system", "user");

        result.Text.Should().Be("fallback answer");
        result.Model.Should().Be("claude-sonnet-5", "token accounting records the model that served the turn");
    }

    [Fact]
    public async Task GenerateAsync_Refusal_ThrowsLlmRefusalExceptionWithCategory()
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = Array.Empty<object>(),
            stop_reason = "refusal",
            stop_details = new { type = "refusal", category = "general_harms", explanation = "x" },
            usage = new { input_tokens = 3, output_tokens = 0 },
        });
        var client = BuildClient(BuildConfig(), new FakeHttpHandler(HttpStatusCode.OK, payload));

        var act = () => client.GenerateAsync("system", "user");

        var ex = (await act.Should().ThrowAsync<StackAlchemist.Engine.Models.LlmRefusalException>()).Which;
        ex.Category.Should().Be("general_harms");
        ex.Message.Should().Contain("declined");
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

    private sealed class BodyCapturingHttpHandler(string body) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public string? LastBetaHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastBetaHeader = request.Headers.TryGetValues("anthropic-beta", out var v) ? string.Join(",", v) : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
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
