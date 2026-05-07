using System.IO.Compression;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Zips the generated project directory and uploads it to Cloudflare R2 (S3-compatible)
/// using the AWS SDK, then returns a presigned HTTPS download URL.
/// </summary>
public sealed partial class CloudflareR2UploadService(
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

        // ── 1. Verify bucket is reachable (fail-fast on misconfig) ───────────
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = BuildS3Config(accountId);
        using var s3 = new AmazonS3Client(credentials, s3Config);

        await EnsureBucketExistsAsync(s3, bucket, accountId, ct);

        // ── 2. Zip the directory into an in-memory stream ─────────────────────
        using var memStream = new MemoryStream();
        ZipFile.CreateFromDirectory(directoryPath, memStream);
        memStream.Position = 0;

        LogUploadingZip(logger, generationId, memStream.Length, bucket, key);

        // ── 3. Upload via AWS SDK (R2 is S3-compatible) ───────────────────────
        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = memStream,
            ContentType = "application/zip",
        };
        await s3.PutObjectAsync(putRequest, ct);

        // ── 4. Generate a presigned download URL ─────────────────────────────
        var presignRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Protocol = Protocol.HTTPS,
            Verb = HttpVerb.GET,
        };
        var url = s3.GetPreSignedURL(presignRequest);

        LogPresignedUrlCreated(logger, generationId, expiryHours);
        return url;
    }

    private async Task EnsureBucketExistsAsync(
        AmazonS3Client s3,
        string bucket,
        string accountId,
        CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucket }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            var translated = TranslateBucketProbeError(ex.StatusCode, ex.ErrorCode, bucket, accountId);
            if (translated is not null)
            {
                LogR2BucketProbeFailed(logger, bucket, accountId, ex.ErrorCode ?? "(none)", (int)ex.StatusCode);
                throw translated;
            }
            throw;
        }
    }

    internal static R2BucketException? TranslateBucketProbeErrorForTests(
        System.Net.HttpStatusCode statusCode,
        string? errorCode,
        string bucket,
        string accountId) =>
        TranslateBucketProbeError(statusCode, errorCode, bucket, accountId);

    private static R2BucketException? TranslateBucketProbeError(
        System.Net.HttpStatusCode statusCode,
        string? errorCode,
        string bucket,
        string accountId)
    {
        if (statusCode == System.Net.HttpStatusCode.NotFound ||
            string.Equals(errorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
        {
            return new R2BucketNotFoundException(bucket, accountId);
        }

        if (statusCode == System.Net.HttpStatusCode.Forbidden ||
            string.Equals(errorCode, "AccessDenied", StringComparison.OrdinalIgnoreCase))
        {
            return new R2BucketAccessDeniedException(bucket, accountId);
        }

        return null; // unknown — let the caller bubble the original exception
    }

    // ── S3 config builder (test-visible) ─────────────────────────────────────

    internal static AmazonS3Config BuildS3ConfigForTests(string accountId) =>
        BuildS3Config(accountId);

    private static AmazonS3Config BuildS3Config(string accountId) =>
        new()
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            // R2 does not use region-based routing; the dummy region satisfies the SDK.
            AuthenticationRegion = "auto",
            // R2 returns 501 for AWSSDK v4's default checksum trailers
            // (x-amz-checksum-crc32 + STREAMING-UNSIGNED-PAYLOAD-TRAILER).
            // WHEN_REQUIRED matches the v3 behavior that worked.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

    // ── LoggerMessage source-gen ──────────────────────────────────────────────

    [LoggerMessage(EventId = 900, Level = LogLevel.Information, Message = "Uploading zip for {Id} ({Bytes:N0} bytes) → R2 bucket {Bucket}/{Key}")]
    private static partial void LogUploadingZip(ILogger logger, string id, long bytes, string bucket, string key);

    [LoggerMessage(EventId = 901, Level = LogLevel.Information, Message = "Presigned URL created for {Id} (expires +{Hours}h)")]
    private static partial void LogPresignedUrlCreated(ILogger logger, string id, int hours);

    [LoggerMessage(EventId = 902, Level = LogLevel.Error, Message = "R2 bucket probe failed: bucket={Bucket}, account={Account}, errorCode={ErrorCode}, status={Status}")]
    private static partial void LogR2BucketProbeFailed(ILogger logger, string bucket, string account, string errorCode, int status);
}
