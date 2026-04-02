using System.IO.Compression;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Zips the generated project directory and uploads it to Cloudflare R2 (S3-compatible)
/// using the AWS SDK, then returns a presigned HTTPS download URL.
/// </summary>
public sealed class CloudflareR2UploadService(
    IConfiguration config,
    ILogger<CloudflareR2UploadService> logger) : IR2UploadService
{
    public async Task<string> UploadZipAsync(
        string generationId,
        string directoryPath,
        CancellationToken ct = default)
    {
        var accountId = config["CloudflareR2:AccountId"]
            ?? throw new InvalidOperationException("CloudflareR2:AccountId is not configured.");
        var accessKey = config["CloudflareR2:AccessKeyId"]
            ?? throw new InvalidOperationException("CloudflareR2:AccessKeyId is not configured.");
        var secretKey = config["CloudflareR2:SecretAccessKey"]
            ?? throw new InvalidOperationException("CloudflareR2:SecretAccessKey is not configured.");
        var bucket = config["CloudflareR2:BucketName"] ?? "stackalchemist-builds";
        var expiryHours = int.TryParse(
            config["CloudflareR2:PresignedUrlExpiryHours"], out var h) ? h : 168;

        var key = $"{generationId}/project.zip";

        // ── 1. Zip the directory into an in-memory stream ─────────────────────
        using var memStream = new MemoryStream();
        ZipFile.CreateFromDirectory(directoryPath, memStream);
        memStream.Position = 0;

        logger.LogInformation(
            "Uploading zip for {Id} ({Bytes:N0} bytes) → R2 bucket {Bucket}/{Key}",
            generationId, memStream.Length, bucket, key);

        // ── 2. Upload via AWS SDK (R2 is S3-compatible) ───────────────────────
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            // R2 does not use region-based routing; the dummy region satisfies the SDK.
            AuthenticationRegion = "auto",
        };

        using var s3 = new AmazonS3Client(credentials, s3Config);

        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = memStream,
            ContentType = "application/zip",
        };
        await s3.PutObjectAsync(putRequest, ct);

        // ── 3. Generate a presigned download URL ─────────────────────────────
        var presignRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Protocol = Protocol.HTTPS,
            Verb = HttpVerb.GET,
        };
        var url = s3.GetPreSignedURL(presignRequest);

        logger.LogInformation("Presigned URL created for {Id} (expires +{Hours}h)", generationId, expiryHours);
        return url;
    }
}
