namespace StackAlchemist.Engine.Services;

/// <summary>Why a paid build is being refunded. It picks the email the customer gets.</summary>
public enum RefundReason
{
    /// <summary>The build still failed after every automatic correction: the Compile Guarantee.</summary>
    CompileRetriesExhausted,

    /// <summary>The build ended some other way that leaves the customer nothing to retry.</summary>
    BuildCouldNotFinish,
}

/// <summary>
/// Refunds a paid build that has ended with no retry left for the customer, and emails them once
/// the refund is actually initiated.
/// </summary>
public interface IFailedBuildRefunder
{
    /// <summary>
    /// Best effort and never throws: the generation's failed status is already written, and a
    /// refund, store or email failure must not undo it or stop the caller. The refund service
    /// only refunds a completed transaction, so an unpaid or already-refunded build is a no-op.
    /// </summary>
    Task TryRefundAsync(string generationId, int tier, RefundReason reason, CancellationToken ct);

    /// <summary>
    /// Whether a paid build that has just failed with this category leaves its customer no
    /// retry: the build ran out of automatic corrections, the category offers none, or the owner
    /// has used every retry. Reads the row's retry count now, at the failure, not when the run
    /// started, and only when the category alone does not decide. Never throws; when the count
    /// cannot be read the answer is false and nothing is refunded on a guess.
    /// </summary>
    Task<bool> ShouldRefundAsync(string generationId, int tier, string errorCategory);
}

public static class FailedBuildRefunderExtensions
{
    /// <summary>
    /// What a failure site calls once the failed status is written: refunds the build when
    /// <see cref="IFailedBuildRefunder.ShouldRefundAsync"/> says the customer has no retry left,
    /// with the email <see cref="FailedBuildRefunder.ReasonFor"/> picks. Never on a caller's
    /// token: the row is already failed, and a host that starts stopping now would cancel the
    /// refund with nothing left to try it again. Never throws.
    /// </summary>
    public static async Task RefundIfOwedAsync(
        this IFailedBuildRefunder refunder, string generationId, int tier, string errorCategory)
    {
        if (await refunder.ShouldRefundAsync(generationId, tier, errorCategory))
        {
            await refunder.TryRefundAsync(
                generationId, tier, FailedBuildRefunder.ReasonFor(errorCategory), CancellationToken.None);
        }
    }
}

/// <inheritdoc />
public sealed partial class FailedBuildRefunder(
    IRefundService refundService,
    IDeliveryService deliveryService,
    IEmailService emailService,
    ILogger logger) : IFailedBuildRefunder
{
    /// <summary>
    /// How many times the owner may retry a failed build: the web refuses a retry once the
    /// row's <c>retry_count</c> reaches this (<c>MAX_RETRIES</c> in
    /// <c>lib/generation-outcome.ts</c>; a web test pins the two together). The reconciler's
    /// automatic restarts are counted apart, in <c>attempt_count</c>.
    /// </summary>
    public const int RetryLimit = 3;

    /// <summary>
    /// The refund rule: a paid tier is refunded when its failure leaves the customer no retry.
    /// This overload is the half that needs no row: a build that still failed after every
    /// automatic correction (<see cref="ErrorCategorizer.Build"/>, the Compile Guarantee), or a
    /// category the result page offers no Retry for. <see cref="ShouldRefundAsync"/> adds the
    /// other half, every retry used. The result page mirrors both halves
    /// (<c>failureOutcome</c> in <c>lib/generation-outcome.ts</c>; a web test checks the build
    /// case). Refunding a failure the customer can retry would turn it into a lost sale: the
    /// refund takes the transaction out of <c>completed</c>, and <see cref="PaidGenerationGate"/>
    /// then refuses the retry.
    /// </summary>
    public static bool ShouldRefund(int tier, string errorCategory) =>
        tier >= 1
        && (errorCategory == ErrorCategorizer.Build || !ErrorCategorizer.OffersRetry(errorCategory));

    /// <summary>
    /// Which email a refund for this failure sends: the Compile Guarantee one for a build that
    /// still failed after every automatic correction, the could-not-finish one for anything else.
    /// </summary>
    public static RefundReason ReasonFor(string errorCategory) =>
        errorCategory == ErrorCategorizer.Build
            ? RefundReason.CompileRetriesExhausted
            : RefundReason.BuildCouldNotFinish;

    public async Task<bool> ShouldRefundAsync(string generationId, int tier, string errorCategory)
    {
        if (tier < 1)
            return false;
        if (ShouldRefund(tier, errorCategory))
            return true;

        try
        {
            // Not on a caller's token: this decides a refund for a row that is already failed.
            // The count alone, not the snapshot: a row whose JSON no longer parses must still be
            // refunded when its retries are used up.
            var retries = await deliveryService.GetRetryCountAsync(generationId, CancellationToken.None);
            return retries >= RetryLimit;
        }
        catch (Exception ex)
        {
            LogRetryCountUnreadable(logger, ex, generationId);
            return false;
        }
    }

    public async Task TryRefundAsync(string generationId, int tier, RefundReason reason, CancellationToken ct)
    {
        try
        {
            var outcome = await refundService.RefundFailedGenerationAsync(generationId, ct);
            if (outcome != RefundOutcome.Issued)
            {
                LogRefundNotIssued(logger, generationId, reason, outcome);
                return;
            }

            LogRefundIssued(logger, generationId, reason);

            var ownerEmail = await deliveryService.GetGenerationOwnerEmailAsync(generationId, ct);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                var (subject, html) = reason == RefundReason.CompileRetriesExhausted
                    ? EmailTemplates.RefundIssued(tier)
                    : EmailTemplates.RefundIssuedForUnfinishedBuild(tier);
                await emailService.SendAsync(ownerEmail, subject, html, ct);
            }
        }
        catch (Exception ex)
        {
            LogRefundAttemptFailed(logger, ex, generationId, reason);
        }
    }

    // Ids 608-610 came here from CompileWorkerService with the code they log.

    [LoggerMessage(EventId = 608, Level = LogLevel.Information, Message = "Refund issued for failed generation {Id} ({Reason})")]
    private static partial void LogRefundIssued(ILogger logger, string id, RefundReason reason);

    [LoggerMessage(EventId = 609, Level = LogLevel.Information, Message = "Refund not issued for failed generation {Id} ({Reason}): {Outcome}")]
    private static partial void LogRefundNotIssued(ILogger logger, string id, RefundReason reason, RefundOutcome outcome);

    [LoggerMessage(EventId = 610, Level = LogLevel.Error, Message = "Refund attempt threw for failed generation {Id} ({Reason}); the generation stays failed and is NOT refunded")]
    private static partial void LogRefundAttemptFailed(ILogger logger, Exception ex, string id, RefundReason reason);

    [LoggerMessage(EventId = 615, Level = LogLevel.Error, Message = "Could not read the retry count of failed generation {Id}; it is NOT refunded on a guess")]
    private static partial void LogRetryCountUnreadable(ILogger logger, Exception ex, string id);
}
