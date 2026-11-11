using System.Threading.RateLimiting;

namespace StackAlchemist.Engine.Middleware;

/// <summary>Names of the rate-limit policies, shared by their registration and the endpoints that use them.</summary>
public static class RateLimitPolicies
{
    public const string Generate = "generate";
    public const string Extract = "extract";
    public const string StripeSession = "stripe-session";
    public const string Download = "download";
}

public static class EngineRateLimiting
{
    /// <summary>
    /// Per-user fixed-window limits on expensive endpoints. A named AddFixedWindowLimiter is ONE
    /// window for every caller, so these used to be limits on the whole product: five generations a
    /// minute across all users, and one busy user locked everyone else out. Each policy is now
    /// partitioned by the user the web app names (EngineCaller); requests that name none share a window.
    /// </summary>
    public static IServiceCollection AddEngineRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(rl =>
        {
            void PerUser(string policy, int permitsPerMinute) =>
                rl.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                    EngineCaller.PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window      = TimeSpan.FromMinutes(1),
                        PermitLimit = permitsPerMinute,
                        QueueLimit  = 0,
                    }));

            PerUser(RateLimitPolicies.Generate, 5);        // generous for a real user, blocks spamming
            PerUser(RateLimitPolicies.Extract, 15);
            PerUser(RateLimitPolicies.StripeSession, 3);
            PerUser(RateLimitPolicies.Download, 30);       // signing a link is cheap; this only stops a loop

            rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rl.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Rate limit exceeded. Please wait before retrying." }, token);
            };
        });
}
