using System.IO.Abstractions.TestingHelpers;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// What <see cref="GenerationOrchestrator.EnqueueAsync"/> does with the customer's money when a
/// generation ends in its catch: refund a paid build only when the failure leaves no Retry, and
/// treat a cancelled token as an interruption, not a failure.
/// </summary>
public sealed class GenerationOrchestratorRefundTests
{
    private sealed record Fixture(
        GenerationOrchestrator Sut,
        ILlmClient Llm,
        IDeliveryService Delivery,
        IFailedBuildRefunder Refunder,
        InFlightGenerationRegistry InFlight,
        Channel<GenerationContext> Queue);

    private static Fixture Build()
    {
        var files = new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
        };

        var templates = Substitute.For<ITemplateProvider>();
        templates.LoadTemplate(Arg.Any<string>()).Returns(files);
        templates.Render(Arg.Any<Dictionary<string, string>>(), Arg.Any<TemplateVariables>()).Returns(files);
        templates.FindInjectionZones(Arg.Any<string>()).Returns([]);

        var reconstruction = Substitute.For<IReconstructionService>();
        reconstruction.Parse(Arg.Any<string>()).Returns(files);
        reconstruction.Reconstruct(Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<ITemplateProvider>())
            .Returns(files);

        var promptBuilder = Substitute.For<IPromptBuilderService>();
        promptBuilder.BuildGenerationPrompt(Arg.Any<GenerationSchema>(), Arg.Any<ProjectType>(), Arg.Any<GenerationPersonalization?>(), Arg.Any<string>())
            .Returns("Generate code.");

