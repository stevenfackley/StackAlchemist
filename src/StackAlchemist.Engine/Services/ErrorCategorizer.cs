using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Maps pipeline exceptions to the machine-readable <c>error_category</c> values the
/// frontend uses to render actionable failure guidance. The category vocabulary is
/// mirrored by the CHECK constraint in migration 20260611000001 and by
/// <c>GENERATION_ERROR_CATEGORIES</c> in the web app — keep all three in sync.
/// </summary>
public static class ErrorCategorizer
{
    public const string Quota = "quota";
    public const string Schema = "schema";
    public const string Build = "build";
    public const string RateLimit = "rate_limit";
    public const string Network = "network";
    public const string Internal = "internal";

    /// <summary>
    /// Whether a failure of this category is worth retrying, which is when the result page offers
    /// a Retry. Mirrors <c>RETRYABLE</c> in the web app's <c>lib/generation-outcome.ts</c>; a web
    /// test (<c>generation-outcome.test.ts</c>) fails when the two differ, because a paid failure
    /// with no Retry is refunded (<see cref="FailedBuildRefunder.ShouldRefund"/>).
    /// </summary>
    public static bool OffersRetry(string category) => category is not (Internal or Quota);

    public static string Categorize(Exception ex) => ex switch
    {
        SchemaExtractionException or SchemaValidationException
            or MalformedLlmOutputException => Schema, // includes TruncatedLlmResponseException
        LlmRateLimitException => RateLimit,
        InjectionFailedException { InnerException: { } inner } => Categorize(inner),
        HttpRequestException or TimeoutException or TaskCanceledException
            or System.Net.Sockets.SocketException or IOException => Network,
        _ => Internal,
    };
}
