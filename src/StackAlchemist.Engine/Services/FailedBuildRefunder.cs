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
}

/// <inheritdoc />
public sealed partial class FailedBuildRefunder(
    IRefundService refundService,
    IDeliveryService deliveryService,
    IEmailService emailService,
    ILogger logger) : IFailedBuildRefunder
{
    /// <summary>
    /// The web's <c>retryGeneration</c> refuses a retry once the row's <c>attempt_count</c>
    /// reaches this. Only the reconciler's re-enqueues raise that count, and its own budget
    /// (<c>Generation:MaxAttempts</c>) defaults to the same number.
    /// </summary>
    public const int RetryAttemptLimit = 3;

    /// <summary>
    /// The rule for a failure outside the build-retry loop: refund a paid tier only when the
    /// customer has no retry left. That is a category the result page offers no Retry for, or a
    /// row at the attempt limit, where the Retry button is shown but refused. Refunding a failure
    /// the customer can retry would turn it into a lost sale: the refund takes the transaction
    /// out of <c>completed</c>, and <see cref="PaidGenerationGate"/> then refuses the retry.
    /// </summary>
    public static bool ShouldRefund(int tier, string errorCategory, int attemptCount = 0) =>
        tier >= 1 && (!ErrorCategorizer.OffersRetry(errorCategory) || attemptCount >= RetryAttemptLimit);

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
}
