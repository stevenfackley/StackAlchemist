using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Real-build coverage for <see cref="DotNetBuildStrategy"/>: exercises the actual
/// `dotnet restore` + `dotnet build --no-restore` process pipeline against temp
/// projects on disk, rather than mocking <c>RunProcessAsync</c>.
///
/// This is the regression test for the prod bug where `dotnet build --no-restore`
/// ran against a freshly-written temp dir that was never restored, failing every
/// generation with NETSDK1004 regardless of whether the generated code was valid.
///
/// Guarded (not [Fact(Skip=...)]'d) on the real .NET SDK being reachable on PATH so
/// these run for real wherever the SDK is present (including the backend CI job,
/// which installs it via actions/setup-dotnet) instead of being permanently skipped.
/// </summary>
public sealed class DotNetBuildStrategyIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "sa-dotnet-build-test-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DotNetBuildStrategy _sut = new(NullLogger<DotNetBuildStrategy>.Instance);

    public DotNetBuildStrategyIntegrationTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task ExecuteBuildAsync_MinimalValidConsoleProject_RestoresAndBuildsSuccessfully()
    {
        if (!IsDotNetSdkAvailable())
        {
            Console.WriteLine("Skipping — `dotnet` not found on PATH.");
            return;
        }

        const string csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;
        const string programCs = """
            Console.WriteLine("Hello from a Compile Guarantee test project.");
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Test.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"), programCs);

        var result = await _sut.ExecuteBuildAsync(_tempDir, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            because: $"a minimal valid project with restore-then-build should compile cleanly.\nOutput:\n{result.StandardOutput}\n{result.ErrorOutput}");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteBuildAsync_ProjectWithSyntaxError_FailsAndErrorContextIsExtractable()
    {
        if (!IsDotNetSdkAvailable())
        {
            Console.WriteLine("Skipping — `dotnet` not found on PATH.");
            return;
        }

        const string csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;
        // Missing semicolon — guaranteed CS1002.
        const string brokenCs = """
            Console.WriteLine("missing semicolon")
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Test.csproj"), csproj);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"), brokenCs);

        var result = await _sut.ExecuteBuildAsync(_tempDir, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);

        var errors = _sut.ExtractBuildErrors(result.ErrorOutput);
        errors.Should().NotBeEmpty("the repair loop needs real error context, not an empty list");
        errors.Should().Contain(e => e.Contains("error CS", StringComparison.Ordinal));
    }

    private static bool IsDotNetSdkAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return false;

            process.WaitForExit(5_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
