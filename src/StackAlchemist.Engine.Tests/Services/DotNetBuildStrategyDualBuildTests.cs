using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Orchestration coverage for the Compile Guarantee's two halves, driven through a stubbed
/// process runner so the sequencing rules are asserted without a 90-second real build.
///
/// The rule under test is the one the product is sold on: a Tier-2 archive is verified only
/// if BOTH <c>dotnet build</c> and <c>npm run build</c> passed. Before the dual build landed,
/// a frontend that did not compile still shipped stamped "Compile Verified".
/// <see cref="Integration.V1TemplateCompileTests"/> is the real-toolchain counterpart.
/// </summary>
public sealed class DotNetBuildStrategyDualBuildTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sa-dual-build-" + Guid.NewGuid().ToString("N")[..8]);

    public DotNetBuildStrategyDualBuildTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dotnet"));
        Directory.CreateDirectory(Path.Combine(_root, "nextjs"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    [Fact]
    public async Task BothHalvesPass_RecordsEveryCommandAndSucceeds()
    {
        WritePackageJson(withTypecheck: true);
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        strategy.Commands.Should().Equal(
            "dotnet restore",
            "dotnet build --no-restore",
            "npm ci --no-audit --no-fund",
            "npm run typecheck",
            "npm run build");

        result.Steps.Where(s => s.Half == BuildHalf.DotNet).Should().HaveCount(2)
            .And.OnlyContain(s => s.IsSuccess);
        result.Steps.Where(s => s.Half == BuildHalf.NextJs && !s.Skipped).Should().HaveCount(3)
            .And.OnlyContain(s => s.IsSuccess);
    }

    [Fact]
    public async Task NextJsBuildFails_FailsTheWholeBuild()
    {
        // The exact regression the dual build exists to prevent: .NET compiles, the frontend
        // does not, and the old strategy returned success — a "Compile Verified" archive with
        // a broken frontend, sold under a guarantee, with no refund triggered.
        WritePackageJson(withTypecheck: false);
        var strategy = new StubbedStrategy
        {
            Failures =
            {
                ["run build"] = new StubResponse(1, "./src/app/page.tsx\nType error: Property 'id' does not exist on type 'Invoice'."),
            },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeFalse("a frontend that does not compile is not a verified archive");
        result.Steps.Should().Contain(s => s.Half == BuildHalf.NextJs && s.Command == "npm run build" && !s.IsSuccess);
        result.Steps.Should().Contain(s => s.Half == BuildHalf.DotNet && s.IsSuccess);
    }

    [Fact]
    public async Task NextJsFailure_SurfacesFrontendErrorsToTheRepairLoop()
    {
        // The repair prompt is built from the transcript. If the frontend's diagnostics are
        // not extractable from it, the LLM gets an empty error list and burns all 3 retries
        // regenerating blind — which ends in an automatic refund on a fixable failure.
        WritePackageJson(withTypecheck: false);
        var strategy = new StubbedStrategy
        {
            Failures =
            {
                ["run build"] = new StubResponse(1, "Type error: Property 'id' does not exist on type 'Invoice'."),
            },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        var errors = strategy.ExtractBuildErrors(result.StandardOutput + "\n" + result.ErrorOutput);

        errors.Should().ContainSingle()
            .Which.Should().Contain("Property 'id' does not exist");
    }

    [Fact]
    public async Task DotNetFails_ShortCircuitsBeforeTouchingNpm()
    {
        // npm ci on a generated project is minutes of wall clock. There is nothing to learn
        // from it when the backend already failed to compile.
        WritePackageJson(withTypecheck: true);
        var strategy = new StubbedStrategy
        {
            Failures = { ["build --no-restore"] = new StubResponse(1, "Program.cs(10,5): error CS1002: ; expected") },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeFalse();
        strategy.Commands.Should().NotContain(c => c.StartsWith("npm", StringComparison.Ordinal));
        result.Steps.Should().NotContain(s => s.Half == BuildHalf.NextJs);
    }

    [Fact]
    public async Task NpmCiFails_FallsBackToNpmInstall()
    {
        // The correction loop legitimately adds dependencies, which desyncs the lockfile and
        // makes `npm ci` hard-fail. That is not a compile failure and must not spend a retry.
        WritePackageJson(withTypecheck: false);
        var strategy = new StubbedStrategy
        {
            Failures = { ["ci --no-audit --no-fund"] = new StubResponse(1, "npm ERR! `npm ci` can only install with an existing package-lock.json") },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        strategy.Commands.Should().ContainInOrder(
            "npm ci --no-audit --no-fund",
            "npm install --no-audit --no-fund",
            "npm run build");
    }

    [Fact]
    public async Task NoTypecheckScript_SkipsItRatherThanFailing()
    {
        WritePackageJson(withTypecheck: false);
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        strategy.Commands.Should().NotContain("npm run typecheck");
        result.Steps.Should().ContainSingle(s => s.Command == "npm run typecheck" && s.Skipped);
    }

    [Fact]
    public async Task NoNextJsDirectory_RecordsASkippedStepInsteadOfClaimingSuccess()
    {
        // A .NET-only tree is a legitimate pass, but the frontend half was NOT verified.
        // Recording the skip is what stops the delivery UI badging a half that never ran.
        Directory.Delete(Path.Combine(_root, "nextjs"), recursive: true);
        var strategy = new StubbedStrategy();

        var result = await strategy.ExecuteBuildAsync(_root);

        result.IsSuccess.Should().BeTrue();
        result.Steps.Should().ContainSingle(s => s.Half == BuildHalf.NextJs)
            .Which.Skipped.Should().BeTrue();
    }

    [Fact]
    public async Task StepsRecordDiagnosticCounts()
    {
        WritePackageJson(withTypecheck: false);
        var strategy = new StubbedStrategy
        {
            Responses =
            {
                ["build --no-restore"] = new StubResponse(
                    0, "Program.cs(11,5): warning CS0168: variable is declared but never used\nBuild succeeded."),
            },
        };

        var result = await strategy.ExecuteBuildAsync(_root);

        var buildStep = result.Steps.Single(s => s.Command == "dotnet build --no-restore");
        buildStep.WarningCount.Should().Be(1);
        buildStep.ErrorCount.Should().Be(0);
    }

    private void WritePackageJson(bool withTypecheck)
    {
        var scripts = withTypecheck
            ? """{ "build": "next build", "typecheck": "tsc --noEmit" }"""
            : """{ "build": "next build" }""";

        File.WriteAllText(
            Path.Combine(_root, "nextjs", "package.json"),
            $$"""{ "name": "stub", "scripts": {{scripts}} }""");
    }

    private sealed record StubResponse(int ExitCode, string Output);

    /// <summary>
    /// Replaces the process runner with a lookup table keyed on a substring of the command
    /// arguments. Records every command in execution order so sequencing can be asserted.
    /// </summary>
    private sealed class StubbedStrategy() : DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance)
    {
        public List<string> Commands { get; } = [];

        /// <summary>Arguments substring → non-zero response.</summary>
        public Dictionary<string, StubResponse> Failures { get; } = new(StringComparer.Ordinal);

        /// <summary>Arguments substring → zero-exit response with custom output.</summary>
        public Dictionary<string, StubResponse> Responses { get; } = new(StringComparer.Ordinal);

        protected override Task<BuildResult> RunProcessAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            CancellationToken ct)
        {
            // fileName is an absolute npm.cmd path on Windows; normalise to what a reader
            // (and the recorded step's Command) would call it.
            var tool = fileName.Contains("npm", StringComparison.OrdinalIgnoreCase) ? "npm" : "dotnet";
            Commands.Add($"{tool} {arguments}");

            var stub = Failures.FirstOrDefault(kvp => arguments.Contains(kvp.Key, StringComparison.Ordinal)).Value
                ?? Responses.FirstOrDefault(kvp => arguments.Contains(kvp.Key, StringComparison.Ordinal)).Value
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
