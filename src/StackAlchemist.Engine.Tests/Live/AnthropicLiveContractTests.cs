using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Live;

/// <summary>
/// Live contract check: the Engine's real <see cref="AnthropicLlmClient"/> request shape (effort,
/// server-side fallback, no thinking or sampling fields) against the real Anthropic API, for the
/// model in ANTHROPIC_MODEL. Opt-in only — it spends a few cents — and run by
/// <c>.github/workflows/llm-contract.yml</c> after any model bump. Normal test runs return early
/// (the repo's skip pattern), unless ANTHROPIC_LIVE_TEST_REQUIRED=1 makes a missing key a failure.
/// </summary>
[Trait("Category", "LiveAnthropic")]
public sealed class AnthropicLiveContractTests
{
    private static (bool Run, string? Key, string Model) Settings()
    {
        var run = Environment.GetEnvironmentVariable("ANTHROPIC_LIVE_TEST") == "1";
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL");
        if (run && string.IsNullOrWhiteSpace(key) &&
            Environment.GetEnvironmentVariable("ANTHROPIC_LIVE_TEST_REQUIRED") == "1")
        {
            throw new InvalidOperationException("ANTHROPIC_LIVE_TEST_REQUIRED=1 but ANTHROPIC_API_KEY is empty.");
        }

        return (run && !string.IsNullOrWhiteSpace(key), key,
                string.IsNullOrWhiteSpace(model) ? AnthropicDefaults.ModelId : model);
    }

    private static AnthropicLlmClient RealClient(string key, string model)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = key,
                ["Anthropic:Model"] = model,
                ["Anthropic:MaxTokens"] = "4000",
            })
            .Build();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AnthropicLlmClient.HttpClientName).Returns(_ => new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com"),
            Timeout = TimeSpan.FromMinutes(5),
        });
        return new AnthropicLlmClient(factory, config, NullLogger<AnthropicLlmClient>.Instance);
    }

    [Fact]
    public async Task ConfiguredModel_AcceptsTheEnginesRequestShape_AndAnswers()
    {
        var (run, key, model) = Settings();
        if (!run) return;

        var response = await RealClient(key!, model).GenerateAsync(
            "You are a contract test. Reply with the single word OK and nothing else.",
            "Reply now.");

        response.StopReason.Should().Be("end_turn");
        response.Text.Trim().Should().Contain("OK");
        response.Model.Should().NotBeNullOrWhiteSpace();
        response.InputTokens.Should().BeGreaterThan(0);
        response.OutputTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConfiguredModel_ProducesTheDelimitedFileFormatTheReconstructorParses()
    {
        var (run, key, model) = Settings();
        if (!run) return;

        var response = await RealClient(key!, model).GenerateAsync(
            "Output ONLY file blocks in this exact format, with no other text:\n" +
            "[[FILE:path]]\n<file content>\n[[END_FILE]]",
            "Write one file at dotnet/Models/Ping.cs containing a C# record `public record Ping(string Value);` " +
            "in namespace Contract.Models.");

        response.StopReason.Should().Be("end_turn");
        response.Text.Should().Contain("[[FILE:dotnet/Models/Ping.cs]]");
        response.Text.Should().Contain("[[END_FILE]]");
        response.Text.Should().Contain("record Ping");
    }
}
