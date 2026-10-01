using System.Collections;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Compile Guarantee children (dotnet, npm, python, …) run LLM-generated code, so they get a
/// toolchain allowlist, never the Engine's environment (#451). The Engine holds DATABASE_URL
/// (the platform database), the Stripe/Anthropic/R2 credentials, ENGINE_SERVICE_KEY and
/// BYOK_ENCRYPTION_KEY; a generated FastAPI app reads DATABASE_URL through pydantic-settings.
/// </summary>
public sealed class BuildProcessEnvironmentTests
{
    [Theory]
    [InlineData("DATABASE_URL")]
    [InlineData("DATABASE_URL_MIGRATE")]
    [InlineData("ConnectionStrings__Db")]
    [InlineData("STRIPE_SECRET_KEY")]
    [InlineData("STRIPE_WEBHOOK_SECRET")]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("ENGINE_SERVICE_KEY")]
    [InlineData("BYOK_ENCRYPTION_KEY")]
    [InlineData("SUPABASE_SERVICE_ROLE_KEY")]
    [InlineData("NEXT_PUBLIC_SUPABASE_ANON_KEY")]
    [InlineData("R2_SECRET_ACCESS_KEY")]
    [InlineData("R2_ACCESS_KEY_ID")]
    [InlineData("AWS_SECRET_ACCESS_KEY")]
    [InlineData("AWS_SESSION_TOKEN")]
    [InlineData("AUTH_SECRET")]
    [InlineData("QAVREN_AUTH_URL")]
    [InlineData("ASPNETCORE_URLS")]
    [InlineData("SOME_FUTURE_SECRET")]
    public void Secrets_AndAnythingNotAllowlisted_AreWithheld(string name) =>
        BuildProcessEnvironment.IsAllowed(name).Should().BeFalse();

    [Theory]
    [InlineData("PATH")]
    [InlineData("Path")]
    [InlineData("HOME")]
    [InlineData("TMPDIR")]
    [InlineData("LANG")]
    [InlineData("LC_ALL")]
    [InlineData("DOTNET_ROOT")]
    [InlineData("DOTNET_CLI_TELEMETRY_OPTOUT")]
    [InlineData("NUGET_PACKAGES")]
    [InlineData("npm_config_cache")]
    [InlineData("NPM_CONFIG_REGISTRY")]
    [InlineData("PIP_INDEX_URL")]
    [InlineData("LD_LIBRARY_PATH")]
    [InlineData("HTTPS_PROXY")]
    [InlineData("SSL_CERT_FILE")]
    [InlineData("NEXT_TELEMETRY_DISABLED")]
    [InlineData("SystemRoot")]
    [InlineData("APPDATA")]
    [InlineData("ProgramFiles(x86)")]
    public void ToolchainVariables_ArePassedThrough(string name) =>
        BuildProcessEnvironment.IsAllowed(name).Should().BeTrue();

    [Theory]
    [InlineData("NUGET_AUTH_TOKEN")]
    [InlineData("NPM_CONFIG__AUTH")]
    [InlineData("NPM_CONFIG_//registry.example/:_authToken")]
    [InlineData("PIP_PASSWORD")]
    [InlineData("DOTNET_ConnectionStrings__SecretDb")]
    [InlineData("NUGET_API_KEY")]
    public void CredentialShapedNames_AreWithheld_EvenInsideAnAllowedFamily(string name) =>
        BuildProcessEnvironment.IsAllowed(name).Should().BeFalse();

    [Fact]
    public void Filter_KeepsCaseDistinctNamesApart()
    {
        // On Linux `http_proxy` and `HTTP_PROXY` are two variables; curl reads only the former.
        IDictionary source = new Hashtable
        {
            ["http_proxy"] = "http://lower:3128",
            ["HTTP_PROXY"] = "http://upper:3128",
        };

        var filtered = BuildProcessEnvironment.Filter(source);

        filtered.Should().HaveCount(2);
        filtered["http_proxy"].Should().Be("http://lower:3128");
        filtered["HTTP_PROXY"].Should().Be("http://upper:3128");
    }

    [Fact]
    public void Apply_PrunesInPlace_KeepingTheTargetsComparerAndEntries()
    {
        var target = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["http_proxy"] = "http://lower:3128",
            ["HTTP_PROXY"] = "http://upper:3128",
            ["DATABASE_URL"] = "postgres://secret",
            ["NUGET_AUTH_TOKEN"] = "tok",
        };

        BuildProcessEnvironment.Apply(target);

        target.Keys.Should().BeEquivalentTo("PATH", "http_proxy", "HTTP_PROXY");
        target.Comparer.Should().BeSameAs(StringComparer.Ordinal);
    }

    [Fact]
    public void Filter_KeepsOnlyTheAllowlistedEntries()
    {
        IDictionary source = new Hashtable
        {
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/root",
            ["DATABASE_URL"] = "postgres://stackalchemist:secret@pooler:6543/postgres",
            ["STRIPE_SECRET_KEY"] = "sk_live_x",
            ["DOTNET_ROOT"] = "/usr/share/dotnet",
        };

        BuildProcessEnvironment.Filter(source).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/root",
            ["DOTNET_ROOT"] = "/usr/share/dotnet",
        });
    }

    [Fact]
    public async Task RealChildProcess_DoesNotInheritAnEngineSecret()
    {
        // A unique name so parallel tests are unaffected; it is not allowlisted, exactly like
        // DATABASE_URL or STRIPE_SECRET_KEY.
        const string Probe = "SA_BUILD_ENV_SECRET_PROBE";
        Environment.SetEnvironmentVariable(Probe, "must-not-leak");
        try
        {
            var runner = new ProcessRunner();
            var (fileName, arguments) = OperatingSystem.IsWindows()
                ? ("cmd.exe", "/c set")
                : ("/bin/sh", "-c env");

            var result = await runner.RunAsync(fileName, arguments, Path.GetTempPath());

            result.IsSuccess.Should().BeTrue();
            result.StandardOutput.Should().NotContain(Probe);
            result.StandardOutput.Should().NotContain("must-not-leak");
            result.StandardOutput.Should().MatchRegex("(?im)^PATH=", "toolchains still need PATH");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Probe, null);
        }
    }

    [Fact]
    public async Task CancelledBuildStep_KillsTheChild()
    {
        // The read side gives up on cancellation by itself; what matters is that the CHILD is
        // gone. Without the kill a hung toolchain (an install waiting on the network) kept
        // running — and holding the job directory — after the worker had moved on. The child
        // writes its own PID so the test can prove it no longer exists.
        var pidFile = Path.Combine(Path.GetTempPath(), $"sa-kill-probe-{Guid.NewGuid():N}.pid");
        var runner = new ProcessRunner();
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("powershell.exe", $"-NoProfile -NonInteractive -Command \"Set-Content -LiteralPath '{pidFile}' -Value $PID -Encoding ascii; Start-Sleep -Seconds 60\"")
            : ("/bin/sh", $"-c \"echo $$ > '{pidFile}'; exec sleep 60\"");
        using var cts = new CancellationTokenSource();
        try
        {
            var run = runner.RunAsync(fileName, arguments, Path.GetTempPath(), cts.Token);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!File.Exists(pidFile) || new FileInfo(pidFile).Length == 0)
            {
                DateTime.UtcNow.Should().BeBefore(deadline, "the probe child should have started");
                await Task.Delay(100);
            }
            await Task.Delay(200); // let the write flush
            var pid = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);

            // Hold a handle to THIS process before cancelling: a bare PID can be reused by an
            // unrelated process within seconds on Windows (the whole suite spawns processes),
            // and a handle always refers to the process it was opened on.
            using var child = System.Diagnostics.Process.GetProcessById(pid);

            await cts.CancelAsync();
            var act = () => run;
            await act.Should().ThrowAsync<OperationCanceledException>();

            child.WaitForExit(TimeSpan.FromSeconds(15)).Should()
                .BeTrue("cancellation must kill the child process, not just stop reading it");
        }
        finally
        {
            try { File.Delete(pidFile); } catch (IOException) { }
        }
    }

    /// <summary>Exposes the production process runner without a toolchain.</summary>
    private sealed class ProcessRunner() : BuildStrategyBase(NullLogger.Instance)
    {
        public override ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

        public override Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public override List<string> ExtractBuildErrors(string buildOutput) => [];

        protected override int CountWarnings(string output) => 0;

        public Task<BuildResult> RunAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct = default) =>
            RunProcessAsync(fileName, arguments, workingDirectory, ct);
    }
}
