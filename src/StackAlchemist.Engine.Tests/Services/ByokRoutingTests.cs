using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// End-to-end BYOK credential resolution + provider routing: a stored (encrypted) key + preferred
/// model resolve to <see cref="LlmCallOptions"/>, route to the right provider client, and the
/// DECRYPTED key + real model reach the outbound HTTP call — while the plaintext key never appears
/// in any logged output. Uses a golden ciphertext (see <see cref="ByokKeyProtectorTests"/>).
/// </summary>
public class ByokRoutingTests
{
    private const string GoldenSecret = ByokKeyProtectorTests.GoldenSecret;
    private const string GoldenPlaintext = ByokKeyProtectorTests.GoldenPlaintext;          // sk-ant-… (Anthropic)
    private const string GoldenCiphertext = ByokKeyProtectorTests.GoldenCiphertext;
    private const string GoldenOpenAiPlaintext = ByokKeyProtectorTests.GoldenOpenAiPlaintext; // sk-proj-… (OpenAI)
    private const string GoldenOpenAiCiphertext = ByokKeyProtectorTests.GoldenOpenAiCiphertext;

    private static IConfiguration Config(string? globalModel = null, string? globalAnthropicKey = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Byok:EncryptionKey"] = GoldenSecret,
                ["Anthropic:Model"] = globalModel,
                ["Anthropic:ApiKey"] = globalAnthropicKey,
                ["Anthropic:MaxTokens"] = "256",
            })
            .Build();

    private static IHttpClientFactory FactoryFor(string baseAddress, HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        return factory;
    }

    [Fact]
    public async Task Resolve_OpenAiOverride_SendsDecryptedKeyAndRealModel_AndNeverLogsKey()
    {
        var logs = new List<string>();
        var config = Config();

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-1", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(GoldenOpenAiCiphertext, "openai/gpt-4o-mini"));

        var protector = new ByokKeyProtector(config, new ListLogger<ByokKeyProtector>(logs));
        var resolver = new LlmCredentialResolver(delivery, protector, config, new ListLogger<LlmCredentialResolver>(logs));

        var options = await resolver.ResolveAsync("gen-1", CancellationToken.None);
        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.OpenAi);
        options.Model.Should().Be("gpt-4o-mini");

        var handler = new CapturingHandler(ChatCompletionJson("done"));
        var openAi = new OpenAiCompatibleLlmClient(
            FactoryFor("https://api.openai.com/v1/", handler), config, new ListLogger<OpenAiCompatibleLlmClient>(logs));
        var routing = new RoutingLlmClient(
            AnthropicClient(config, handler),
            openAi,
            new MockLlmClient(),
            config,
            new ListLogger<RoutingLlmClient>(logs));

        var response = await routing.GenerateAsync("system", "user", options, CancellationToken.None);

        // Outbound request carried the DECRYPTED key + the real (deprefixed) model.
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.openai.com/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be(GoldenOpenAiPlaintext);
        handler.LastBody.Should().Contain("\"model\":\"gpt-4o-mini\"");

        // Token accounting records the ACTUAL model, not the global default.
        response.Model.Should().Be("gpt-4o-mini");

        // The decrypted key must never appear in any logged output.
        logs.Should().NotBeEmpty();
        logs.Should().NotContain(l => l.Contains(GoldenOpenAiPlaintext, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Resolve_CrossProviderKey_IsNeverForwardedToTheWrongVendor()
    {
        // H1: a stored OpenAI key (sk-proj-…) with an Anthropic model selected must NOT be sent to
        // Anthropic. The key is dropped (provider mismatch) and the Anthropic route falls back to
        // the global key — the user's OpenAI secret never reaches api.anthropic.com.
        var config = Config(globalAnthropicKey: "sk-ant-GLOBAL-house-key");

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-x", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(GoldenOpenAiCiphertext, "claude-3-5-haiku-20241022"));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-x", CancellationToken.None);

        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.Anthropic);
        options.HasKey.Should().BeFalse("an OpenAI key must never be attached to the Anthropic route");

        var handler = new CapturingHandler(AnthropicJson("done"));
        var routing = new RoutingLlmClient(
            AnthropicClient(config, handler), OpenAiClient(config, handler), new MockLlmClient(),
            config, NullLogger<RoutingLlmClient>.Instance);

        await routing.GenerateAsync("system", "user", options, CancellationToken.None);

        // The outbound Anthropic request used the GLOBAL house key, not the user's OpenAI key.
        handler.LastRequest!.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be("sk-ant-GLOBAL-house-key");
        handler.LastBody.Should().NotContain(GoldenOpenAiPlaintext);
    }

    [Fact]
    public async Task Resolve_NonAllowlistedAnthropicModelOnGlobalKey_DowngradesToDefault()
    {
        // M1: preferred_model is RLS-writable, bypassing the web allowlist. A user cannot pin our
        // GLOBAL key to an arbitrary expensive model — a non-allowlisted Anthropic model with no
        // BYOK key is downgraded to the configured default.
        var config = Config(globalModel: "claude-sonnet-4-6", globalAnthropicKey: "sk-ant-GLOBAL-house-key");

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-m1", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(null, "claude-opus-4-turbo-ultra-expensive"));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-m1", CancellationToken.None);

        // Downgraded to the default → resolves to null (unchanged global path on the default model).
        options.Should().BeNull("a non-allowlisted model on the global key downgrades to the default");
    }

    [Fact]
    public async Task Resolve_AllowlistedNonDefaultAnthropicModelOnGlobalKey_IsHonored()
    {
        // An allowlisted non-default Anthropic model (e.g. Haiku) is fine on the global key.
        var config = Config(globalModel: "claude-sonnet-4-6", globalAnthropicKey: "sk-ant-GLOBAL-house-key");

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-m2", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(null, "claude-3-5-haiku-20241022"));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-m2", CancellationToken.None);

        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.Anthropic);
        options.Model.Should().Be("claude-3-5-haiku-20241022");
        options.HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_AnthropicByokOverride_SendsDecryptedKeyAndModel()
    {
        var logs = new List<string>();
        var config = Config();

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-2", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(GoldenCiphertext, "claude-3-5-haiku-20241022"));

        var protector = new ByokKeyProtector(config, new ListLogger<ByokKeyProtector>(logs));
        var resolver = new LlmCredentialResolver(delivery, protector, config, new ListLogger<LlmCredentialResolver>(logs));

        var options = await resolver.ResolveAsync("gen-2", CancellationToken.None);
        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.Anthropic);
        options.Model.Should().Be("claude-3-5-haiku-20241022");
        options.HasKey.Should().BeTrue();

        var handler = new CapturingHandler(AnthropicJson("done"));
        var routing = new RoutingLlmClient(
            AnthropicClient(config, handler),
            OpenAiClient(config, handler),
            new MockLlmClient(),
            config,
            new ListLogger<RoutingLlmClient>(logs));

        await routing.GenerateAsync("system", "user", options, CancellationToken.None);

        handler.LastRequest!.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be(GoldenPlaintext);
        handler.LastBody.Should().Contain("\"model\":\"claude-3-5-haiku-20241022\"");
        logs.Should().NotContain(l => l.Contains(GoldenPlaintext, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Resolve_ByokOnlyProviderWithoutKey_FailsCleanly()
    {
        var config = Config();

        var delivery = Substitute.For<IDeliveryService>();
        // No stored ciphertext, but an OpenAI (BYOK-only) model is selected.
        delivery.GetGenerationCredentialAsync("gen-3", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(null, "openai/gpt-4o-mini"));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-3", CancellationToken.None);
        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.OpenAi);
        options.HasKey.Should().BeFalse();

        var handler = new CapturingHandler(ChatCompletionJson("unused"));
        var routing = new RoutingLlmClient(
            AnthropicClient(config, handler),
            OpenAiClient(config, handler),
            new MockLlmClient(),
            config,
            NullLogger<RoutingLlmClient>.Instance);

        var act = () => routing.GenerateAsync("system", "user", options, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ByokConfigException>()).Which;
        ex.Message.Should().NotContain("sk-"); // no key material in the user-facing message
        // No dedicated "config" category exists — categorizes as internal.
        ErrorCategorizer.Categorize(ex).Should().Be(ErrorCategorizer.Internal);
        handler.LastRequest.Should().BeNull("the call must fail before any HTTP request is made");
    }

    [Theory]
    [InlineData(null, null)]                       // no override, no model → null
    [InlineData(null, "claude-sonnet-4-6")]        // no override, default model → null
    public async Task Resolve_NoOverrideDefaultModel_ReturnsNull(string? ciphertext, string? preferredModel)
    {
        // Global model = default; a row with no BYOK key and the default model must resolve to
        // null so the pipeline uses the unchanged global path byte-for-byte.
        var config = Config(globalModel: "claude-sonnet-4-6");

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-4", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(ciphertext, preferredModel));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-4", CancellationToken.None);

        options.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_NoProfile_ReturnsNull()
    {
        var config = Config(globalModel: "claude-sonnet-4-6");
        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ProfileCredential?)null);

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        (await resolver.ResolveAsync("gen-5", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_UndecryptableAnthropicKey_FallsBackToGlobalKey()
    {
        // A stored key that fails to decrypt (wrong/rotated secret) must degrade to the global
        // Anthropic key for an Anthropic model — never crash, never a BYOK failure.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Byok:EncryptionKey"] = "a-completely-different-32-char-secret!!!!",
                ["Anthropic:Model"] = "claude-sonnet-4-6",
            })
            .Build();

        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationCredentialAsync("gen-6", Arg.Any<CancellationToken>())
            .Returns(new ProfileCredential(GoldenCiphertext, "claude-3-5-haiku-20241022"));

        var protector = new ByokKeyProtector(config, NullLogger<ByokKeyProtector>.Instance);
        var resolver = new LlmCredentialResolver(delivery, protector, config, NullLogger<LlmCredentialResolver>.Instance);

        var options = await resolver.ResolveAsync("gen-6", CancellationToken.None);

        // Per-user model is still honored, but the key falls back to global (null here).
        options.Should().NotBeNull();
        options!.Provider.Should().Be(LlmProvider.Anthropic);
        options.Model.Should().Be("claude-3-5-haiku-20241022");
        options.HasKey.Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AnthropicLlmClient AnthropicClient(IConfiguration config, HttpMessageHandler handler) =>
        new(FactoryFor("https://api.anthropic.com", handler), config, NullLogger<AnthropicLlmClient>.Instance);

    private static OpenAiCompatibleLlmClient OpenAiClient(IConfiguration config, HttpMessageHandler handler) =>
        new(FactoryFor("https://api.openai.com/v1/", handler), config, NullLogger<OpenAiCompatibleLlmClient>.Instance);

    private static string ChatCompletionJson(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { role = "assistant", content }, finish_reason = "stop" } },
        usage = new { prompt_tokens = 3, completion_tokens = 7 },
    });

    private static string AnthropicJson(string text) => JsonSerializer.Serialize(new
    {
        content = new[] { new { type = "text", text } },
        stop_reason = "end_turn",
        usage = new { input_tokens = 3, output_tokens = 7 },
    });

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>Captures every formatted log line into a shared list for leak assertions.</summary>
    private sealed class ListLogger<T>(List<string> sink) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => sink.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
