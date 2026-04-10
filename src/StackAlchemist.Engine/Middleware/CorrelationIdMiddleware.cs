using Serilog.Context;

namespace StackAlchemist.Engine.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the request (or mints a new GUID),
/// pushes it into Serilog's LogContext, and echoes it in the response.
/// Must be registered early — before exception handler and auth.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("D");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
