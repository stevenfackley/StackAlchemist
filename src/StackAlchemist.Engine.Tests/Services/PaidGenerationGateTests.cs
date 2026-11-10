using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The payment check in front of <c>/api/generate</c>: a paid tier starts only for a completed
/// transaction, whatever tier the caller asked for.
/// </summary>
public sealed class PaidGenerationGateTests
{
    private const string GenerationId = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";

    private readonly IDeliveryService _delivery = Substitute.For<IDeliveryService>();
    private readonly IBillingStore _billing = Substitute.For<IBillingStore>();

    private PaidGenerationGate Sut(bool withBilling = true) =>
        new(_delivery, withBilling ? _billing : null, NullLogger<PaidGenerationGate>.Instance);

    private void RowHasTier(int tier) =>
        _delivery.GetGenerationSnapshotAsync(GenerationId, Arg.Any<CancellationToken>())
            .Returns(new GenerationSnapshot(
                GenerationId, "pending", tier, "simple", "a prompt", ProjectType.DotNetNextJs,
                Schema: null, Personalization: null, AttemptCount: 0, UpdatedAt: DateTimeOffset.UtcNow));

    private void PaymentIs(EligibleTransaction? transaction) =>
        _billing.FindCompletedTransactionAsync(GenerationId, Arg.Any<CancellationToken>()).Returns(transaction);

    [Fact]
    public async Task FreeTier_IsAllowed_WithoutAPaymentLookup()
    {
        RowHasTier(0);

        (await Sut().CheckAsync(GenerationId, requestedTier: 0, CancellationToken.None))
            .Should().Be(PaymentCheck.Allowed);

        await _billing.DidNotReceiveWithAnyArgs().FindCompletedTransactionAsync(default!, default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PaidTier_WithoutACompletedTransaction_IsUnpaid(int tier)
    {
        PaymentIs(null);

        (await Sut().CheckAsync(GenerationId, tier, CancellationToken.None))
            .Should().Be(PaymentCheck.Unpaid);
    }

    [Fact]
    public async Task PaidTier_WithACompletedTransaction_IsAllowed()
    {
        PaymentIs(new EligibleTransaction("txn_1", "pi_1"));

        (await Sut().CheckAsync(GenerationId, requestedTier: 2, CancellationToken.None))
            .Should().Be(PaymentCheck.Allowed);
    }

    [Fact]
    public async Task FreeRequest_ForAPaidRow_IsCheckedAsThePaidTier()
    {
        // The orchestrator builds the higher of the two tiers, so that is the one that must be paid.
        RowHasTier(3);
        PaymentIs(null);

        (await Sut().CheckAsync(GenerationId, requestedTier: 0, CancellationToken.None))
            .Should().Be(PaymentCheck.Unpaid);
    }

    [Fact]
    public async Task FreeRequest_ForARowThatDoesNotExist_IsAllowed()
    {
        _delivery.GetGenerationSnapshotAsync(GenerationId, Arg.Any<CancellationToken>())
            .Returns((GenerationSnapshot?)null);

        (await Sut().CheckAsync(GenerationId, requestedTier: 0, CancellationToken.None))
            .Should().Be(PaymentCheck.Allowed);
    }

    [Fact]
    public async Task PaidTier_WhenTheLookupFails_IsUnavailable_NotAllowed()
    {
        _billing.FindCompletedTransactionAsync(GenerationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("store down"));

        (await Sut().CheckAsync(GenerationId, requestedTier: 2, CancellationToken.None))
            .Should().Be(PaymentCheck.Unavailable);
    }

    [Fact]
    public async Task PaidTier_WithNoBillingStore_IsAllowed()
    {
        // DATABASE_URL unset: local runs only, Production refuses to start without it.
        (await Sut(withBilling: false).CheckAsync(GenerationId, requestedTier: 2, CancellationToken.None))
            .Should().Be(PaymentCheck.Allowed);
    }
}
