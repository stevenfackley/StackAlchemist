using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Services;
using Stripe;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit tests for <see cref="StripeRefundService"/>. The billing store is a substitute
/// <see cref="IBillingStore"/> (its SQL, including the completed → refund_pending CAS, is covered
/// against a real Postgres in <c>PostgresBillingStoreTests</c>); the Stripe SDK call is
/// intercepted by substituting <see cref="RefundService"/> itself — its <c>CreateAsync</c> is
/// virtual and the class has a public parameterless constructor, so NSubstitute can proxy it
/// directly without touching the network or the ambient <see cref="StripeConfiguration.ApiKey"/>.
/// </summary>
public sealed class StripeRefundServiceTests
{
    private static IConfiguration Config(string? stripeKey = "sk_test_123") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = stripeKey,
            })
            .Build();

    /// <param name="withStore">False mirrors a host without DATABASE_URL: no billing store is registered.</param>
    private static (StripeRefundService Sut, IBillingStore Billing, RefundService StripeRefunds) BuildSut(
        IConfiguration? config = null, bool withStore = true)
    {
        var billing = Substitute.For<IBillingStore>();
        var stripeRefunds = Substitute.For<RefundService>();
        var sut = new StripeRefundService(
            withStore ? billing : null, config ?? Config(), stripeRefunds, NullLogger<StripeRefundService>.Instance);

        return (sut, billing, stripeRefunds);
    }

    [Fact]
    public async Task NoCompletedTransaction_ReturnsNotEligible_AndNeverCallsStripe()
    {
        var (sut, billing, stripeRefunds) = BuildSut();
        billing.FindCompletedTransactionAsync("gen-free-tier", Arg.Any<CancellationToken>())
            .Returns((EligibleTransaction?)null);

        var outcome = await sut.RefundFailedGenerationAsync("gen-free-tier", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        await billing.Received(1).FindCompletedTransactionAsync("gen-free-tier", Arg.Any<CancellationToken>());
        await billing.DidNotReceiveWithAnyArgs().TryClaimForRefundAsync(default!, default);
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedTransaction_ClaimsRowAndIssuesFullStripeRefund()
    {
        var (sut, billing, stripeRefunds) = BuildSut();
        billing.FindCompletedTransactionAsync("gen-42", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_123"));
        billing.TryClaimForRefundAsync("tx-1", Arg.Any<CancellationToken>()).Returns(true);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Refund { Id = "re_1" });

        var outcome = await sut.RefundFailedGenerationAsync("gen-42", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.Issued);

        // Lookup, then the claim, then Stripe: the refund is only created once the row is ours.
        Received.InOrder(() =>
        {
            billing.FindCompletedTransactionAsync("gen-42", Arg.Any<CancellationToken>());
            billing.TryClaimForRefundAsync("tx-1", Arg.Any<CancellationToken>());
            stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
        });

        await stripeRefunds.Received(1).CreateAsync(
            Arg.Is<RefundCreateOptions>(o =>
                o.PaymentIntent == "pi_123" &&
                o.Metadata["generation_id"] == "gen-42" &&
                o.Metadata["reason"] == "compile_guarantee"),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
        await billing.DidNotReceiveWithAnyArgs().RevertRefundClaimAsync(default!, default);
    }

    [Fact]
    public async Task ClaimLosesRace_ReturnsNotEligible_AndNeverCallsStripe()
    {
        // The lookup finds a 'completed' row, but the claim matches zero rows —
        // a concurrent caller (or a status change in between) already moved it
        // out of 'completed'.
        var (sut, billing, stripeRefunds) = BuildSut();
        billing.FindCompletedTransactionAsync("gen-race", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-2", "pi_456"));
        billing.TryClaimForRefundAsync("tx-2", Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await sut.RefundFailedGenerationAsync("gen-race", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
        await billing.DidNotReceiveWithAnyArgs().RevertRefundClaimAsync(default!, default);
    }

    [Fact]
    public async Task StripeCallThrows_RevertsClaimBackToCompleted_AndReturnsFailed()
    {
        var (sut, billing, stripeRefunds) = BuildSut();
        billing.FindCompletedTransactionAsync("gen-stripe-fail", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-3", "pi_789"));
        billing.TryClaimForRefundAsync("tx-3", Arg.Any<CancellationToken>()).Returns(true);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns<Refund>(_ => throw new StripeException("card issuer unreachable"));

        var outcome = await sut.RefundFailedGenerationAsync("gen-stripe-fail", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.Failed);

        // refund_pending → completed, after the failed Stripe call.
        Received.InOrder(() =>
        {
            billing.TryClaimForRefundAsync("tx-3", Arg.Any<CancellationToken>());
            stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
            billing.RevertRefundClaimAsync("tx-3", Arg.Any<CancellationToken>());
        });
        await billing.Received(1).RevertRefundClaimAsync("tx-3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CalledTwiceForSameGeneration_SecondCallIsNoOp()
    {
        // First call: happy path (claims + Stripe succeeds). Second call: the lookup
        // this time returns nothing because the row is no longer 'completed' —
        // exactly what happens for a real repeat invocation, since the first
        // call already flipped the status. This is the idempotency guarantee:
        // no second Stripe refund is ever created for the same generation.
        var (sut, billing, stripeRefunds) = BuildSut();
        billing.FindCompletedTransactionAsync("gen-dup", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-4", "pi_dup"), (EligibleTransaction?)null);
        billing.TryClaimForRefundAsync("tx-4", Arg.Any<CancellationToken>()).Returns(true);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Refund { Id = "re_dup" });

        var first = await sut.RefundFailedGenerationAsync("gen-dup", CancellationToken.None);
        var second = await sut.RefundFailedGenerationAsync("gen-dup", CancellationToken.None);

        first.Should().Be(RefundOutcome.Issued);
        second.Should().Be(RefundOutcome.NotEligible);

        await billing.Received(1).TryClaimForRefundAsync("tx-4", Arg.Any<CancellationToken>());
        await stripeRefunds.Received(1).CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StripeNotConfigured_ReturnsNotEligible_AndNeverTouchesTheStore()
    {
        var (sut, billing, stripeRefunds) = BuildSut(config: Config(stripeKey: null));

        var outcome = await sut.RefundFailedGenerationAsync("gen-no-stripe", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        billing.ReceivedCalls().Should().BeEmpty();
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreNotConfigured_ReturnsNotEligible_AndNeverCallsStripe()
    {
        var (sut, billing, stripeRefunds) = BuildSut(withStore: false);

        var outcome = await sut.RefundFailedGenerationAsync("gen-no-store", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        billing.ReceivedCalls().Should().BeEmpty();
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }
}
