using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Coverage for the Compile Guarantee refund wiring at the final-build-failure
/// branch of <see cref="CompileWorkerService"/>. <see cref="IRefundService"/> is
/// mocked here — the actual Stripe/billing-store orchestration lives in
/// <see cref="Services.StripeRefundServiceTests"/>. These tests only assert the
/// worker's orchestration: who it calls, when, and that a refund-side failure
/// never escapes the worker loop or flips the generation's terminal status.
/// </summary>
public class CompileWorkerRefundTests
{
    [Fact]
    public async Task FinalFailure_PaidTierWithRefundIssued_EmailsTheCustomer()
    {
        var job = MakeJob(tier: 2);

        var (compile, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationOwnerEmailAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns("owner@example.com");
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();
        refund.RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns(RefundOutcome.Issued);

        var capturedJob = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email, refund);

        capturedJob.State.Should().Be(GenerationState.Failed);

        await refund.Received(1).RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync(
            "owner@example.com",
            Arg.Is<string>(s => s.Contains("refund", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalFailure_Tier0_NeverAttemptsRefund()
    {
        var job = MakeJob(tier: 0);

        var (compile, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();

        var capturedJob = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email, refund);

        capturedJob.State.Should().Be(GenerationState.Failed);

        await refund.DidNotReceive().RefundFailedGenerationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalFailure_NoEligibleTransaction_SkipsEmailWithoutThrowing()
    {
        var job = MakeJob(tier: 2);

        var (compile, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();
        refund.RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns(RefundOutcome.NotEligible); // already refunded / no completed transaction

        var capturedJob = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email, refund);

        capturedJob.State.Should().Be(GenerationState.Failed);
        capturedJob.ErrorMessage.Should().Contain("Build failed after 3 retries");

        await refund.Received(1).RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>());
        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalFailure_RefundServiceThrows_WorkerCompletesAndGenerationStaysFailed()
    {
        var job = MakeJob(tier: 3);

        var (compile, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();
        refund.RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns<Task<RefundOutcome>>(_ => throw new InvalidOperationException("Stripe unreachable"));

        var capturedJob = await RunWorkerAsync(job, compile, llm, reconstruction, r2, delivery, email, refund);

        // The worker loop must not crash, and the generation's terminal status
        // (already persisted via deliveryService.UpdateStatusAsync before the
        // refund attempt) must not be touched by the refund failure.
        capturedJob.State.Should().Be(GenerationState.Failed);
        capturedJob.ErrorMessage.Should().Contain("Build failed after 3 retries");

        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Only ever the one Failed status push — a refund failure doesn't cause a
        // second, conflicting delivery update.
        await delivery.Received(1).UpdateStatusAsync(
            job.GenerationId,
            GenerationState.Failed,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ── Failures outside the build-retry loop ────────────────────────────────

    private static ICompileService CompileThatThrows(Exception ex)
    {
        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns<Task<BuildResult>>(_ => throw ex);
        return compile;
    }

    [Fact]
    public async Task WorkerThrows_PaidTier_CategoryOffersNoRetry_RefundsAndSendsTheUnfinishedBuildEmail()
    {
        // The sandbox failing to start is the build never running, not the build failing: the
        // result page shows "contact support" with no Retry, so the payment goes back.
        var job = MakeJob(tier: 2);
        var (_, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationOwnerEmailAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns("owner@example.com");
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();
        refund.RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns(RefundOutcome.Issued);

        await RunWorkerAsync(
            job, CompileThatThrows(new InvalidOperationException("chown exited 1")), llm, reconstruction, r2, delivery, email, refund);

        job.State.Should().Be(GenerationState.Failed);
        await delivery.Received(1).UpdateStatusAsync(
            job.GenerationId, GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
        await refund.Received(1).RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync(
            "owner@example.com",
            EmailTemplates.RefundIssuedForUnfinishedBuild(2).Subject,
            Arg.Is<string>(html => !html.Contains("Compile Guarantee") && !html.Contains("correction")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkerThrows_PaidTier_CategoryTheCustomerCanRetry_IsNotRefunded()
    {
        // A refund would take the transaction out of 'completed', and the Retry the result page
        // shows for a network failure would then answer 402.
        var job = MakeJob(tier: 2);
        var (_, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        var email = Substitute.For<IEmailService>();
        var refund = Substitute.For<IRefundService>();

        await RunWorkerAsync(
            job, CompileThatThrows(new HttpRequestException("registry unreachable")), llm, reconstruction, r2, delivery, email, refund);

        job.State.Should().Be(GenerationState.Failed);
        await delivery.Received(1).UpdateStatusAsync(
            job.GenerationId, GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            ErrorCategorizer.Network, Arg.Any<CancellationToken>());
        await refund.DidNotReceiveWithAnyArgs().RefundFailedGenerationAsync(default!, default);
    }

    [Fact]
    public async Task WorkerThrows_Tier0_IsNeverRefunded()
    {
        var job = MakeJob(tier: 0);
        var (_, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var refund = Substitute.For<IRefundService>();

        await RunWorkerAsync(
            job, CompileThatThrows(new InvalidOperationException("chown exited 1")), llm, reconstruction, r2,
            Substitute.For<IDeliveryService>(), Substitute.For<IEmailService>(), refund);

        job.State.Should().Be(GenerationState.Failed);
        await refund.DidNotReceiveWithAnyArgs().RefundFailedGenerationAsync(default!, default);
    }

    [Fact]
    public async Task MissingOutputDirectory_PaidTier_IsRefunded()
    {
        var job = MakeJob(tier: 3);
        job.OutputDirectory = null;
        var (compile, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var refund = Substitute.For<IRefundService>();

        await RunWorkerAsync(
            job, compile, llm, reconstruction, r2,
            Substitute.For<IDeliveryService>(), Substitute.For<IEmailService>(), refund);

        job.State.Should().Be(GenerationState.Failed);
        await refund.Received(1).RefundFailedGenerationAsync(job.GenerationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HostStopsMidJob_IsNeitherFailedNorRefunded()
    {
        // A deploy stops the host while a paid build is compiling. That is not the build failing:
        // the row keeps its in-progress status for the reconciler, and nothing is refunded here.
        var job = MakeJob(tier: 2);
        var (_, llm, reconstruction, r2) = MakeAlwaysFailingBuildMocks();
        var delivery = Substitute.For<IDeliveryService>();
        var refund = Substitute.For<IRefundService>();
        using var stopping = new CancellationTokenSource();
        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns<Task<BuildResult>>(_ =>
            {
                stopping.Cancel();
                throw new OperationCanceledException(stopping.Token);
            });

        await RunWorkerAsync(
            job, compile, llm, reconstruction, r2, delivery, Substitute.For<IEmailService>(), refund,
            stopping.Token, keepQueueOpen: true);

        job.State.Should().Be(GenerationState.Building);
        await delivery.DidNotReceive().UpdateStatusAsync(
            Arg.Any<string>(), GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await refund.DidNotReceiveWithAnyArgs().RefundFailedGenerationAsync(default!, default);
    }

    [Fact]
    public async Task CompletionEmailThrowsAfterDelivery_TheBuildStaysDeliveredAndIsNotRefunded()
    {
        // Everything up to and including the success write worked: the customer has the archive.
        // The owner lookup or the email throwing afterwards used to flip the row to failed; with
        // the refund rule it would also have paid them back.
        var job = MakeJob(tier: 2);
        var compile = Substitute.For<ICompileService>();
        compile.ExecuteBuildAsync(Arg.Any<string>(), Arg.Any<ProjectType>(), Arg.Any<CancellationToken>())
            .Returns(new BuildResult { ExitCode = 0, StandardOutput = "ok", ErrorOutput = "" });
        var r2 = Substitute.For<IR2UploadService>();
        r2.UploadZipAsync(job.GenerationId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://downloads.example/project.zip");
        var delivery = Substitute.For<IDeliveryService>();
        delivery.GetGenerationOwnerEmailAsync(job.GenerationId, Arg.Any<CancellationToken>())
            .Returns<Task<string?>>(_ => throw new InvalidOperationException("profiles lookup failed"));
        var refund = Substitute.For<IRefundService>();

        await RunWorkerAsync(
            job, compile, Substitute.For<ILlmClient>(), new ReconstructionService(), r2,
            delivery, Substitute.For<IEmailService>(), refund);

        job.State.Should().Be(GenerationState.Success);
        await delivery.Received(1).UpdateStatusAsync(
            job.GenerationId, GenerationState.Success, "https://downloads.example/project.zip",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().UpdateStatusAsync(
            Arg.Any<string>(), GenerationState.Failed, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await refund.DidNotReceiveWithAnyArgs().RefundFailedGenerationAsync(default!, default);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GenerationContext MakeJob(int tier) => new()
    {
        GenerationId = "refund-" + Guid.NewGuid(),
        Mode = "advanced",
        Tier = tier,
        ProjectType = ProjectType.DotNetNextJs,
        Prompt = "Build a Product CRUD app",
        OutputDirectory = MakeTempOutputDir(),
        State = GenerationState.Building,
    };

    private static string MakeTempOutputDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stackalchemist-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Compile/LLM/R2 mocks wired so every build attempt fails identically —
    /// drives the job straight to MaxRetries-exhausted → Failed, which is the
    /// only branch that reaches the refund call site.
    /// </summary>
    private static (ICompileService Compile, ILlmClient Llm, IReconstructionService Reconstruction, IR2UploadService R2)
        MakeAlwaysFailingBuildMocks()
    {
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
        compile.BuildRetryContext(Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns("retry-prompt");

        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<LlmCallOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("[[FILE:dummy.cs]]\nclass Dummy{}\n[[END_FILE]]", 10, 5, "v1-stub-retry"));

        var reconstruction = new ReconstructionService();
        var r2 = Substitute.For<IR2UploadService>();

        return (compile, llm, reconstruction, r2);
    }

    private static async Task<GenerationContext> RunWorkerAsync(
        GenerationContext job,
        ICompileService compile,
        ILlmClient llm,
        IReconstructionService reconstruction,
        IR2UploadService r2,
        IDeliveryService delivery,
        IEmailService email,
        IRefundService refund,
        CancellationToken stopping = default,
        bool keepQueueOpen = false)
    {
        var queue = Channel.CreateUnbounded<GenerationContext>();
        await queue.Writer.WriteAsync(job);
        // A stopping host has an open queue: the worker ends because its token is cancelled.
        if (!keepQueueOpen)
            queue.Writer.Complete();

        var worker = new CompileWorkerService(
            queue.Reader,
            compile,
            llm,
            reconstruction,
            r2,
            delivery,
            email,
            refund,
            new InFlightGenerationRegistry(),
            NullLogger<CompileWorkerService>.Instance);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        await worker.StartAsync(linked.Token);
        try
        {
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The worker ended because the host stopped, which is what the test asked for.
        }

        return job;
    }
}
