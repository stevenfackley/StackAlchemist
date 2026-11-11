namespace StackAlchemist.Engine.Services;

/// <summary>
/// Zips a generated project directory and uploads it to Cloudflare R2.
/// Returns a presigned HTTPS download URL.
/// </summary>
public interface IR2UploadService
{
    /// <summary>
    /// Creates a zip of <paramref name="directoryPath"/>, uploads it to R2 under
    /// <c>{generationId}/project.zip</c>, and returns a presigned download URL.
    /// </summary>
    Task<string> UploadZipAsync(string generationId, string directoryPath, CancellationToken ct = default);

    /// <summary>
    /// A new presigned download URL for the archive already uploaded for
    /// <paramref name="generationId"/>, good for <paramref name="lifetime"/>. The URL is signed
    /// locally: no request is made, so this does not check that the archive exists.
    /// </summary>
    string CreateDownloadUrl(string generationId, TimeSpan lifetime);
}
