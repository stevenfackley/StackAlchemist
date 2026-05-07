using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Integration coverage for the build-retry loop in <see cref="CompileWorkerService"/>:
/// drives the worker via its real channel-consumer loop with stubbed LLM / R2 / Compile
/// services, asserts retry counts, error-history accumulation, and terminal state
/// transitions for the success-after-retry and max-retries-exhausted paths.
///
/// The retry loop writes "fixed" LLM blocks to disk via <c>System.IO</c> directly
/// (not <see cref="IFileSystem"/>), so each test allocates a real temp directory and
/// the worker's own cleanup tears it down on exit.
/// </summary>
public class CompileWorkerRetryTests
{
    [Fact]
    public async Task WithBuildFailure_RetriesOnceAndSucceeds()
    {
        var outputDir = MakeTempOutputDir();
        var job = MakeJob(outputDir);

        var compile = Substitute.For<ICompileService>();
        // First call: fail with a CS0103. Second call: pass.
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<BuildResult>(new BuildResult { ExitCode = 1, StandardOutput = "", ErrorOutput = "error CS0103: name 'Foo' does not exist" }),
                Task.FromResult<BuildResult>(new BuildResult { ExitCode = 0, StandardOutput = "Build succeeded.", ErrorOutput = "" }));
        compile.ExtractBuildErrors(Arg.Any<string>(), Arg.Any<ProjectType>())
            .Returns(["CS0103: name 'Foo' does not exist"]);
        compile.BuildRetryContext(Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<int>())
            .Returns(call => $"RETRY-PROMPT[errors={call.ArgAt<List<string>>(1).Count},attempt={call.ArgAt<int>(2)}]");

        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse(
                "[[FILE:Models/Product.cs]]\npublic record Product;\n[[END_FILE]]",
                100, 50, "v1-stub-retry"));

        var reconstruction = new ReconstructionService();
        var r2 = Substitute.For<IR2UploadService>();
        r2.UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://r2.test/fake-presigned");
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();

        var (queue, capturedJob) = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email);

        // The retry succeeded → terminal state Success.
        capturedJob.State.Should().Be(GenerationState.Success);
        capturedJob.RetryCount.Should().Be(1, "one retry (failure → success)");
        capturedJob.BuildErrorHistory.Should().HaveCount(1)
            .And.ContainSingle(e => e.Contains("Attempt 1") && e.Contains("CS0103"));

        // ICompileService.ExecuteBuildAsync was called twice (fail, then success).
        await compile.Received(2).ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>());

        // ICompileService.BuildRetryContext was called once with the captured error history.
        // retryAttempt arg is job.RetryCount AFTER the state machine increments it (so 1 for a first retry).
        compile.Received(1).BuildRetryContext(
            Arg.Any<string>(),
            Arg.Is<List<string>>(h => h.Count == 1 && h[0].Contains("CS0103")),
            Arg.Is<int>(n => n == 1));

        // ILlmClient called exactly once (the retry).
        await llm.Received(1).GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // R2 upload happened.
        await r2.Received(1).UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        capturedJob.DownloadUrl.Should().Be("https://r2.test/fake-presigned");

        // Final delivery status push was Success with the download URL.
        await delivery.Received().UpdateStatusAsync(
            Arg.Any<string>(),
            Arg.Is(GenerationState.Success),
            Arg.Is<string?>(d => d == "https://r2.test/fake-presigned"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithBuildAlwaysFailing_ExhaustsRetriesAndMarksFailed()
    {
        var outputDir = MakeTempOutputDir();
        var job = MakeJob(outputDir);

        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BuildResult>(new BuildResult
            {
                ExitCode = 1,
                StandardOutput = "",
                ErrorOutput = "error CS9999: persistent failure",
            }));
        compile.ExtractBuildErrors(Arg.Any<string>(), Arg.Any<ProjectType>())
            .Returns(["CS9999: persistent failure"]);
        compile.BuildRetryContext(Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<int>())
            .Returns("retry-prompt");

        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("[[FILE:dummy.cs]]\nclass Dummy{}\n[[END_FILE]]", 10, 5, "v1-stub-retry"));

        var reconstruction = new ReconstructionService();
        var r2 = Substitute.For<IR2UploadService>();
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();

        var (_, capturedJob) = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email);

        // After 3 retries exhausted, the job is terminal Failed.
        capturedJob.State.Should().Be(GenerationState.Failed);
        capturedJob.RetryCount.Should().Be(3, "MaxRetries=3 in CompileWorkerService");
        capturedJob.BuildErrorHistory.Should().HaveCount(4, "initial attempt + 3 retries = 4 build attempts");
        capturedJob.ErrorMessage.Should().NotBeNull().And.Contain("Build failed after 3 retries");

        // No R2 upload — never reached pack/upload.
        await r2.DidNotReceive().UploadZipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Final delivery status push was Failed with the error message.
        await delivery.Received().UpdateStatusAsync(
            Arg.Any<string>(),
            Arg.Is(GenerationState.Failed),
            Arg.Any<string?>(),
            Arg.Is<string?>(m => m != null && m.Contains("Build failed after 3 retries")),
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GenerationContext MakeJob(string outputDir) => new()
    {
        GenerationId = "retry-" + Guid.NewGuid(),
        Mode = "advanced",
        Tier = 2,
        ProjectType = ProjectType.DotNetNextJs,
        Prompt = "Build a Product CRUD app",
        OutputDirectory = outputDir,
        State = GenerationState.Building, // orchestrator hands the job over already in Building.
    };

    private static string MakeTempOutputDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stackalchemist-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Runs <see cref="CompileWorkerService"/> end-to-end against the supplied job:
    /// writes the job to a fresh channel, completes the writer so the worker exits
    /// after processing, awaits the ExecuteTask, and returns the channel + the
    /// captured <see cref="GenerationContext"/> (same object, mutated in place).
    /// </summary>
    private static async Task<(Channel<GenerationContext> Queue, GenerationContext CapturedJob)> RunWorkerAsync(
        GenerationContext job,
        ICompileService compile,
        ILlmClient llm,
        IReconstructionService reconstruction,
        IR2UploadService r2,
        IDeliveryService delivery,
        IEmailService email)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        await queue.Writer.WriteAsync(job);
        queue.Writer.Complete();

        var worker = new CompileWorkerService(
            queue.Reader,
            compile,
            llm,
            reconstruction,
            r2,
            delivery,
            email,
            NullLogger<CompileWorkerService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        return (queue, job);
    }
}
