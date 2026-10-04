using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using Stripe;
using Stripe.Checkout;

namespace StackAlchemist.Engine.Tests.Webhooks;

/// <summary>
/// Unit tests for <see cref="StripeWebhookHandler"/> against a substituted <see cref="IBillingStore"/>:
/// which store calls each event makes, in what order, and how the handler answers Stripe.
/// The store's own SQL (one transaction per replayed checkout, the refund CAS) is covered against
/// a real Postgres in <c>PostgresBillingStoreTests</c>.
/// </summary>
public sealed class StripeWebhookTests
{
    private static readonly CheckoutOutcome NewAdvancedCheckout =
        new(IsNew: true, Mode: "advanced", Prompt: null, ProjectType: ProjectType.DotNetNextJs, Schema: null, Personalization: null);

    private static readonly CheckoutOutcome Redelivered =
        new(IsNew: false, Mode: null, Prompt: null, ProjectType: null, Schema: null, Personalization: null);

    private static (StripeWebhookHandler Sut, IBillingStore Billing, IGenerationOrchestrator Orchestrator) BuildSut()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.TryRecordEventAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EventRecord.New);

        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var email = Substitute.For<IEmailService>();
        var sut = new StripeWebhookHandler(orchestrator, billing, email, NullLogger<StripeWebhookHandler>.Instance);
        return (sut, billing, orchestrator);
    }

    private static void CheckoutReturns(IBillingStore billing, CheckoutOutcome outcome) =>
        billing.ProcessCheckoutCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(outcome);

    [Fact]
    public async Task ChargeRefunded_MarksTransactionRefundedAndCancelsGeneration()
    {
        var (sut, billing, _) = BuildSut();
        billing.UpdateTransactionsAsync(
                TransactionKey.StripePaymentIntent, "pi_42", "refunded", "evt_refund_1", true, Arg.Any<CancellationToken>())
            .Returns("gen-42");

        var stripeEvent = new Event
        {
            Id = "evt_refund_1",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_1", PaymentIntentId = "pi_42", Refunded = true },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        Received.InOrder(() =>
        {
            billing.TryRecordEventAsync("evt_refund_1", "charge.refunded", Arg.Any<CancellationToken>());
            billing.UpdateTransactionsAsync(
                TransactionKey.StripePaymentIntent, "pi_42", "refunded", "evt_refund_1", true, Arg.Any<CancellationToken>());
            billing.CancelUndeliveredGenerationAsync("gen-42", Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CheckoutAsyncPaymentFailed_MarksTransactionFailed()
    {
        var (sut, billing, orchestrator) = BuildSut();

        var stripeEvent = new Event
        {
            Id = "evt_failed_1",
            Type = "checkout.session.async_payment_failed",
            Data = new EventData { Object = new Session { Id = "cs_1" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        await billing.Received(1).UpdateTransactionsAsync(
            TransactionKey.StripeSessionId, "cs_1", "failed", "evt_failed_1", false, Arg.Any<CancellationToken>());
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisputeCreated_MarksTransactionDisputed()
    {
        var (sut, billing, _) = BuildSut();

        var stripeEvent = new Event
        {
            Id = "evt_dispute_1",
            Type = "charge.dispute.created",
            Data = new EventData { Object = new Dispute { ChargeId = "ch_99", Reason = "fraudulent" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        await billing.Received(1).UpdateTransactionsAsync(
            TransactionKey.StripeChargeId, "ch_99", "disputed", "evt_dispute_1", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateCheckoutEvent_IsSkipped()
    {
        // The checkout call reports is_new=false → handler short-circuits without enqueueing.
        var (sut, billing, orchestrator) = BuildSut();
        CheckoutReturns(billing, Redelivered);

        var stripeEvent = new Event
        {
            Id = "evt_dup_1",
            Type = "checkout.session.completed",
            Data = new EventData { Object = new Session { Id = "cs_dup" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("duplicate");
        result.Retry.Should().BeFalse();
        await billing.Received(1).ProcessCheckoutCompletedAsync(
            "evt_dup_1", "checkout.session.completed", "cs_dup", Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await billing.DidNotReceiveWithAnyArgs().DeleteEventAsync(default!, default);
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateNonCheckoutEvent_IsSkipped()
    {
        // The event is already in stripe_events → handler short-circuits.
        var (sut, billing, _) = BuildSut();
        billing.TryRecordEventAsync("evt_dup_2", "charge.refunded", Arg.Any<CancellationToken>())
            .Returns(EventRecord.Duplicate);

        var stripeEvent = new Event
        {
            Id = "evt_dup_2",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_dup", PaymentIntentId = "pi_dup" },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("duplicate");
        await billing.DidNotReceiveWithAnyArgs().UpdateTransactionsAsync(default, default!, default!, default!, default, default);
        await billing.DidNotReceiveWithAnyArgs().CancelUndeliveredGenerationAsync(default!, default!, default);
    }

    [Fact]
    public async Task NonCheckoutEvent_IdempotencyUnavailable_RequestsRetry()
    {
        // stripe_events cannot be reached → no fail-open: ask Stripe to redeliver
        // instead of processing unlogged (concurrent duplicates double-processed before).
        var (sut, billing, _) = BuildSut();
        billing.TryRecordEventAsync("evt_unavail_1", "charge.refunded", Arg.Any<CancellationToken>())
            .Returns(EventRecord.Unavailable);

        var stripeEvent = new Event
        {
            Id = "evt_unavail_1",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_x", PaymentIntentId = "pi_x" },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue();
        // The transaction update must not run unlogged.
        await billing.DidNotReceiveWithAnyArgs().UpdateTransactionsAsync(default, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UnhandledEventType_ReturnsNotProcessed()
    {
        var (sut, _, _) = BuildSut();

        var stripeEvent = new Event
        {
            Id = "evt_other_1",
            Type = "invoice.payment_succeeded",
            Data = new EventData { Object = new Invoice() },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().StartWith("unhandled:");
    }

    private static Event CheckoutCompletedEvent(string eventId = "evt_completed_1") => new()
    {
        Id = eventId,
        Type = "checkout.session.completed",
        Data = new EventData
        {
            Object = new Session
            {
                Id = "cs_77",
                PaymentIntentId = "pi_77",
                AmountTotal = 59_900,
                Metadata = new Dictionary<string, string>
                {
                    ["generationId"] = "gen-77",
                    ["tier"] = "2",
                },
            },
        },
    };

    [Fact]
    public async Task CheckoutCompleted_RunsAtomicRpcThenEnqueues()
    {
        // One store call replaces the old insert→load→tier-update→upsert sequence: event
        // recording, tier update, and transaction upsert are one Postgres transaction, so a
        // partial failure can no longer strand a paid generation.
        var (sut, billing, orchestrator) = BuildSut();
        CheckoutReturns(billing, NewAdvancedCheckout);

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.Retry.Should().BeFalse();

        await billing.Received(1).ProcessCheckoutCompletedAsync(
            "evt_completed_1", "checkout.session.completed", "cs_77", "pi_77",
            "gen-77", 2, 59_900, Arg.Any<CancellationToken>());
        // checkout.session.completed owns its idempotency inside that call; the separate event
        // log is never written first.
        await billing.DidNotReceiveWithAnyArgs().TryRecordEventAsync(default!, default!, default);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r =>
                r.GenerationId == "gen-77" && r.Tier == 2 && r.Mode == "advanced"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutCompleted_RpcFailure_RequestsRetryWithoutEnqueue()
    {
        var (sut, billing, orchestrator) = BuildSut();
        billing.ProcessCheckoutCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<CheckoutOutcome>>(_ => throw new InvalidOperationException("process_checkout_completed failed (08006)"));

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue("the checkout call rolled back, so Stripe's redelivery re-processes cleanly");
        await billing.DidNotReceiveWithAnyArgs().DeleteEventAsync(default!, default);
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutCompleted_EnqueueThrows_CompensatesEventAndRetries()
    {
        var (sut, billing, orchestrator) = BuildSut();
        CheckoutReturns(billing, NewAdvancedCheckout);

        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GenerateResponse>>(_ => throw new InvalidOperationException("queue full"));

        var result = await sut.HandleAsync(CheckoutCompletedEvent("evt_comp_2"), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue();

        // Compensation: the event row is deleted so the checkout call sees a fresh insert on
        // Stripe's redelivery instead of short-circuiting as a duplicate.
        Received.InOrder(() =>
        {
            billing.ProcessCheckoutCompletedAsync(
                "evt_comp_2", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
            billing.DeleteEventAsync("evt_comp_2", Arg.Any<CancellationToken>());
        });
        await billing.Received(1).DeleteEventAsync("evt_comp_2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutCompleted_EnqueueThrowsAfterRequestAborted_StillRunsCompensatingDelete()
    {
        // The enqueue can fail because the request was aborted. The compensation must not ride
        // that token: a delete skipped on an already-cancelled token would leave the event
        // recorded, and Stripe's redelivery would then report "duplicate" for a paid checkout.
        // The abort happens inside the enqueue (the checkout call ran on a live token), so the
        // delete must arrive on a token that is not cancelled.
        var (sut, billing, orchestrator) = BuildSut();
        CheckoutReturns(billing, NewAdvancedCheckout);

        using var requestAborted = new CancellationTokenSource();
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GenerateResponse>>(_ =>
            {
                requestAborted.Cancel();
                throw new OperationCanceledException(requestAborted.Token);
            });

        var result = await sut.HandleAsync(CheckoutCompletedEvent("evt_comp_3"), requestAborted.Token);

        requestAborted.IsCancellationRequested.Should().BeTrue();
        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue();

        await billing.Received(1).ProcessCheckoutCompletedAsync(
            "evt_comp_3", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await billing.Received(1).DeleteEventAsync(
            "evt_comp_3", Arg.Is<CancellationToken>(t => !t.IsCancellationRequested));
    }

    [Fact]
    public async Task CheckoutCompleted_WithoutBillingStore_EnqueuesDirectly()
    {
        // No store registered (local dev): no idempotency log, so the paid generation is
        // enqueued straight from the session metadata.
        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var email = Substitute.For<IEmailService>();
        var sut = new StripeWebhookHandler(orchestrator, billing: null, email, NullLogger<StripeWebhookHandler>.Instance);

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.Retry.Should().BeFalse();
        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r =>
                r.GenerationId == "gen-77" && r.Tier == 2 && r.Mode == "advanced" &&
                r.ProjectType == ProjectType.DotNetNextJs),
            Arg.Any<CancellationToken>());
    }
}