        var llm = Substitute.For<ILlmClient>();
        var delivery = Substitute.For<IDeliveryService>();
        // The refund call is observed on a substitute; the decision is the real rule, reading the
        // same delivery service the orchestrator uses.
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var rule = new FailedBuildRefunder(
            Substitute.For<IRefundService>(), delivery, Substitute.For<IEmailService>(),
            NullLogger<FailedBuildRefunder>.Instance);
        refunder.ShouldRefundAsync(default!, default, default!).ReturnsForAnyArgs(call =>
            rule.ShouldRefundAsync(call.ArgAt<string>(0), call.ArgAt<int>(1), call.ArgAt<string>(2)));
        var inFlight = new InFlightGenerationRegistry();
        var queue = Channel.CreateUnbounded<GenerationContext>();

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            Substitute.For<IInjectionEngine>(),
            delivery,
            new TierGatingService(),
            new MockFileSystem(),
            new ConfigurationBuilder().Build(),
            queue.Writer,
            inFlight,
            NullLogger<GenerationOrchestrator>.Instance,
            credentialResolver: null,
            refunder);

        return new Fixture(sut, llm, delivery, refunder, inFlight, queue);
    }

    private static GenerateRequest Request(int tier) => new()
    {
        GenerationId = Guid.NewGuid().ToString(),
        Mode = "simple",
        Tier = tier,
        Prompt = "Build an app",
    };

    private static void LlmThrows(Fixture f, Exception ex) =>
        f.Llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LlmCallOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PaidBuild_FailsWithACategoryThatOffersNoRetry_IsRefunded(int tier)
    {
        var f = Build();
        LlmThrows(f, new InvalidOperationException("template bug"));
        var request = Request(tier);

        var response = await f.Sut.EnqueueAsync(request);

        response.Status.Should().Be("failed");
        Received.InOrder(() =>
        {
            // The failed status is written first: the refund must never be the only record.
            f.Delivery.UpdateStatusAsync(
                request.GenerationId, GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
                ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
            f.Refunder.TryRefundAsync(
                request.GenerationId, tier, RefundReason.BuildCouldNotFinish, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task PaidBuild_RefundRunsOnATokenTheStoppingHostCannotCancel()
    {
        var f = Build();
        LlmThrows(f, new InvalidOperationException("template bug"));
        using var stopping = new CancellationTokenSource();
        var request = Request(tier: 2);

        await f.Sut.EnqueueAsync(request, stopping.Token);

        await f.Refunder.Received(1).TryRefundAsync(
            request.GenerationId, 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);
    }

    public static TheoryData<Exception> RetryableFailures() => new()
    {
        new HttpRequestException("upstream failed"),          // network
        new TimeoutException("llm timed out"),                // network
        new LlmRateLimitException("slow down"),               // rate_limit
        new MalformedLlmOutputException("no file blocks"),    // schema
    };

    [Theory]
    [MemberData(nameof(RetryableFailures))]
    public async Task PaidBuild_FailsWithACategoryTheCustomerCanRetry_IsNotRefunded(Exception failure)
    {
        // A refund here takes the transaction out of 'completed', and the payment gate then
        // answers 402 to the Retry the result page is showing.
        var f = Build();
        LlmThrows(f, failure);
        var request = Request(tier: 2);

        var response = await f.Sut.EnqueueAsync(request);

        response.Status.Should().Be("failed");
        ErrorCategorizer.OffersRetry(ErrorCategorizer.Categorize(failure)).Should().BeTrue();
        await f.Refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    private static GenerationSnapshot Row(string id, int retryCount, int attemptCount = 0) =>
        new(id, "pending", 2, "simple", null, null, null, null, attemptCount, DateTimeOffset.UtcNow)
        {
            RetryCount = retryCount,
        };

    [Fact]
    public async Task PaidBuild_RetryableFailureWithEveryRetryUsed_IsRefunded()
    {
        // The customer has pressed Retry three times. A network failure is retryable, but the
        // web refuses a fourth retry, so there is nothing left for them to press.
        var f = Build();
        LlmThrows(f, new HttpRequestException("upstream failed"));
        var request = Request(tier: 2);
        f.Delivery.GetGenerationSnapshotAsync(request.GenerationId, Arg.Any<CancellationToken>())
            .Returns(Row(request.GenerationId, retryCount: FailedBuildRefunder.RetryLimit));

        await f.Sut.EnqueueAsync(request);

        await f.Refunder.Received(1).TryRefundAsync(
            request.GenerationId, 2, RefundReason.BuildCouldNotFinish, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaidBuild_RestartedThreeTimesByDeploys_StillHasItsRetries_AndIsNotRefunded()
    {
        // attempt_count is the reconciler's restart budget. Counting it against the customer
        // refunded a build they could still retry, and told them they had used retries they had not.
        var f = Build();
        LlmThrows(f, new HttpRequestException("upstream failed"));
        var request = Request(tier: 2);
        f.Delivery.GetGenerationSnapshotAsync(request.GenerationId, Arg.Any<CancellationToken>())
            .Returns(Row(request.GenerationId, retryCount: 0, attemptCount: 3));

        await f.Sut.EnqueueAsync(request);

        await f.Refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    [Fact]
    public async Task PaidBuild_RetryCountIsReadAtTheFailure_NotWhenTheRunStarted()
    {
        // The row read at the start of the run fails (the store blinks) and comes back for the
        // read at the failure. A count taken at the start would have been "none used".
        var f = Build();
        LlmThrows(f, new HttpRequestException("upstream failed"));
        var request = Request(tier: 2);
        f.Delivery.GetGenerationSnapshotAsync(request.GenerationId, Arg.Any<CancellationToken>())
            .Returns((GenerationSnapshot?)null, Row(request.GenerationId, retryCount: 3));

        await f.Sut.EnqueueAsync(request);

        await f.Refunder.Received(1).TryRefundAsync(
            request.GenerationId, 2, RefundReason.BuildCouldNotFinish, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedBuild_StaysInFlightUntilTheFailedStatusAndTheRefundAreDone()
    {
        // Released first, a sweep running during the failed write could see the row as orphaned
        // and re-enqueue it, and the refund below would then pay back a build that goes on to
        // deliver.
        var f = Build();
        LlmThrows(f, new InvalidOperationException("template bug"));
        var request = Request(tier: 2);
        var inFlightDuringWrite = false;
        var inFlightDuringRefund = false;
        f.Delivery.UpdateStatusAsync(
                request.GenerationId, GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                inFlightDuringWrite = f.InFlight.Contains(request.GenerationId);
                return Task.CompletedTask;
            });
        f.Refunder.TryRefundAsync(request.GenerationId, 2, RefundReason.BuildCouldNotFinish, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                inFlightDuringRefund = f.InFlight.Contains(request.GenerationId);
                return Task.CompletedTask;
            });

        await f.Sut.EnqueueAsync(request);

        inFlightDuringWrite.Should().BeTrue();
        inFlightDuringRefund.Should().BeTrue();
        f.InFlight.Contains(request.GenerationId).Should().BeFalse();
    }

    [Fact]
    public async Task CancelledByTheCallersToken_IsAnInterruption_NotAFailure()
    {
        // The host stopping (a deploy) cancels the token mid-codegen. Writing 'failed' made every
        // deploy fail the builds in flight; a refund here would refund them too.
        var f = Build();
        using var stopping = new CancellationTokenSource();
        f.Llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LlmCallOptions?>(), Arg.Any<CancellationToken>())
            .Returns<Task<LlmResponse>>(_ =>
            {
                stopping.Cancel();
                throw new OperationCanceledException(stopping.Token);
            });
        var request = Request(tier: 2);

        var response = await f.Sut.EnqueueAsync(request, stopping.Token);

        response.Status.Should().NotBe("failed");
        await f.Delivery.DidNotReceive().UpdateStatusAsync(
            Arg.Any<string>(), GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await f.Refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
        f.InFlight.Contains(request.GenerationId).Should().BeFalse("the reconciler has to see the row to re-enqueue it");
        f.Queue.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task CancellationThatIsNotTheCallers_IsStillAFailure()
    {
        // An HttpClient timeout surfaces as TaskCanceledException with the caller's token still
        // live. That is a real failure of this attempt: failed, network, retryable, no refund.
        var f = Build();
        LlmThrows(f, new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        var request = Request(tier: 2);

        var response = await f.Sut.EnqueueAsync(request, CancellationToken.None);

        response.Status.Should().Be("failed");
        await f.Delivery.Received(1).UpdateStatusAsync(
            request.GenerationId, GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            ErrorCategorizer.Network, Arg.Any<CancellationToken>());
        await f.Refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }
}
