using StackAlchemist.Engine.Middleware;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Endpoints;

/// <summary>The body of <c>POST /api/download-url</c>.</summary>
public sealed record DownloadUrlRequest(string? GenerationId);

/// <summary>
/// <c>/api/download-url</c>: a new download link for a delivered paid generation.
///
/// The link stored on the row at delivery is presigned once and dies after seven days, the longest
/// a presigned URL can live. A customer who came back later found a page that said "Ready" over a
/// dead link. The archive itself stays in the bucket, so the web app asks for a new, short-lived
/// link each time the customer clicks Download.
/// </summary>
public static class DownloadEndpoints
{
    /// <summary>Long enough to start the download; short enough that a copied link is soon useless.</summary>
    internal static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/download-url", CreateDownloadUrlAsync)
            .WithName("CreateDownloadUrl")
            .WithSummary("Create a short-lived download link for a delivered paid generation.")
            .RequireRateLimiting(RateLimitPolicies.Download);

        return app;
    }

    private static async Task<IResult> CreateDownloadUrlAsync(
        DownloadUrlRequest request,
        IDeliveryService delivery,
        IR2UploadService r2,
        CancellationToken ct)
    {
        // The id becomes part of the object key, so it must be a uuid and nothing else. Whether the
        // caller owns the generation is the web app's check: it holds the session, the Engine does not.
        if (!Guid.TryParse(request.GenerationId, out var id))
            return NoDownload();

        var generationId = id.ToString();
        var snapshot = await delivery.GetGenerationSnapshotAsync(generationId, ct);

        // Only a delivered paid build has an archive. A free preview never uploads one, and a failed
        // or refunded build must not be handed a link to whatever an earlier attempt left behind.
        if (snapshot is not { Status: "success", Tier: >= 1 })
            return NoDownload();

        return Results.Ok(new { url = r2.CreateDownloadUrl(generationId, LinkLifetime) });
    }

    private static IResult NoDownload() =>
        Results.NotFound(new { error = "No download is available for this generation." });
}
