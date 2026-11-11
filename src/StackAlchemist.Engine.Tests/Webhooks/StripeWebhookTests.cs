using FluentAssertions;
using Microsoft.Extensions.Hosting;
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
        var sut = new StripeWebhookHandler(
            new InlineGenerationDispatcher(orchestrator), billing, email, NullLogger<StripeWebhookHandler>.Instance);
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
    public async Task ChargePartiallyRefunded_LeavesTheTransactionAndTheGenerationAlone()
    {
        // Stripe sends charge.refunded for a partial refund too, with Refunded = false.
        var (sut, billing, _) = BuildSut();

        var stripeEvent = new Event
        {
            Id = "evt_partial_1",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_p", PaymentIntentId = "pi_p", Refunded = false, Amount = 59900, AmountRefunded = 5000 },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("partial_refund");
        result.Retry.Should().BeFalse();
        await billing.DidNotReceiveWithAnyArgs().UpdateTransactionsAsync(default, default!, default!, default!, default, default);
        await billing.DidNotReceiveWithAnyArgs().CancelUndeliveredGenerationAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("charge.refunded")]
    [InlineData("charge.dispute.created")]
    [InlineData("payment_intent.payment_failed")]
    [InlineData("checkout.session.async_payment_failed")]
    public async Task StatusWriteThrows_TakesTheEventBackOutAndRequestsRetry(string eventType)
    {
        // The event is recorded before the write. Left recorded, Stripe's redelivery would be
        // skipped as a duplicate and the status never written.
        var (sut, billing, _) = BuildSut();
        billing.UpdateTransactionsAsync(default, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException<string?>(new InvalidOperationException("transaction status update failed")));

        IHasObject payload = eventType switch
        {
            "charge.refunded" => new Charge { Id = "ch_t", PaymentIntentId = "pi_t", Refunded = true },
            "charge.dispute.created" => new Dispute { ChargeId = "ch_t", PaymentIntentId = "pi_t" },
            "payment_intent.payment_failed" => new PaymentIntent { Id = "pi_t" },
            _ => new Session { Id = "cs_t" },
        };
        var stripeEvent = new Event { Id = "evt_throw_1", Type = eventType, Data = new EventData { Object = payload } };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("status_write_failed");
        result.Retry.Should().BeTrue();
        await billing.Received(1).DeleteEventAsync("evt_throw_1", CancellationToken.None);
        await billing.DidNotReceiveWithAnyArgs().CancelUndeliveredGenerationAsync(default!, default!, default);
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

    /// <summary>The handler wired to the real background dispatcher, as production is.</summary>
    private static (StripeWebhookHandler Sut, IBillingStore Billing, IGenerationOrchestrator Orchestrator, CancellationTokenSource AppStopping)
        BuildSutWithBackgroundDispatch()
    {
        var billing = Substitute.For<IBillingStore>();
        CheckoutReturns(billing, NewAdvancedCheckout);

        var appStopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(appStopping.Token);

        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var dispatcher = new BackgroundGenerationDispatcher(
            orchestrator, lifetime, NullLogger<BackgroundGenerationDispatcher>.Instance);
        var sut = new StripeWebhookHandler(
            dispatcher, billing, Substitute.For<IEmailService>(), NullLogger<StripeWebhookHandler>.Instance);
        return (sut, billing, orchestrator, appStopping);
    }

    [Fact]
    public async Task CheckoutCompleted_AnswersStripeWithoutWaitingForTheBuild()
    {
        // A tier 2 or 3 build spends minutes in LLM code generation inside EnqueueAsync. Stripe
        // expects its answer in seconds, so the handler must return while the build is running.
        var (sut, _, orchestrator, appStopping) = BuildSutWithBackgroundDispatch();
        using var _ = appStopping;
        var buildStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var buildNeverFinishes = new TaskCompletionSource<GenerateResponse>();
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                buildStarted.SetResult();
                return buildNeverFinishes.Task;
            });

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));

        result.Processed.Should().BeTrue();
        result.Retry.Should().BeFalse();
        await buildStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CheckoutCompleted_RequestAbortedAfterTheAnswer_DoesNotCancelTheBuild()
    {
        // Stripe hanging up, or the proxy's read timeout, aborts the request token. The build
        // runs on the app-lifetime token, so neither can cancel it.
        var (sut, _, orchestrator, appStopping) = BuildSutWithBackgroundDispatch();
        using var _ = appStopping;
        var buildToken = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                buildToken.SetResult(call.ArgAt<CancellationToken>(1));
                return new GenerateResponse { JobId = "gen-77", Status = "building" };
            });
        using var requestAborted = new CancellationTokenSource();

        await sut.HandleAsync(CheckoutCompletedEvent(), requestAborted.Token);
        await requestAborted.CancelAsync();

        var token = await buildToken.Task.WaitAsync(TimeSpan.FromSeconds(10));
        token.IsCancellationRequested.Should().BeFalse();
        token.Should().Be(appStopping.Token);
    }

    [Fact]
    public async Task CheckoutCompleted_BuildFailsToStart_KeepsThePaymentRecordedAndDoesNotAskForARetry()
    {
        // The payment and the event are already committed. Taking the event back out would make
        // Stripe redeliver, but the build no longer runs on the delivery, so there is nothing for
        // a redelivery to wait for: the row is pending and paid, and the reconciler starts it.
        var (sut, billing, orchestrator, appStopping) = BuildSutWithBackgroundDispatch();
        using var _ = appStopping;
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GenerateResponse>>(_ =>
            {
                attempted.SetResult();
                throw new InvalidOperationException("store unreachable");
            });

        var result = await sut.HandleAsync(CheckoutCompletedEvent("evt_comp_2"), CancellationToken.None);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        result.Processed.Should().BeTrue();
        result.Retry.Should().BeFalse();
        await billing.DidNotReceiveWithAnyArgs().DeleteEventAsync(default!, default);
    }

    [Fact]
    public async Task CheckoutCompleted_WithoutBillingStore_EnqueuesDirectly()
    {
        // No store registered (local dev): no idempotency log, so the paid generation is
        // enqueued straight from the session metadata.
        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var email = Substitute.For<IEmailService>();
        var sut = new StripeWebhookHandler(
            new InlineGenerationDispatcher(orchestrator), billing: null, email, NullLogger<StripeWebhookHandler>.Instance);

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
