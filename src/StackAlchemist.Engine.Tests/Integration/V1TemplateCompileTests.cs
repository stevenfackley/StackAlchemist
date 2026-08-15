using System.Diagnostics;
using System.Text;
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
/// `npm run build`, exactly what compile-guarantee.md promises the customer — and then
/// builds the shipped Dockerfile's `web` and `engine` targets over the same tree, which is
/// what the customer's own first command does.
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
        if (!ToolchainAvailable("dotnet", "--version", "the .NET SDK"))
            return;

        if (!ToolchainAvailable(ProcessCommandResolver.Npm, "--version", "npm"))
            return;

        V1TemplateHarness.RenderTo(_outputDir);

        var strategy = new DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        var result = await strategy.ExecuteBuildAsync(_outputDir, cts.Token);

        result.IsSuccess.Should().BeTrue(
            because: "the V1 template is the baseline of every Tier-2 'Compile Guarantee' " +
                     $"archive and must compile before the LLM adds anything.\n\n{result.StandardOutput}\n{result.ErrorOutput}");

        // The frontend leg genuinely ran. BuildNextJsAsync returns success when
        // nextjs/package.json is absent, so a green result alone does not prove `next build`
        // happened — the standalone server entrypoint does.
        File.Exists(Path.Combine(_outputDir, "nextjs", ".next", "standalone", "server.js"))
            .Should().BeTrue("`next build` must have produced the standalone output, not been skipped");

        AssertArchiveIsCustomerReady();
    }

    /// <summary>
    /// Builds the shipped Dockerfile, which is the customer's very first command
    /// (<c>docker compose up --build</c>, per the template's own header comment).
    ///
    /// The host-side gate above proves <c>dotnet build</c> and <c>npm run build</c> succeed;
    /// that is a different thing from the multi-stage image build actually working, and the
    /// difference was not academic. The <c>engine</c> target ran
    /// <c>addgroup --system … &amp;&amp; adduser --system …</c> — BusyBox syntax that exists on the
    /// Alpine base used by the <c>web</c> target but not on the Debian/Ubuntu
    /// <c>dotnet/aspnet</c> base — so <c>docker build --target engine</c> died at exit 127 on
    /// every archive we sold, and nothing in the suite noticed because nothing built it.
    ///
    /// Both targets, because the failure was in exactly the one that was not exercised by
    /// anything else.
    /// </summary>
    [Theory]
    [InlineData("web")]
    [InlineData("engine")]
    public async Task RenderedTemplate_DockerTargetBuilds(string target)
    {
        if (!ToolchainAvailable("docker", "version", "Docker"))
            return;

        V1TemplateHarness.RenderTo(_outputDir);

        var (exitCode, transcript) = await RunAsync(
            "docker",
            $"build --target {target} --tag {DockerImageTagPrefix}:{target} .",
            _outputDir);

        exitCode.Should().Be(0,
            $"`docker build --target {target}` is the customer's first command against the " +
            $"archive they paid for.\n\n{Tail(transcript)}");
    }

    private const string DockerImageTagPrefix = "sa-v1-template-gate";

    /// <summary>
    /// Cold image pulls plus <c>npm ci</c> plus <c>next build</c> plus <c>dotnet publish</c>.
    /// Generous, because a timeout here should mean "hung", not "the runner was slow".
    /// </summary>
    private static readonly TimeSpan DockerBuildTimeout = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Runs a process to completion, interleaving stdout and stderr into one transcript.
    /// Docker writes its build log to stderr, so capturing only stdout would leave a failing
    /// assertion with nothing useful in it.
    /// </summary>
    private static async Task<(int ExitCode, string Transcript)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        var transcript = new StringBuilder();
        void Append(object _, DataReceivedEventArgs e)
        {
            if (e.Data is null)
                return;
            lock (transcript)
                transcript.AppendLine(e.Data);
        }

        process.OutputDataReceived += Append;
        process.ErrorDataReceived += Append;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(DockerBuildTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already gone */ }

            lock (transcript)
                return (-1, $"[killed after {DockerBuildTimeout}]\n{transcript}");
        }

        lock (transcript)
            return (process.ExitCode, transcript.ToString());
    }

    /// <summary>Last <see cref="TranscriptTailLines"/> lines — a docker build log is megabytes.</summary>
    private static string Tail(string transcript)
    {
        var lines = transcript.Split('\n');
        return lines.Length <= TranscriptTailLines
            ? transcript
            : string.Join('\n', lines[^TranscriptTailLines..]);
    }

    private const int TranscriptTailLines = 120;

    /// <summary>
    /// The build above filled this directory with a Linux/Windows-specific
    /// <c>node_modules/</c> and a <c>.next/</c> cache — 525 MB / 25,488 files, measured. This
    /// is the same directory <c>CompileWorkerService.PackUploadAndNotifyAsync</c> hands to the
    /// R2 uploader, so without exclusions every paid generation would have emailed the buyer a
    /// ~141 MB zip of another machine's build output. Asserted here, on the real post-build
    /// tree, because that is the only place the regression is actually observable.
    /// </summary>
    private void AssertArchiveIsCustomerReady()
    {
        var entries = ProjectArchiver.EnumerateArchiveEntries(_outputDir);

        entries.Should().NotContain(
            e => e.Contains("node_modules/", StringComparison.Ordinal)
              || e.Contains(".next/", StringComparison.Ordinal)
              || e.Contains("/obj/", StringComparison.Ordinal)
              || e.Contains("/bin/Debug/", StringComparison.Ordinal)
              || e.Contains("/bin/Release/", StringComparison.Ordinal),
            "build output belongs to the build machine, not to the customer");

        entries.Should().Contain("dotnet/Program.cs");
        entries.Should().Contain("nextjs/package.json");
        entries.Should().Contain("nextjs/package-lock.json");
        entries.Should().Contain("docker-compose.yml");

        var zipPath = ProjectArchiver.CreateArchiveFile(_outputDir);
        try
        {
            var bytes = new FileInfo(zipPath).Length;
            Console.WriteLine($"Customer archive: {entries.Count} entries, {bytes:N0} bytes.");

            bytes.Should().BeLessThan(MaxCustomerArchiveBytes,
                $"the archive is what the buyer downloads; it was {bytes:N0} bytes with " +
                $"{entries.Count} entries");

            KeepArchiveIfRequested(zipPath);
        }
        finally
        {
            ProjectArchiver.TryDelete(zipPath);
        }
    }

    /// <summary>
    /// Copies the packed archive somewhere durable when <c>SA_TEMPLATE_ARCHIVE_OUTPUT</c>
    /// names a path. This is how you get the exact bytes a Tier-2 buyer downloads — built
    /// by the real strategy, packed by the real <see cref="ProjectArchiver"/> — without
    /// spending money on a live generation.
    /// </summary>
    private static void KeepArchiveIfRequested(string zipPath)
    {
        var destination = Environment.GetEnvironmentVariable(ArchiveOutputEnvVar);
        if (string.IsNullOrWhiteSpace(destination))
            return;

        var fullPath = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.Copy(zipPath, fullPath, overwrite: true);
        Console.WriteLine($"Customer archive written to {fullPath}");
    }

    /// <summary>Env var naming a file to copy the packed customer archive to.</summary>
    private const string ArchiveOutputEnvVar = "SA_TEMPLATE_ARCHIVE_OUTPUT";

    /// <summary>
    /// Ceiling for the packed Tier-2 deliverable. Generous against the real figure (the
    /// rendered tree is a few hundred KB zipped) but two orders of magnitude below the
    /// 147,720,784-byte archive the unfiltered writer produced after the build ran.
    /// </summary>
    private const long MaxCustomerArchiveBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Skips locally when a toolchain is missing, but FAILS on CI. A gate that quietly
    /// passes in 0.4s because npm moved is not a gate — and this one exists precisely
    /// because a whole class of breakage went unnoticed for want of an assertion.
    /// </summary>
    private static bool ToolchainAvailable(string fileName, string arguments, string label)
    {
        if (IsOnPath(fileName, arguments))
            return true;

        Assert.False(
            IsContinuousIntegration,
            $"{label} is required to verify the Compile Guarantee and was not found on PATH " +
            $"(tried '{fileName}'). The backend CI job runs on ubuntu-latest, which provides " +
            "the .NET SDK, Node and Docker — if this fires, the gate is no longer actually " +
            "building the template.");

        Console.WriteLine($"Skipping — {label} not found on PATH.");
        return false;
    }

    private static bool IsContinuousIntegration =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 } ci
        && !ci.Equals("false", StringComparison.OrdinalIgnoreCase)
        && !ci.Equals("0", StringComparison.Ordinal);

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
