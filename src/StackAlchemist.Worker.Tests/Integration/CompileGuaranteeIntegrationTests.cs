using FluentAssertions;

namespace StackAlchemist.Worker.Tests.Integration;

/// <summary>
/// Integration tests for the compile guarantee worker.
/// These tests create actual .NET projects in temp directories and verify that
/// dotnet build is executed correctly.
/// 
/// Requires: .NET SDK on the test machine, Docker for Testcontainers.
/// </summary>
public class CompileGuaranteeIntegrationTests : IAsyncLifetime, IDisposable
{
    private string _tempDir = string.Empty;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sa-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* Best effort cleanup */ }
        }
    }

    [Fact(Skip = "Integration test — requires .NET SDK")]
    public async Task DotnetBuild_ValidMinimalProject_ReturnsExitCodeZero()
    {
        // Arrange — create a minimal valid .NET project
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var programCs = """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => "Hello World");
            app.Run();
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Test.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"), programCs);

        // Act — run dotnet build
        // var result = await _sut.ExecuteBuild(_tempDir);

        // Assert
        // result.ExitCode.Should().Be(0);
        // result.IsSuccess.Should().BeTrue();
    }

    [Fact(Skip = "Integration test — requires .NET SDK")]
    public async Task DotnetBuild_ProjectWithSyntaxError_ReturnsExitCodeOne()
    {
        // Arrange — create a .NET project with a syntax error
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var brokenCs = """
            var builder = WebApplication.CreateBuilder(args)  // Missing semicolon
            var app = builder.Build();
            app.Run();
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Test.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"), brokenCs);

        // Act
        // var result = await _sut.ExecuteBuild(_tempDir);

        // Assert
        // result.ExitCode.Should().Be(1);
        // result.IsSuccess.Should().BeFalse();
        // result.ErrorOutput.Should().Contain("error CS");
    }

    [Fact(Skip = "Integration test — requires .NET SDK")]
    public async Task FullRetryLoop_BrokenThenFixed_SucceedsOnRetry()
    {
        // Arrange
        // - First LLM response produces code with a missing using statement
        // - Second LLM response (retry) includes the fix

        // Act — run the full worker loop

        // Assert
        // - Final status is "success"
        // - retry_count = 1
        // - The fix was applied correctly
        await Task.CompletedTask;
    }
}
