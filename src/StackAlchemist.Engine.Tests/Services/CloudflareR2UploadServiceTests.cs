using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CloudflareR2UploadService"/>.
/// Configuration-validation tests run locally.
/// Actual S3/R2 upload tests are skipped — they require valid Cloudflare R2 credentials.
/// </summary>
public class CloudflareR2UploadServiceTests
{
    private static IConfiguration BuildConfig(
        string? accountId = "test-account",
        string? accessKey = "test-access",
        string? secretKey = "test-secret",
        string? bucket    = "test-bucket") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudflareR2:AccountId"]    = accountId,
                ["CloudflareR2:AccessKeyId"]  = accessKey,
                ["CloudflareR2:SecretAccessKey"] = secretKey,
                ["CloudflareR2:BucketName"]   = bucket,
                ["CloudflareR2:PresignedUrlExpiryHours"] = "24",
            })
            .Build();

    private static CloudflareR2UploadService BuildSut(IConfiguration config) =>
        new(config, NullLogger<CloudflareR2UploadService>.Instance);

    [Fact]
    public async Task UploadZipAsync_MissingAccountId_ThrowsInvalidOperationException()
    {
        var sut = BuildSut(BuildConfig(accountId: null));
        var dir = Directory.CreateTempSubdirectory("r2test").FullName;

        try
        {
            var act = () => sut.UploadZipAsync("gen-id", dir);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*AccountId*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task UploadZipAsync_MissingAccessKeyId_ThrowsInvalidOperationException()
    {
        var sut = BuildSut(BuildConfig(accessKey: null));
        var dir = Directory.CreateTempSubdirectory("r2test").FullName;

        try
        {
            var act = () => sut.UploadZipAsync("gen-id", dir);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*AccessKeyId*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task UploadZipAsync_MissingSecretAccessKey_ThrowsInvalidOperationException()
    {
        var sut = BuildSut(BuildConfig(secretKey: null));
        var dir = Directory.CreateTempSubdirectory("r2test").FullName;

        try
        {
            var act = () => sut.UploadZipAsync("gen-id", dir);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*SecretAccessKey*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(Skip = "Integration test — requires valid Cloudflare R2 credentials in user-secrets")]
    public async Task UploadZipAsync_WithRealCredentials_ReturnsPresignedUrl()
    {
        // Populate via: dotnet user-secrets set CloudflareR2:AccountId <value> etc.
        var config = new ConfigurationBuilder()
            .AddUserSecrets<CloudflareR2UploadServiceTests>()
            .Build();

        var sut = BuildSut(config);
        var dir = Directory.CreateTempSubdirectory("r2-integration").FullName;
        File.WriteAllText(Path.Combine(dir, "test.txt"), "Hello R2");

        try
        {
            var url = await sut.UploadZipAsync($"integration-test-{Guid.NewGuid()}", dir);
            url.Should().StartWith("https://");
            url.Should().Contain("r2.cloudflarestorage.com");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
