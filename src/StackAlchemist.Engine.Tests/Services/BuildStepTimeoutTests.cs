using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Engine.Tests.Integration;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The per-step deadline on Compile Guarantee commands (StackAlchemist#455). Before it, the only
/// cancellation that reached a build step was the host's stopping token, so a hung
/// <c>npm install</c> or a build that never exited held the in-process compile worker, and every
/// queued generation behind it, until the next deploy.
/// </summary>
public sealed class BuildStepTimeoutTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sa-step-timeout-" + Guid.NewGuid().ToString("N")[..8]);

    public BuildStepTimeoutTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dotnet"));
        Directory.CreateDirectory(Path.Combine(_root, "nextjs"));
        File.WriteAllText(Path.Combine(_root, "nextjs", "package.json"), """{ "scripts": { "build": "next build" } }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    [Fact]
    public void DefaultStepTimeout_IsTenMinutes()
    {
        new BuildStrategyOptions().StepTimeout.Should().Be(TimeSpan.FromMinutes(10));
        new HangingDotNetStrategy(new BuildStrategyOptions()).StepTimeout.Should().Be(TimeSpan.FromMinutes(10));
        new HangingDotNetStrategy(options: null).StepTimeout.Should().Be(TimeSpan.FromMinutes(10),
            "a strategy built without options (every test, any non-DI caller) still gets the deadline");
    }

    [Fact]
    public async Task StepThatOutlivesItsDeadline_FailsTheBuildWithATimedOutStep_InsteadOfHanging()
    {
        var strategy = new HangingDotNetStrategy(new BuildStrategyOptions { StepTimeout = ShortTimeout }, hangOn: "restore");

        var result = await strategy.ExecuteBuildAsync(_root).WaitAsync(TimeSpan.FromSeconds(30));

        result.IsSuccess.Should().BeFalse();
        result.TimedOut.Should().BeTrue();
        result.ExitCode.Should().Be(BuildStrategyBase.StepTimedOutExitCode);
        result.ErrorOutput.Should().Contain("`dotnet restore` timed out after 0.3 seconds");
        result.StandardOutput.Should().Contain("$ dotnet restore  (exit 124)",
            "the transcript (build log + repair prompt) must say which command hung");

        strategy.Commands.Should().ContainSingle("nothing runs after a step that timed out")
            .Which.Should().Be("dotnet restore");
        var step = result.Steps.Should().ContainSingle().Subject;
        step.Command.Should().Be("dotnet restore");
        step.ExitCode.Should().Be(BuildStrategyBase.StepTimedOutExitCode);
        step.IsSuccess.Should().BeFalse();
        strategy.ObservedCancellation.Should().BeTrue("the step's token is what tells RunProcessAsync to kill the tree");
    }

    [Fact]
    public async Task TimedOutNpmCi_IsNotRetriedAsNpmInstall()
    {
        // The `npm install` fallback is for a lockfile desync. A registry stall would hang it too,
        // doubling the time the worker is held for nothing.
        var strategy = new HangingDotNetStrategy(new BuildStrategyOptions { StepTimeout = ShortTimeout }, hangOn: "ci --no-audit");

        var result = await strategy.ExecuteBuildAsync(_root).WaitAsync(TimeSpan.FromSeconds(30));

        result.IsSuccess.Should().BeFalse();
        result.TimedOut.Should().BeTrue();
        strategy.Commands.Should().Equal("dotnet restore", "dotnet build --no-restore", "npm ci --no-audit --no-fund");
        result.Steps.Last().Superseded.Should().BeFalse("the timed-out `npm ci` is the step that decided the build");
    }

    [Fact]
    public async Task PythonStrategy_HungPipInstall_TimesOutBeforeFlake8()
    {
        Directory.CreateDirectory(Path.Combine(_root, "backend"));
        var strategy = new HangingPythonStrategy(new BuildStrategyOptions { StepTimeout = ShortTimeout }, hangOn: "pip install");

        var result = await strategy.ExecuteBuildAsync(_root).WaitAsync(TimeSpan.FromSeconds(30));

        result.TimedOut.Should().BeTrue();
        result.ErrorOutput.Should().Contain("`python -m pip install -r requirements.txt` timed out");
        strategy.Commands.Should().HaveCount(2, "venv, then the hung pip install, and nothing after it");
    }

    [Fact]
    public async Task StepsThatFinishInTime_AreUnaffected()
    {
        var strategy = new HangingDotNetStrategy(new BuildStrategyOptions { StepTimeout = TimeSpan.FromSeconds(30) }, hangOn: null);

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        result.TimedOut.Should().BeFalse();
        result.Steps.Should().OnlyContain(s => s.IsSuccess || s.Skipped);
    }

    [Fact]
    public async Task HostShutdown_StillPropagatesAsCancellation_NotAsATimedOutBuild()
    {
        // A deploy stopping the worker is not the generated code's fault: it must not be recorded
        // as a failed build (which would burn a repair attempt and, at the end, refund).
        var strategy = new HangingDotNetStrategy(new BuildStrategyOptions { StepTimeout = TimeSpan.FromHours(1) }, hangOn: "restore");
        using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = () => strategy.ExecuteBuildAsync(_root, shutdown.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TimedOutStep_KillsTheRealChildProcess()
    {
        // The real RunProcessAsync, a real child that would sleep for a minute. The child writes
        // its PID so the test can prove the deadline ended it rather than merely stopped waiting.
        var pidFile = Path.Combine(Path.GetTempPath(), $"sa-timeout-probe-{Guid.NewGuid():N}.pid");
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("powershell.exe", $"-NoProfile -NonInteractive -Command \"Set-Content -LiteralPath '{pidFile}' -Value $PID -Encoding ascii; Start-Sleep -Seconds 60\"")
            : ("/bin/sh", $"-c \"echo $$ > '{pidFile}'; exec sleep 60\"");
        var runner = new StepRunner(new BuildStrategyOptions { StepTimeout = TimeSpan.FromSeconds(10) });
        try
        {
            var run = runner.RunAsync("long-running probe", fileName, arguments);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!File.Exists(pidFile) || new FileInfo(pidFile).Length == 0)
            {
                DateTime.UtcNow.Should().BeBefore(deadline, "the probe child should have started");
                await Task.Delay(100);
            }
            await Task.Delay(200); // let the write flush
            var pid = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);

            // A handle, not a bare PID: PIDs are reused within seconds on Windows.
            using var child = System.Diagnostics.Process.GetProcessById(pid);

            var result = await run.WaitAsync(TimeSpan.FromSeconds(30));

            result.TimedOut.Should().BeTrue();
            result.ErrorOutput.Should().Contain("`long-running probe` timed out after 10 seconds");
            child.WaitForExit(TimeSpan.FromSeconds(15)).Should()
                .BeTrue("a timed-out step must kill its child, not just stop waiting for it");
        }
        finally
        {
            try { File.Delete(pidFile); } catch (IOException) { }
        }
    }

    // ── Stubs ────────────────────────────────────────────────────────────────

    /// <summary>A fake long-running process: the step whose arguments contain <c>hangOn</c> never
    /// exits on its own and ends only when its token is cancelled, as the real runner does.</summary>
    private static async Task<BuildResult> HangOrSucceed(
        string arguments, string? hangOn, Action onCancelled, CancellationToken ct)
    {
        if (hangOn is not null && arguments.Contains(hangOn, StringComparison.Ordinal))
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                onCancelled();
                throw;
            }
        }

        return new BuildResult { ExitCode = 0, StandardOutput = "ok", ErrorOutput = string.Empty };
    }

    private static string Tool(string fileName) =>
        fileName.Contains("npm", StringComparison.OrdinalIgnoreCase) ? "npm"
        : fileName.Contains("python", StringComparison.OrdinalIgnoreCase) ? "python"
        : "dotnet";

    private sealed class HangingDotNetStrategy(BuildStrategyOptions? options, string? hangOn = null)
        : DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance, options)
    {
        public List<string> Commands { get; } = [];

        public bool ObservedCancellation { get; private set; }

        protected override Task<BuildResult> RunProcessAsync(
            string fileName, string arguments, string workingDirectory, CancellationToken ct)
        {
            Commands.Add($"{Tool(fileName)} {arguments}");
            return HangOrSucceed(arguments, hangOn, () => ObservedCancellation = true, ct);
        }
    }

    private sealed class HangingPythonStrategy(BuildStrategyOptions? options, string? hangOn = null)
        : PythonReactBuildStrategy(NullLogger<PythonReactBuildStrategy>.Instance, options)
    {
        public List<string> Commands { get; } = [];

        protected override Task<BuildResult> RunProcessAsync(
            string fileName, string arguments, string workingDirectory, CancellationToken ct)
        {
            Commands.Add($"{Tool(fileName)} {arguments}");
            return HangOrSucceed(arguments, hangOn, () => { }, ct);
        }
    }

    /// <summary>Exposes the production step runner (real process, real deadline) without a toolchain.</summary>
    private sealed class StepRunner(BuildStrategyOptions options) : BuildStrategyBase(NullLogger.Instance, options)
    {
        public override ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

        public override Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public override List<string> ExtractBuildErrors(string buildOutput) => [];

        protected override int CountWarnings(string output) => 0;

        public Task<BuildResult> RunAsync(string displayCommand, string fileName, string arguments) =>
            RunStepAsync(BuildHalf.DotNet, displayCommand, fileName, arguments, Path.GetTempPath(),
                new StringBuilder(), [], CancellationToken.None);
    }
}

/// <summary><c>Generation:BuildStepTimeoutMinutes</c> reaches the strategies the real host builds.</summary>
public sealed class BuildStepTimeoutWiringTests(EngineWebApplicationFactory factory)
    : IClassFixture<EngineWebApplicationFactory>
{
    [Fact]
    public void Host_DefaultsEveryStrategyToTenMinutes()
    {
        factory.Services.GetServices<IBuildStrategy>().Should().NotBeEmpty()
            .And.AllSatisfy(s => ((BuildStrategyBase)s).StepTimeout.Should().Be(TimeSpan.FromMinutes(10)));
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("0", -1)] // <= 0 disables the deadline
    public void Host_AppliesTheConfiguredStepTimeout(string configured, int expectedMinutes)
    {
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Generation:BuildStepTimeoutMinutes", configured));
        var expected = expectedMinutes > 0 ? TimeSpan.FromMinutes(expectedMinutes) : Timeout.InfiniteTimeSpan;

        host.Services.GetServices<IBuildStrategy>().Should().NotBeEmpty()
            .And.AllSatisfy(s => ((BuildStrategyBase)s).StepTimeout.Should().Be(expected));
    }
}
