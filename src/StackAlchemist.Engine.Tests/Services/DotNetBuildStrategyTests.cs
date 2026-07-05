using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit coverage for <see cref="DotNetBuildStrategy.ExtractBuildErrors"/> — the repair
/// loop only gets useful retry context if this regex actually captures the error
/// families a failed `dotnet restore`/`dotnet build` emits.
/// </summary>
public sealed class DotNetBuildStrategyTests
{
    private readonly DotNetBuildStrategy _sut = new(NullLogger<DotNetBuildStrategy>.Instance);

    [Fact]
    public void ExtractBuildErrors_NetSdk1004RestoreFailure_IsCaptured()
    {
        // Exact shape `dotnet build --no-restore` emits when a project was never
        // restored — this was silently swallowed before (regex only matched `CS\d+`),
        // so the LLM repair loop burned all 3 retries with empty error context.
        var output = """
            Build started 1/1/2026 12:00:00 AM.
            /tmp/build/Test.csproj : error NETSDK1004: Assets file '/tmp/build/obj/project.assets.json' not found. Run a NuGet package restore to generate this file.
            Build FAILED.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("NETSDK1004");
    }

    [Fact]
    public void ExtractBuildErrors_NuGetAndMsBuildErrors_AreCaptured()
    {
        var output = """
            Determining projects to restore...
            /tmp/build/Test.csproj : error NU1101: Unable to find package Foo. No packages exist with this id in source(s): nuget.org
            MSBUILD : error MSB1009: Project file does not exist.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Contains("NU1101"));
        errors.Should().Contain(e => e.Contains("MSB1009"));
    }

    [Fact]
    public void ExtractBuildErrors_StillCapturesCSharpCompilerErrors()
    {
        var output = """
            Program.cs(10,5): error CS1002: ; expected
            Program.cs(11,5): warning CS0168: variable is declared but never used
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("CS1002");
    }

    [Fact]
    public void ExtractBuildErrors_NoErrors_ReturnsEmpty()
    {
        var output = """
            Build succeeded.
                0 Warning(s)
                0 Error(s)
            """;

        _sut.ExtractBuildErrors(output).Should().BeEmpty();
    }
}
