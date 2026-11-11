using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class BackgroundGenerationDispatcherTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static GenerateRequest Request(string id = "gen-1") => new() { GenerationId = id, Mode = "advanced", Tier = 2 };

    private static (BackgroundGenerationDispatcher Sut, IGenerationOrchestrator Orchestrator, CancellationTokenSource Stopping) BuildSut()
    {
        var stopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping.Token);

        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var sut = new BackgroundGenerationDispatcher(
            orchestrator, lifetime, NullLogger<BackgroundGenerationDispatcher>.Instance);
        return (sut, orchestrator, stopping);
    }

    [Fact]
    public async Task Dispatch_ReturnsWhileTheGenerationIsStillRunning()
    {
        var (sut, orchestrator, stopping) = BuildSut();
        using var _ = stopping;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverFinishes = new TaskCompletionSource<GenerateResponse>();
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.SetResult();
                return neverFinishes.Task;
            });

        // Returning at all is the assertion: the generation never completes.
        sut.Dispatch(Request());

        await started.Task.WaitAsync(Patience);
    }

    [Fact]
    public async Task Dispatch_RunsOnTheAppLifetimeToken()
    {
        var (sut, orchestrator, stopping) = BuildSut();
        using var _ = stopping;
        var seen = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seen.SetResult(call.ArgAt<CancellationToken>(1));
                return new GenerateResponse { JobId = "gen-1", Status = "building" };
            });

        sut.Dispatch(Request());

        var token = await seen.Task.WaitAsync(Patience);
        token.IsCancellationRequested.Should().BeFalse();
        await stopping.CancelAsync();
        token.IsCancellationRequested.Should().BeTrue("the generation stops with the host, and with nothing else");
    }

    [Fact]
    public async Task Dispatch_GenerationThatThrows_DoesNotBreakTheNextOne()
    {
        var (sut, orchestrator, stopping) = BuildSut();
        using var _ = stopping;
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.EnqueueAsync(Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-bad"), Arg.Any<CancellationToken>())
            .Returns<Task<GenerateResponse>>(_ => throw new InvalidOperationException("store unreachable"));
        orchestrator.EnqueueAsync(Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-good"), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                second.SetResult();
                return new GenerateResponse { JobId = "gen-good", Status = "building" };
            });

        sut.Dispatch(Request("gen-bad"));
        sut.Dispatch(Request("gen-good"));

        await second.Task.WaitAsync(Patience);
    }
}
