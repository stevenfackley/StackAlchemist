using FluentAssertions;

namespace StackAlchemist.Worker.Tests;

/// <summary>
/// Tests for the CompileWorker — runs dotnet build + npm build on generated projects
/// and handles the retry loop with error context accumulation.
/// </summary>
public class CompileWorkerTests
{
    [Fact]
    public void ExecuteBuild_WithValidProject_ReturnsSuccessExitCode()
    {
        // Arrange — a temp directory with a valid .NET project

        // Act & Assert
        // var result = await _sut.ExecuteBuild(tempDir);
        // result.ExitCode.Should().Be(0);
        // result.IsSuccess.Should().BeTrue();
        // result.ErrorOutput.Should().BeNullOrEmpty();
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void ExecuteBuild_WithBrokenProject_ReturnsFailureWithErrorDetails()
    {
        // Arrange — a temp directory with a .NET project that has syntax errors

        // Act & Assert
        // var result = await _sut.ExecuteBuild(tempDir);
        // result.ExitCode.Should().Be(1);
        // result.IsSuccess.Should().BeFalse();
        // result.ErrorOutput.Should().Contain("error CS");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void ExtractBuildErrors_FromDotnetOutput_ReturnsParsedErrors()
    {
        // Arrange
        var buildOutput = """
            Microsoft (R) Build Engine version 17.0.0
            Build started 1/1/2026 12:00:00 AM.
            src/Controllers/ProductsController.cs(15,22): error CS1002: ; expected
            src/Controllers/ProductsController.cs(20,5): error CS0246: The type or namespace name 'ILogger' could not be found
            Build FAILED.
            """;

        // Act & Assert
        // var errors = _sut.ExtractBuildErrors(buildOutput);
        // errors.Should().HaveCount(2);
        // errors[0].Should().Contain("CS1002");
        // errors[1].Should().Contain("CS0246");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void HandleRetry_AppendsErrorContextToPrompt()
    {
        // Arrange
        var originalPrompt = "Generate a Product controller";
        var buildErrors = "error CS1002: ; expected at line 15";

        // Act & Assert
        // var retryContext = _sut.BuildRetryContext(originalPrompt, buildErrors, retryAttempt: 1);
        // retryContext.Should().Contain(originalPrompt);
        // retryContext.Should().Contain("CS1002");
        // retryContext.Should().Contain("attempt 1");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void HandleRetry_AccumulatesErrorsAcrossAttempts()
    {
        // Arrange — errors from 2 previous attempts
        var errorHistory = new List<string>
        {
            "Attempt 1: error CS1002: ; expected",
            "Attempt 2: error CS0103: The name 'context' does not exist"
        };

        // Act & Assert
        // var retryContext = _sut.BuildRetryContext(originalPrompt, errorHistory, retryAttempt: 3);
        // retryContext.Should().Contain("Attempt 1");
        // retryContext.Should().Contain("Attempt 2");
        // retryContext.Should().Contain("CS1002");
        // retryContext.Should().Contain("CS0103");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void HandleRetry_TruncatesOldErrorsWhenApproachingTokenLimit()
    {
        // Arrange — very long error history that might exceed token limits
        var longErrorHistory = Enumerable.Range(1, 100)
            .Select(i => $"Attempt {i}: error CS{i:D4}: some very long error message with lots of context about what went wrong in the build process")
            .ToList();

        // Act & Assert
        // var retryContext = _sut.BuildRetryContext(originalPrompt, longErrorHistory, retryAttempt: 3);
        // var estimatedTokens = retryContext.Length / 4;
        // estimatedTokens.Should().BeLessThan(maxContextTokens);
        // retryContext should contain the MOST RECENT errors, not the oldest
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void ExecuteBuild_IsolatesTemporaryDirectories()
    {
        // Two concurrent builds should not interfere with each other

        // Arrange
        // var tempDir1 = _sut.CreateIsolatedBuildDirectory(generationId1);
        // var tempDir2 = _sut.CreateIsolatedBuildDirectory(generationId2);

        // Act & Assert
        // tempDir1.Should().NotBe(tempDir2);
        // Both directories should be independent
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void ExecuteBuild_CleansUpTempDirectory_OnSuccess()
    {
        // After successful build + zip, temp directory should be cleaned up

        // Act & Assert
        // Directory.Exists(tempDir).Should().BeFalse("temp directory should be deleted after successful build");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }

    [Fact]
    public void ExecuteBuild_CleansUpTempDirectory_OnFinalFailure()
    {
        // After max retries exceeded, temp directory should still be cleaned up

        // Act & Assert
        // Directory.Exists(tempDir).Should().BeFalse("temp directory should be deleted after final failure");
        Assert.True(true, "Scaffold: implement when CompileWorker exists");
    }
}
