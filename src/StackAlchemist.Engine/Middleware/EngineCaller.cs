namespace StackAlchemist.Engine.Middleware;

/// <summary>
/// Who a request is on behalf of, for rate limiting. Every caller of <c>/api/*</c> is the web
/// app's server, so the client IP says nothing; the web sends the signed-in user's id in
/// <see cref="UserHeader"/> instead. The header is only as trustworthy as the service key, which
/// is why the rate limiter runs after the key check.
/// </summary>
public static class EngineCaller
{
    public const string UserHeader = "X-Engine-User";

    /// <summary>The bucket for requests that name no user: one window shared by all of them.</summary>
    public const string SharedPartition = "shared";

    /// <summary>
    /// The user's id in one canonical form, or <see cref="SharedPartition"/>. Anything that is not
    /// a GUID lands in the shared bucket, so a caller cannot mint a fresh window per request.
    /// </summary>
    public static string PartitionKey(HttpContext context) =>
        Guid.TryParse(context.Request.Headers[UserHeader].FirstOrDefault(), out var userId)
            ? userId.ToString("N")
            : SharedPartition;
}
