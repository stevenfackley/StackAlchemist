using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using Stripe;
using Stripe.Checkout;

namespace StackAlchemist.Engine.Tests.Webhooks;

/// <summary>
/// Money-path behaviour of <see cref="StripeWebhookHandler"/> against any <see cref="IBillingStore"/>:
/// delayed payment methods, the async-success event, a checkout for a missing generation (#419),
/// the full stored prompt over the clipped metadata copy, and disputes matched by payment intent (#423).
/// </summary>
public sealed class StripeWebhookMoneyPathTests
{
    private readonly IBillingStore _billing = Substitute.For<IBillingStore>();
    private readonly IGenerationOrchestrator _orchestrator = Substitute.For<IGenerationOrchestrator>();

    private StripeWebhookHandler Sut() =>
        new(_orchestrator, _billing, Substitute.For<IEmailService>(), NullLogger<StripeWebhookHandler>.Instance);

    private static Event SessionEvent(string type, string? paymentStatus, string prompt = "short prompt") => new()
    {
        Id = $"evt_{type}",
        Type = type,
        Data = new EventData
        {
            Object = new Session
            {
                Id = "cs_1",
                PaymentIntentId = "pi_1",
                PaymentStatus = paymentStatus,
                AmountTotal = 29_900,
                Metadata = new Dictionary<string, string>
                {
                    ["generationId"] = "8f9a3c1e-0000-4000-8000-000000000001",
                    ["tier"] = "1",
                    ["prompt"] = prompt,
                },
            },
        },
    };

    private void RpcReturns(CheckoutOutcome outcome) =>
        _billing.ProcessCheckoutCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(outcome);

    [Fact]
    public async Task CompletedButUnpaid_DelayedPaymentMethod_IsDeferredWithoutRecordingOrBuilding()
    {
        var result = await Sut().HandleAsync(SessionEvent("checkout.session.completed", "unpaid"), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("awaiting_async_payment");
        result.Retry.Should().BeFalse("Stripe sends async_payment_succeeded later; a retry would change nothing");
        await _billing.DidNotReceiveWithAnyArgs().ProcessCheckoutCompletedAsync(default!, default!, default!, default, default!, default, default, default);
        await _orchestrator.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task AsyncPaymentSucceeded_IsThePaidMomentAndBuilds()
    {
        RpcReturns(new CheckoutOutcome(true, "simple", "short prompt", ProjectType.DotNetNextJs, null, null));

        var result = await Sut().HandleAsync(
            SessionEvent("checkout.session.async_payment_succeeded", "paid"), CancellationToken.None);

        result.Processed.Should().BeTrue();
        await _billing.Received(1).ProcessCheckoutCompletedAsync(
            "evt_checkout.session.async_payment_succeeded", "checkout.session.async_payment_succeeded",
            "cs_1", "pi_1", "8f9a3c1e-0000-4000-8000-000000000001", 1, 29_900, Arg.Any<CancellationToken>());
        await _orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.Tier == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaidCheckoutForAMissingGeneration_IsRecordedButNeitherBuiltNorRetried()
    {
        // generations.mode is NOT NULL, so the function's null mode means "row does not exist".
        RpcReturns(new CheckoutOutcome(true, null, null, null, null, null));

        var result = await Sut().HandleAsync(SessionEvent("checkout.session.completed", "paid"), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("generation_missing");
        result.Retry.Should().BeFalse("the payment and event are recorded; redelivery would change nothing");
        await _orchestrator.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task Checkout_BuildsWithTheFullStoredPromptNotTheClippedMetadataCopy()
    {
        var full = new string('x', 1_500);
        RpcReturns(new CheckoutOutcome(true, "simple", full, ProjectType.DotNetNextJs, null, null));

        await Sut().HandleAsync(
            SessionEvent("checkout.session.completed", "paid", prompt: StripeMetadata.Clip(full)), CancellationToken.None);

        await _orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.Prompt == full), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisputeCreated_MatchesTheTransactionByPaymentIntent()
    {
        _billing.TryRecordEventAsync("evt_dispute", "charge.dispute.created", Arg.Any<CancellationToken>())
            .Returns(EventRecord.New);
        var dispute = new Event
        {
            Id = "evt_dispute",
            Type = "charge.dispute.created",
            Data = new EventData { Object = new Dispute { ChargeId = "ch_1", PaymentIntentId = "pi_1", Reason = "fraudulent" } },
        };

        var result = await Sut().HandleAsync(dispute, CancellationToken.None);

        result.Processed.Should().BeTrue();
        await _billing.Received(1).UpdateTransactionsAsync(
            TransactionKey.StripePaymentIntent, "pi_1", "disputed", "evt_dispute", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisputeWithoutAPaymentIntent_FallsBackToTheChargeId()
    {
        _billing.TryRecordEventAsync("evt_dispute2", "charge.dispute.created", Arg.Any<CancellationToken>())
            .Returns(EventRecord.New);
        var dispute = new Event
        {
            Id = "evt_dispute2",
            Type = "charge.dispute.created",
            Data = new EventData { Object = new Dispute { ChargeId = "ch_2", Reason = "general" } },
        };

        await Sut().HandleAsync(dispute, CancellationToken.None);

        await _billing.Received(1).UpdateTransactionsAsync(
            TransactionKey.StripeChargeId, "ch_2", "disputed", "evt_dispute2", false, Arg.Any<CancellationToken>());
    }
}
