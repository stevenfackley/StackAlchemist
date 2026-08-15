using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// THE Compile Guarantee gate for the Tier-2/3 deliverable.
///
/// Before this existed, no test anywhere rendered the real on-disk V1-DotNet-NextJs tree
/// and compiled it: <see cref="DotNetBuildStrategyIntegrationTests"/> builds a hand-written
/// two-file console project, and the TemplateProvider tests use MockFileSystem with
/// synthetic inline templates. The first real build of real template output therefore
/// happened in production, on a paying customer's generation.
///
/// This test renders the real template set through the real render path and runs the real
/// <see cref="DotNetBuildStrategy"/> over it — both halves, `dotnet build` and
/// `npm run build`, exactly what compile-guarantee.md promises the customer.
///
/// Guarded (not [Fact(Skip=…)]'d) on the toolchains being reachable on PATH, the same way
/// <see cref="DotNetBuildStrategyIntegrationTests"/> is, so it runs for real in the backend
/// CI job (which has both the .NET SDK and Node) instead of being permanently skipped.
/// </summary>
public sealed class V1TemplateCompileTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "sa-v1-template-build-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _outputDir;
    private readonly bool _keepOutput;

    public V1TemplateCompileTests()
    {
        var configured = Environment.GetEnvironmentVariable(V1TemplateHarness.OutputDirEnvVar);
        _keepOutput = !string.IsNullOrWhiteSpace(configured);
        _outputDir = _keepOutput ? Path.GetFullPath(configured!) : _tempDir;
    }

    public void Dispose()
    {
        if (_keepOutput)
            return;

        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void Render_ProducesTheExpectedTreeWithNoUnresolvedTemplateScaffolding()
    {
        var files = V1TemplateHarness.RenderTo(_outputDir);

        files.Should().Contain("dotnet/InvoiceHub.csproj",
            "the .csproj filename token must be substituted, not left as {{ProjectName}}");
        files.Should().Contain("dotnet/Program.cs");
        files.Should().Contain("nextjs/package.json");

        files.Should().NotContain(p => p.Contains("/obj/", StringComparison.Ordinal)
                                    || p.Contains("/bin/", StringComparison.Ordinal)
                                    || p.Contains("node_modules", StringComparison.Ordinal),
            "build residue in the template tree must never reach a customer archive");

        foreach (var relativePath in files)
        {
            var content = File.ReadAllText(Path.Combine(_outputDir, relativePath));

            content.Should().NotContain("[[LLM_INJECTION_",
                $"{relativePath} would ship pipeline scaffolding to the customer (and is a syntax error in .cs/.tsx)");
            content.Should().NotContain("{{",
                $"{relativePath} has an unsubstituted Handlebars token");
        }
    }

    [Fact]
    public async Task RenderedTemplate_BuildsBothHalves()
    {
        if (!IsOnPath("dotnet", "--version"))
        {
            Console.WriteLine("Skipping — `dotnet` not found on PATH.");
            return;
        }

        if (!IsOnPath(ProcessCommandResolver.Npm, "--version"))
        {
            Console.WriteLine("Skipping — `npm` not found on PATH.");
            return;
        }

        V1TemplateHarness.RenderTo(_outputDir);

        var strategy = new DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        var result = await strategy.ExecuteBuildAsync(_outputDir, cts.Token);

        result.IsSuccess.Should().BeTrue(
            because: "the V1 template is the baseline of every Tier-2 'Compile Guarantee' " +
                     $"archive and must compile before the LLM adds anything.\n\n{result.StandardOutput}\n{result.ErrorOutput}");
    }

    private static bool IsOnPath(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return false;

            process.WaitForExit(15_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
