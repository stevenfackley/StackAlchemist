using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Proves <c>build-report.json</c> is actually inside the archive the customer downloads.
///
/// The file has been promised in docs/advanced-docs/compile-guarantee.md since the guarantee
/// was written, and nothing produced it: build output lived only in the
/// <c>generations.build_log</c> column, which the customer never sees again after delivery.
/// The assertion has to happen inside the R2 upload callback, because the worker deletes the
/// output directory as soon as the job is terminal.
/// </summary>
public sealed class CompileWorkerBuildReportTests
{
    [Fact]
    public async Task PassingBuild_WritesBuildReportIntoTheArchiveBeforeUpload()
    {
        var outputDir = MakeTempOutputDir();
        var job = MakeJob(outputDir, tier: 2, GenerationState.Building);

        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BuildResult
            {
                ExitCode = 0,
                StandardOutput = "$ dotnet build --no-restore  (exit 0)\nBuild succeeded.",
                ErrorOutput = string.Empty,
                Steps =
                [
                    Step(BuildHalf.DotNet, "dotnet restore"),
                    Step(BuildHalf.DotNet, "dotnet build --no-restore"),
                    Step(BuildHalf.NextJs, "npm ci"),
                    Step(BuildHalf.NextJs, "npm run typecheck"),
                    Step(BuildHalf.NextJs, "npm run build"),
                ],
            }));

        string? reportJson = null;
        var r2 = Substitute.For<IR2UploadService>();
        r2.UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                // Snapshot what is on disk at zip time — that is the archive's contents.
                var path = Path.Combine(call.ArgAt<string>(1), BuildReportWriter.FileName);
                if (File.Exists(path))
                    reportJson = File.ReadAllText(path);
                return "https://r2.test/report";
            });

        var delivery = Substitute.For<IDeliveryService>();
        await RunWorkerAsync(job, compile, r2, delivery);

        job.State.Should().Be(GenerationState.Success);
        reportJson.Should().NotBeNull("compile-guarantee.md tells the customer this file is in their archive");

        using var document = JsonDocument.Parse(reportJson!);
        var root = document.RootElement;
        root.GetProperty("status").GetString().Should().Be("verified");
        root.GetProperty("generationId").GetString().Should().Be(job.GenerationId);
        root.GetProperty("attemptsUsed").GetInt32().Should().Be(1);

        var halves = root.GetProperty("halves").EnumerateArray()
            .ToDictionary(h => h.GetProperty("half").GetString()!, h => h.GetProperty("status").GetString());
        halves["dotnet"].Should().Be("passed");
        halves["nextjs"].Should().Be("passed");
    }

    [Fact]
    public async Task PassingBuild_AppendsTheVerdictToTheStreamedBuildLog()
    {
        var outputDir = MakeTempOutputDir();
        var job = MakeJob(outputDir, tier: 2, GenerationState.Building);

        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BuildResult
            {
                ExitCode = 0,
                StandardOutput = "Build succeeded.",
                ErrorOutput = string.Empty,
                Steps = [Step(BuildHalf.DotNet, "dotnet build --no-restore"), Step(BuildHalf.NextJs, "npm run build")],
            }));

        var appended = new List<string>();
        var delivery = Substitute.For<IDeliveryService>();
        delivery.AppendBuildLogAsync(Arg.Any<string>(), Arg.Do<string>(appended.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var r2 = Substitute.For<IR2UploadService>();
        r2.UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://r2.test/report");

        await RunWorkerAsync(job, compile, r2, delivery);

        // This block is what the delivery page parses to badge each half. Without it the UI
        // has nothing to base the claim on but a hardcoded string.
        var verdict = appended.Should().ContainSingle(entry => entry.Contains(BuildReportWriter.SummaryBeginMarker, StringComparison.Ordinal)).Subject;
        verdict.Should().Contain("\"status\":\"verified\"");
        verdict.Should().Contain("\"half\":\"nextjs\"");
    }

    [Fact]
    public async Task Tier1Blueprint_ShipsNoBuildReport()
    {
        // Nothing in a Blueprint is compiled. A report in that archive would be an unearned
        // compile claim — precisely what this work exists to remove.
        var outputDir = MakeTempOutputDir();
        var job = MakeJob(outputDir, tier: 1, GenerationState.Packing);

        var sawReport = true;
        var r2 = Substitute.For<IR2UploadService>();
        r2.UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sawReport = File.Exists(Path.Combine(call.ArgAt<string>(1), BuildReportWriter.FileName));
                return "https://r2.test/blueprint";
            });

        await RunWorkerAsync(job, Substitute.For<ICompileService>(), r2, Substitute.For<IDeliveryService>());

        job.State.Should().Be(GenerationState.Success);
        sawReport.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BuildStepResult Step(BuildHalf half, string command) => new()
    {
        Half = half,
        Command = command,
        ExitCode = 0,
        DurationMs = 2_500,
    };

    private static GenerationContext MakeJob(string outputDir, int tier, GenerationState state) => new()
    {
        GenerationId = "report-" + Guid.NewGuid().ToString("N")[..8],
        Mode = "advanced",
        Tier = tier,
        ProjectType = ProjectType.DotNetNextJs,
        Prompt = "Build an invoicing app",
        OutputDirectory = outputDir,
        State = state,
    };

    private static string MakeTempOutputDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stackalchemist-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task RunWorkerAsync(
        GenerationContext job,
        ICompileService compile,
        IR2UploadService r2,
        IDeliveryService delivery)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        await queue.Writer.WriteAsync(job);
        queue.Writer.Complete();

        var worker = new CompileWorkerService(
            queue.Reader,
            compile,
            Substitute.For<ILlmClient>(),
            new ReconstructionService(),
            r2,
            delivery,
            Substitute.For<IEmailService>(),
            Substitute.For<IRefundService>(),
            new InFlightGenerationRegistry(),
            NullLogger<CompileWorkerService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
