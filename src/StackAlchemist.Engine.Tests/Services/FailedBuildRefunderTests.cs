using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class FailedBuildRefunderTests
{
    private readonly IRefundService _refund = Substitute.For<IRefundService>();
    private readonly IDeliveryService _delivery = Substitute.For<IDeliveryService>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    private FailedBuildRefunder Sut() => new(_refund, _delivery, _email, NullLogger<FailedBuildRefunder>.Instance);

    // ── The rule ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, ErrorCategorizer.Internal, true)]
    [InlineData(2, ErrorCategorizer.Internal, true)]
    [InlineData(3, ErrorCategorizer.Internal, true)]
    // The result page shows Retry for these; a refund would make that Retry answer 402.
    [InlineData(2, ErrorCategorizer.Network, false)]
    [InlineData(2, ErrorCategorizer.RateLimit, false)]
    [InlineData(2, ErrorCategorizer.Schema, false)]
    [InlineData(2, ErrorCategorizer.Build, false)]
    // Nothing was paid.
    [InlineData(0, ErrorCategorizer.Internal, false)]
    public void ShouldRefund_PaidTierAndACategoryWithNoRetry(int tier, string category, bool expected) =>
        FailedBuildRefunder.ShouldRefund(tier, category).Should().Be(expected);

    private static GenerationSnapshot Row(int retryCount, int attemptCount = 0) =>
        new("gen-1", "failed", 2, "advanced", null, null, null, null, attemptCount, DateTimeOffset.UtcNow)
        {
            RetryCount = retryCount,
        };

    [Theory]
    // Every retry used: the web refuses another, so the customer has nothing left to press.
    [InlineData(2, ErrorCategorizer.Network, 3, true)]
    [InlineData(2, ErrorCategorizer.Schema, 4, true)]
    [InlineData(2, ErrorCategorizer.Network, 2, false)]
    [InlineData(0, ErrorCategorizer.Network, 3, false)]
    public async Task ShouldRefundAsync_ARetryableCategoryWithEveryRetryUsedHasNoRetryLeft(
        int tier, string category, int retryCount, bool expected)
    {
        _delivery.GetGenerationSnapshotAsync("gen-1", Arg.Any<CancellationToken>()).Returns(Row(retryCount));

        (await Sut().ShouldRefundAsync("gen-1", tier, category)).Should().Be(expected);
    }

    [Fact]
    public async Task ShouldRefundAsync_AutomaticRestartsAreNotTheCustomersRetries()
    {
        // Three deploys restarted this build. The customer has pressed Retry once, and can again.
        _delivery.GetGenerationSnapshotAsync("gen-1", Arg.Any<CancellationToken>())
            .Returns(Row(retryCount: 1, attemptCount: 3));

        (await Sut().ShouldRefundAsync("gen-1", 2, ErrorCategorizer.Network)).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRefundAsync_ACategoryWithNoRetryNeedsNoRow()
    {
        (await Sut().ShouldRefundAsync("gen-1", 2, ErrorCategorizer.Internal)).Should().BeTrue();
        await _delivery.DidNotReceiveWithAnyArgs().GetGenerationSnapshotAsync(default!, default);
    }

    [Fact]
    public async Task ShouldRefundAsync_ReadsTheCountOnATokenTheStoppingHostCannotCancel()
    {
        _delivery.GetGenerationSnapshotAsync("gen-1", Arg.Any<CancellationToken>()).Returns(Row(retryCount: 3));

        await Sut().ShouldRefundAsync("gen-1", 2, ErrorCategorizer.Network);

        await _delivery.Received(1).GetGenerationSnapshotAsync("gen-1", CancellationToken.None);
    }

    [Fact]
    public async Task ShouldRefundAsync_WhenTheRowCannotBeRead_RefundsNothingOnAGuess()
    {
        _delivery.GetGenerationSnapshotAsync("gen-missing", Arg.Any<CancellationToken>())
            .Returns((GenerationSnapshot?)null);
        _delivery.GetGenerationSnapshotAsync("gen-throws", Arg.Any<CancellationToken>())
            .Returns<Task<GenerationSnapshot?>>(_ => throw new InvalidOperationException("db down"));

        (await Sut().ShouldRefundAsync("gen-missing", 2, ErrorCategorizer.Network)).Should().BeFalse();
        (await Sut().ShouldRefundAsync("gen-throws", 2, ErrorCategorizer.Network)).Should().BeFalse();
    }

    [Theory]
    // Must match RETRYABLE in src/StackAlchemist.Web/src/lib/generation-outcome.ts (a web test checks it).
    [InlineData(ErrorCategorizer.Quota, false)]
    [InlineData(ErrorCategorizer.Schema, true)]
    [InlineData(ErrorCategorizer.Build, true)]
    [InlineData(ErrorCategorizer.RateLimit, true)]
    [InlineData(ErrorCategorizer.Network, true)]
    [InlineData(ErrorCategorizer.Internal, false)]
    public void OffersRetry_MirrorsTheResultPage(string category, bool expected) =>
        ErrorCategorizer.OffersRetry(category).Should().Be(expected);

    // ── The refund and its email ─────────────────────────────────────────────

    [Fact]
    public async Task Issued_CompileRetriesExhausted_SendsTheCompileGuaranteeEmail()
    {
        _refund.RefundFailedGenerationAsync("gen-1", Arg.Any<CancellationToken>()).Returns(RefundOutcome.Issued);
        _delivery.GetGenerationOwnerEmailAsync("gen-1", Arg.Any<CancellationToken>()).Returns("owner@example.com");

        await Sut().TryRefundAsync("gen-1", 2, RefundReason.CompileRetriesExhausted, CancellationToken.None);

        var (subject, html) = EmailTemplates.RefundIssued(2);
        await _email.Received(1).SendAsync("owner@example.com", subject, html, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issued_BuildCouldNotFinish_DoesNotClaimTheBuildFailedToCompile()
    {
        _refund.RefundFailedGenerationAsync("gen-1", Arg.Any<CancellationToken>()).Returns(RefundOutcome.Issued);
        _delivery.GetGenerationOwnerEmailAsync("gen-1", Arg.Any<CancellationToken>()).Returns("owner@example.com");

        await Sut().TryRefundAsync("gen-1", 3, RefundReason.BuildCouldNotFinish, CancellationToken.None);

        var (subject, html) = EmailTemplates.RefundIssuedForUnfinishedBuild(3);
        html.Should().Contain("full refund").And.Contain("tier 3")
            .And.NotContain("compile").And.NotContain("Compile Guarantee");
        await _email.Received(1).SendAsync("owner@example.com", subject, html, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RefundOutcome.NotEligible)]
    [InlineData(RefundOutcome.Failed)]
    public async Task NotIssued_SendsNoEmail(RefundOutcome outcome)
    {
        // The email says a refund was initiated. It goes out only when one was.
        _refund.RefundFailedGenerationAsync("gen-1", Arg.Any<CancellationToken>()).Returns(outcome);

        await Sut().TryRefundAsync("gen-1", 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        await _delivery.DidNotReceiveWithAnyArgs().GetGenerationOwnerEmailAsync(default!, default);
    }

    [Fact]
    public async Task Issued_NoOwnerEmailOnFile_SendsNothingAndDoesNotThrow()
    {
        _refund.RefundFailedGenerationAsync("gen-1", Arg.Any<CancellationToken>()).Returns(RefundOutcome.Issued);
        _delivery.GetGenerationOwnerEmailAsync("gen-1", Arg.Any<CancellationToken>()).Returns((string?)null);

        await Sut().TryRefundAsync("gen-1", 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task NeverThrows_WhenTheRefundTheLookupOrTheEmailDoes()
    {
        _refund.RefundFailedGenerationAsync("gen-refund", Arg.Any<CancellationToken>())
            .Returns<Task<RefundOutcome>>(_ => throw new InvalidOperationException("Stripe unreachable"));
        _refund.RefundFailedGenerationAsync("gen-email", Arg.Any<CancellationToken>()).Returns(RefundOutcome.Issued);
        _delivery.GetGenerationOwnerEmailAsync("gen-email", Arg.Any<CancellationToken>()).Returns("owner@example.com");
        _email.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(_ => throw new HttpRequestException("Resend down"));

        var refundThrows = () => Sut().TryRefundAsync("gen-refund", 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);
        var emailThrows = () => Sut().TryRefundAsync("gen-email", 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);

        await refundThrows.Should().NotThrowAsync();
        await emailThrows.Should().NotThrowAsync();
    }
}
