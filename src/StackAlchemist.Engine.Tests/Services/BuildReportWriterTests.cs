using System.Text.Json;
using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Covers <c>build-report.json</c> — the file docs/advanced-docs/compile-guarantee.md has
/// promised customers since the guarantee was written and which nothing produced until now.
///
/// The field names asserted here are a published contract: they are documented, and support
/// requests will quote them. A rename that does not also change the doc is a break.
/// </summary>
public sealed class BuildReportWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "sa-build-report-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    [Fact]
    public void BothHalvesPassed_ReportsVerifiedWithPerHalfVerdicts()
    {
        var job = MakeJob();
        job.BuildAttempts.Add(PassingAttempt(1));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("verified");
        report.AttemptsUsed.Should().Be(1);
        report.MaxAttempts.Should().Be(3);
        Half(report, "dotnet").Status.Should().Be("passed");
        Half(report, "nextjs").Status.Should().Be("passed");
        Half(report, "nextjs").Commands.Should().Contain("npm run build");
    }

    [Fact]
    public void FrontendFailed_ReportsThatHalfFailed_NotJustAnOverallFailure()
    {
        // "The build failed" and "the Next.js half failed" are different facts, and only the
        // second one tells a customer (or support) where to look.
        var job = MakeJob();
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: false, steps:
            [
                Step(BuildHalf.DotNet, "dotnet restore", 0),
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 0),
                Step(BuildHalf.NextJs, "npm ci", 0),
                Step(BuildHalf.NextJs, "npm run build", 1, errorCount: 3),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("failed");
        Half(report, "dotnet").Status.Should().Be("passed");
        Half(report, "nextjs").Status.Should().Be("failed");
    }

    [Fact]
    public void DotNetFailedBeforeFrontendRan_ReportsTheFrontendAsNotRun()
    {
        // Not "skipped" and emphatically not "passed": the half was never reached, so there
        // is no evidence about it either way and the UI must not badge it.
        var job = MakeJob();
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: false, steps:
            [
                Step(BuildHalf.DotNet, "dotnet restore", 0),
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 1, errorCount: 2),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        Half(report, "nextjs").Status.Should().Be("not_run");
    }

    [Fact]
    public void SkippedFrontend_IsNotReportedAsPassed()
    {
        var job = MakeJob();
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: true, steps:
            [
                Step(BuildHalf.DotNet, "dotnet restore", 0),
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 0),
                Step(BuildHalf.NextJs, "npm run build", 0, skipped: true),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("verified", "the .NET-only archive did compile");
        Half(report, "nextjs").Status.Should().Be("skipped");
    }

    [Fact]
    public void HalvesReflectTheFinalAttempt_NotTheFirst()
    {
        // The archive contains the code from the LAST attempt. Badging from attempt 1 would
        // report a failure the customer never received.
        var job = MakeJob();
        var failed = new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: false, steps: [Step(BuildHalf.NextJs, "npm run build", 1)]);
        failed.CorrectedFiles.Add("nextjs/src/app/page.tsx");
        job.BuildAttempts.Add(failed);
        job.BuildAttempts.Add(PassingAttempt(2));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("verified");
        report.AttemptsUsed.Should().Be(2);
        Half(report, "nextjs").Status.Should().Be("passed");
        report.Attempts[0].Status.Should().Be("failed");
        report.Attempts[0].CorrectedFiles.Should().ContainSingle()
            .Which.Should().Be("nextjs/src/app/page.tsx");
    }

    [Fact]
    public async Task WriteAsync_EmitsCamelCaseJsonAtTheDocumentedPath()
    {
        var job = MakeJob();
        job.BuildAttempts.Add(PassingAttempt(1));
        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        var path = await BuildReportWriter.WriteAsync(_dir, report);

        Path.GetFileName(path).Should().Be("build-report.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("status").GetString().Should().Be("verified");
        root.GetProperty("attemptsUsed").GetInt32().Should().Be(1);
        root.GetProperty("maxAttempts").GetInt32().Should().Be(3);
        root.GetProperty("generationId").GetString().Should().Be(job.GenerationId);

        var firstStep = root.GetProperty("attempts")[0].GetProperty("steps")[0];
        firstStep.GetProperty("half").GetString().Should().Be("dotnet");
        firstStep.GetProperty("command").GetString().Should().Be("dotnet restore");
        firstStep.GetProperty("exitCode").GetInt32().Should().Be(0);
        firstStep.GetProperty("status").GetString().Should().Be("passed");
        firstStep.TryGetProperty("durationMs", out _).Should().BeTrue();
        firstStep.TryGetProperty("warningCount", out _).Should().BeTrue();
    }

    [Fact]
    public void ToLogSummary_IsFencedAndParseable()
    {
        // The delivery page reads exactly this block out of generations.build_log to decide
        // which halves it may claim. If the fence or the shape drifts, the badge goes blank
        // rather than lying — but it should not drift.
        var job = MakeJob();
        job.BuildAttempts.Add(PassingAttempt(1));
        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        var summary = BuildReportWriter.ToLogSummary(report);

        summary.Should().StartWith(BuildReportWriter.SummaryBeginMarker);
        summary.Should().EndWith(BuildReportWriter.SummaryEndMarker);

        var json = summary[BuildReportWriter.SummaryBeginMarker.Length..^BuildReportWriter.SummaryEndMarker.Length];
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("status").GetString().Should().Be("verified");
        var halves = document.RootElement.GetProperty("halves").EnumerateArray().ToList();
        halves.Should().HaveCount(2);
        halves[0].GetProperty("half").GetString().Should().Be("dotnet");
        halves[0].GetProperty("label").GetString().Should().Be(".NET");
        halves[1].GetProperty("label").GetString().Should().Be("Next.js");
    }

    [Fact]
    public void SupersededStep_DoesNotCondemnTheHalfThatCompiled()
    {
        // `npm ci` hard-fails whenever the correction loop added a dependency, and the
        // strategy retries it as `npm install`. Counting that retried failure marked the
        // frontend half "failed" on archives that compiled — in a report whose own top-level
        // status said "verified".
        var job = MakeJob();
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: true, steps:
            [
                Step(BuildHalf.DotNet, "dotnet restore", 0),
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 0),
                Step(BuildHalf.NextJs, "npm ci", 1, errorCount: 1, superseded: true),
                Step(BuildHalf.NextJs, "npm install", 0),
                Step(BuildHalf.NextJs, "npm run build", 0),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("verified");
        Half(report, "nextjs").Status.Should().Be("passed");
        Half(report, "nextjs").Commands.Should().Equal("npm install", "npm run build");
        report.Attempts[0].Steps.Single(s => s.Command == "npm ci").Status.Should().Be("superseded");
    }

    [Fact]
    public void SupersededStepFollowedByARealFailure_StillFailsTheHalf()
    {
        // Superseding must not launder a genuine failure: if the fallback fails too, nothing
        // redid its work and the half did not compile.
        var job = MakeJob();
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: false, steps:
            [
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 0),
                Step(BuildHalf.NextJs, "npm ci", 1, superseded: true),
                Step(BuildHalf.NextJs, "npm install", 1, errorCount: 4),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Status.Should().Be("failed");
        Half(report, "nextjs").Status.Should().Be("failed");
    }

    [Fact]
    public void PythonReactJob_ReportsItsOwnStackNotDotNetAndNextJs()
    {
        // FastAPI + React is a live, user-selectable Tier-2 option. The report hardcoded a
        // ".NET" and a "Next.js" half for every generation, so those customers received a
        // build-report.json describing a stack they never chose — both halves "not_run",
        // under `"status": "verified"`, and the delivery page badged exactly that.
        var job = MakeJob(ProjectType.PythonReact);
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: true, steps:
            [
                Step(BuildHalf.Python, "python -m flake8 .", 0),
                Step(BuildHalf.Python, "python -m pytest --collect-only", 0),
                Step(BuildHalf.React, "npm run lint", 0),
                Step(BuildHalf.React, "npx tsc --noEmit", 0),
            ]));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.ProjectType.Should().Be("PythonReact");
        report.Halves.Select(h => h.Half).Should().Equal("python", "react");
        report.Halves.Select(h => h.Label).Should().Equal("FastAPI", "React");
        report.Halves.Should().OnlyContain(h => h.Status == "passed");
        report.Halves.Should().NotContain(h => h.Label == ".NET" || h.Label == "Next.js");

        var summary = BuildReportWriter.ToLogSummary(report);
        summary.Should().NotContain("Next.js");
        summary.Should().Contain("\"half\":\"react\"");
    }

    [Fact]
    public void PythonReactJob_WithNoRecordedSteps_DoesNotClaimVerifiedHalves()
    {
        // Belt and braces on the same bug: even with an empty step list the halves must be
        // the customer's own, and must read "not_run" rather than borrowing another stack's.
        var job = MakeJob(ProjectType.PythonReact);
        job.BuildAttempts.Add(new BuildAttemptRecord { Attempt = 1, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: true, steps: []));

        var report = BuildReportWriter.Create(job, maxAttempts: 3, DateTimeOffset.UtcNow);

        report.Halves.Select(h => h.Half).Should().Equal("python", "react");
        report.Halves.Should().OnlyContain(h => h.Status == "not_run");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GenerationContext MakeJob(ProjectType projectType = ProjectType.DotNetNextJs) => new()
    {
        GenerationId = "gen-" + Guid.NewGuid().ToString("N")[..8],
        Mode = "advanced",
        Tier = 2,
        ProjectType = projectType,
    };

    private static BuildHalfSummary Half(BuildReport report, string half) =>
        report.Halves.Single(h => h.Half == half);

    private static BuildAttemptRecord PassingAttempt(int attempt) =>
        new BuildAttemptRecord { Attempt = attempt, StartedAt = DateTimeOffset.UtcNow }
            .With(passed: true, steps:
            [
                Step(BuildHalf.DotNet, "dotnet restore", 0),
                Step(BuildHalf.DotNet, "dotnet build --no-restore", 0),
                Step(BuildHalf.NextJs, "npm ci", 0),
                Step(BuildHalf.NextJs, "npm run build", 0, warningCount: 2),
            ]);

    private static BuildStepResult Step(
        BuildHalf half,
        string command,
        int exitCode,
        int errorCount = 0,
        int warningCount = 0,
        bool skipped = false,
        bool superseded = false) => new()
        {
            Half = half,
            Command = command,
            ExitCode = exitCode,
            DurationMs = 1_234,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            Skipped = skipped,
            Superseded = superseded,
        };
}

internal static class BuildAttemptRecordTestExtensions
{
    public static BuildAttemptRecord With(
        this BuildAttemptRecord record,
        bool passed,
        IReadOnlyList<BuildStepResult> steps)
    {
        record.Passed = passed;
        record.Steps = steps;
        record.CompletedAt = record.StartedAt.AddSeconds(90);
        record.Output = "$ dotnet restore  (exit 0)\n…";
        record.Errors = passed ? [] : ["error CS1002: ; expected"];
        return record;
    }
}
