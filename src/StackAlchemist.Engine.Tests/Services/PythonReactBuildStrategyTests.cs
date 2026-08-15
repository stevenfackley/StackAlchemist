using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Step recording for the FastAPI + React deliverable, driven through a stubbed process
/// runner.
///
/// This strategy ran real checks and recorded none of them, so <c>build-report.json</c> had
/// nothing to summarise: every FastAPI + React archive shipped with both halves "not_run"
/// under a top-level <c>"status": "verified"</c> — and the halves it named were ".NET" and
/// "Next.js", a stack the customer never chose. FastAPI + React is a live Tier-2 option on
/// the platform step, not a dev-only path.
/// </summary>
public sealed class PythonReactBuildStrategyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sa-py-react-" + Guid.NewGuid().ToString("N")[..8]);

    public PythonReactBuildStrategyTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "backend"));
        Directory.CreateDirectory(Path.Combine(_root, "frontend"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    [Fact]
    public async Task BothHalvesPass_RecordsEveryCommandAgainstItsOwnHalf()
    {
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        result.Steps.Where(s => s.Half == BuildHalf.Python).Select(s => s.Command).Should().Equal(
            "python -m pip install -r requirements.txt",
            "python -m flake8 .",
            "python -m pytest --collect-only");
        result.Steps.Where(s => s.Half == BuildHalf.React).Select(s => s.Command).Should().Equal(
            "npm install",
            "npm run lint",
            "npx tsc --noEmit");
        result.Steps.Should().OnlyContain(s => s.IsSuccess);

        // No BuildHalf from the other stack may appear — that mislabelling is the bug.
        result.Steps.Should().NotContain(s => s.Half == BuildHalf.DotNet || s.Half == BuildHalf.NextJs);
    }

    [Fact]
    public async Task PassingBuild_ReportsBothHalvesOfTheCustomersOwnStack()
    {
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);
        var report = ReportFor(result);

        report.Status.Should().Be("verified");
        report.Halves.Select(h => h.Label).Should().Equal("FastAPI", "React");
        report.Halves.Should().OnlyContain(h => h.Status == "passed",
            "a report that says 'verified' while both halves read 'not built' is not a verdict");
    }

    [Fact]
    public async Task FrontendFails_FailsTheBuildAndNamesTheFailingHalf()
    {
        var strategy = new StubbedStrategy
        {
            Failures = { ["tsc --noEmit"] = new StubResponse(1, "src/App.tsx: error TS2339: Property 'id' does not exist.") },
        };

        var result = await strategy.ExecuteBuildAsync(_root);
        var report = ReportFor(result);

        result.IsSuccess.Should().BeFalse();
        report.Status.Should().Be("failed");
        report.Halves.Single(h => h.Half == "python").Status.Should().Be("passed");
        report.Halves.Single(h => h.Half == "react").Status.Should().Be("failed");
    }

    [Fact]
    public async Task BackendFails_ShortCircuitsBeforeTouchingTheFrontend()
    {
        var strategy = new StubbedStrategy
        {
            Failures = { ["flake8"] = new StubResponse(1, "app/main.py:3:1: F401 'os' imported but unused") },
        };

        var result = await strategy.ExecuteBuildAsync(_root);
        var report = ReportFor(result);

        result.IsSuccess.Should().BeFalse();
        strategy.Commands.Should().NotContain(c => c.StartsWith("npm", StringComparison.Ordinal));
        report.Halves.Single(h => h.Half == "react").Status.Should().Be("not_run");
    }

    [Fact]
    public async Task FailureCarriesTheWholeTranscriptToTheRepairLoop()
    {
        // The repair prompt is built from the returned output. Returning only the failing
        // command's own result hid every earlier command — and the steps with it.
        var strategy = new StubbedStrategy
        {
            Failures = { ["flake8"] = new StubResponse(1, "app/main.py:3:1: F401 'os' imported but unused") },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        result.StandardOutput.Should().Contain("python -m pip install -r requirements.txt");
        result.StandardOutput.Should().Contain("F401");
        result.Steps.Should().NotBeEmpty("a failed attempt still has to be describable per half");
        strategy.ExtractBuildErrors(result.StandardOutput + "\n" + result.ErrorOutput)
            .Should().ContainSingle().Which.Should().Contain("F401");
    }

    [Fact]
    public async Task MissingFrontendDirectory_IsRecordedAsSkippedNotPassed()
    {
        Directory.Delete(Path.Combine(_root, "frontend"), recursive: true);
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);
        var report = ReportFor(result);

        result.IsSuccess.Should().BeTrue();
        report.Halves.Single(h => h.Half == "react").Status.Should().Be("skipped");
        report.Halves.Single(h => h.Half == "python").Status.Should().Be("passed");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BuildReport ReportFor(BuildResult result)
    {
        var job = new GenerationContext
        {
            GenerationId = "gen-py-react",
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.PythonReact,
        };
        job.BuildAttempts.Add(new BuildAttemptRecord
        {
            Attempt = 1,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Passed = result.IsSuccess,
            Steps = result.Steps,
            Output = result.StandardOutput,
        });

        return BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);
    }

    private sealed record StubResponse(int ExitCode, string Output);

    /// <summary>
    /// Replaces the process runner with a lookup table keyed on a substring of the command
    /// arguments. Records every command in execution order so sequencing can be asserted.
    /// </summary>
    private sealed class StubbedStrategy() : PythonReactBuildStrategy(NullLogger<PythonReactBuildStrategy>.Instance)
    {
        public List<string> Commands { get; } = [];

        /// <summary>Arguments substring → non-zero response.</summary>
        public Dictionary<string, StubResponse> Failures { get; } = new(StringComparer.Ordinal);

        protected override Task<BuildResult> RunProcessAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            CancellationToken ct)
        {
            // fileName is an absolute npm.cmd/npx.cmd path on Windows; normalise to what a
            // reader (and the recorded step's Command) would call it.
            var tool = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            Commands.Add($"{tool} {arguments}");

            var stub = Failures.FirstOrDefault(kvp => arguments.Contains(kvp.Key, StringComparison.Ordinal)).Value
                ?? new StubResponse(0, "ok");

            return Task.FromResult(new BuildResult
            {
                ExitCode = stub.ExitCode,
                StandardOutput = stub.Output,
                ErrorOutput = stub.ExitCode == 0 ? string.Empty : stub.Output,
            });
        }
    }
}
