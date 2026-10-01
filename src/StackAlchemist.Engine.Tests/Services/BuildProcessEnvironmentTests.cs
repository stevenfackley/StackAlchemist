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
        // Pipe reads ignore cancellation; without the kill a hung toolchain held the compile
        // worker indefinitely while the caller believed it had given up.
        var runner = new ProcessRunner();
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c ping -n 60 127.0.0.1 >NUL")
            : ("/bin/sh", "-c \"sleep 60\"");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var act = () => runner.RunAsync(fileName, arguments, Path.GetTempPath(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the child must be killed, not waited out");
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
