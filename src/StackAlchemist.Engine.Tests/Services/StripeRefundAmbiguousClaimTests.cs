using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Services;
using Stripe;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>#424: a claim whose UPDATE may have committed before the failure must not strand the row.</summary>
public sealed class StripeRefundAmbiguousClaimTests
{
    [Fact]
    public async Task ClaimThrows_RevertsTheClaimAndCallsNoStripeRefund()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-1", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_1"));
        billing.TryClaimForRefundAsync("tx-1", Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("connection lost after commit"));
        var stripeRefunds = Substitute.For<RefundService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Stripe:SecretKey"] = "sk_test_x" })
            .Build();
        var sut = new StripeRefundService(billing, config, stripeRefunds, NullLogger<StripeRefundService>.Instance);

        var outcome = await sut.RefundFailedGenerationAsync("gen-1", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.Failed);
        await billing.Received(1).RevertRefundClaimAsync("tx-1", Arg.Any<CancellationToken>());
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }
}
