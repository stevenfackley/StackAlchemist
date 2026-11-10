namespace StackAlchemist.Engine.Middleware;

public static class ServiceKeyAuth
{
    /// <summary>
    /// Service key auth. Protects /api/* (except /api/webhooks/stripe which uses Stripe signature
    /// auth) against unauthenticated callers. When ENGINE_SERVICE_KEY is unset the check is skipped
    /// so local dev / CI works without extra config. It is middleware, not an endpoint filter,
    /// because it has to run before the rate limiter.
    /// </summary>
    public static IApplicationBuilder UseEngineServiceKey(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/api/webhooks"))
            {
                var configuredKey = context.RequestServices
                    .GetRequiredService<IConfiguration>()["Engine:ServiceKey"];

                if (!string.IsNullOrWhiteSpace(configuredKey))
                {
                    var providedKey = context.Request.Headers[EngineCaller.KeyHeader].FirstOrDefault();
                    if (!EngineCaller.HasServiceKey(providedKey, configuredKey))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                        return;
                    }
                }
            }

            await next(context);
        });
}
