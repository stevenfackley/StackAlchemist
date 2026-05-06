namespace StackAlchemist.Engine;

/// <summary>
/// LoggerMessage source-gen partials for the top-level statements in Program.cs.
/// Top-level statements have no class to host instance partials, so these live here
/// as a static class. Event IDs in 400–499 are reserved for Program.cs.
/// </summary>
internal static partial class ProgramLog
{
    [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "Schema extraction requested for generation {Id}")]
    public static partial void SchemaExtractRequested(this ILogger logger, string id);

    [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Schema extracted for {Id}: {Count} entities")]
    public static partial void SchemaExtracted(this ILogger logger, string id, int count);

    [LoggerMessage(EventId = 402, Level = LogLevel.Warning, Message = "Schema extraction failed for {Id}: {Msg}")]
    public static partial void SchemaExtractFailed(this ILogger logger, string id, string msg);

    [LoggerMessage(EventId = 403, Level = LogLevel.Warning, Message = "Schema validation failed for {Id}: {Msg}")]
    public static partial void SchemaValidationFailed(this ILogger logger, string id, string msg);

    [LoggerMessage(EventId = 404, Level = LogLevel.Information, Message = "Stripe session {SessionId} created for generation {GenId} (tier {Tier})")]
    public static partial void StripeSessionCreated(this ILogger logger, string sessionId, string genId, int tier);

    [LoggerMessage(EventId = 405, Level = LogLevel.Error, Message = "Stripe session creation failed for generation {GenId}")]
    public static partial void StripeSessionCreationFailed(this ILogger logger, Exception ex, string genId);

    [LoggerMessage(EventId = 406, Level = LogLevel.Warning, Message = "Stripe webhook signature verification failed: {Msg}")]
    public static partial void StripeSignatureVerifyFailed(this ILogger logger, string msg);

    [LoggerMessage(EventId = 407, Level = LogLevel.Information, Message = "Stripe event received: {Type} / {Id}")]
    public static partial void StripeEventReceived(this ILogger logger, string type, string id);

    [LoggerMessage(EventId = 408, Level = LogLevel.Information, Message = "Stripe event {Id} skipped: {Reason}")]
    public static partial void StripeEventSkipped(this ILogger logger, string id, string? reason);
}
