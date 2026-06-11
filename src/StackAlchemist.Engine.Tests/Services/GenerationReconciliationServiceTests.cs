using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class GenerationReconciliationServiceTests
{
    private static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(15);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static IConfiguration Config(int maxAttempts = 3) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Generation:MaxAttempts"] = maxAttempts.ToString(),
            })
            .Build();

    private static (GenerationReconciliationService Sut,
        IDeliveryService Delivery,
        IGenerationOrchestrator Orchestrator,
        InFlightGenerationRegistry Registry,
        PendingWriteBuffer Buffer)
        BuildSut(DateTimeOffset? processStart = null, int maxAttempts = 3)
    {
        var delivery = Substitute.For<IDeliveryService>();
        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var registry = new InFlightGenerationRegistry();
        var buffer = new PendingWriteBuffer();
        var sut = new GenerationReconciliationService(
            delivery,
            orchestrator,
            registry,
            buffer,
            Config(maxAttempts),
            new FixedTimeProvider(processStart ?? DateTimeOffset.UtcNow),
            NullLogger<GenerationReconciliationService>.Instance);
        return (sut, delivery, orchestrator, registry, buffer);
    }

    private static GenerationSnapshot StaleRow(
        string id = "gen-stale",
        string status = "generating_code",
        int attemptCount = 0,
        DateTimeOffset? updatedAt = null) =>
        new(id, status, 2, "advanced", "build an app", ProjectType.DotNetNextJs,
            new GenerationSchema(), null, attemptCount,
            updatedAt ?? DateTimeOffset.UtcNow.AddMinutes(-30));

    [Fact]
    public async Task RunOnce_RequeueableRowUnderBudget_ClaimsAndReenqueues()
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut();
        var row = StaleRow();
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>())
            .Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r =>
                r.GenerationId == row.Id &&
                r.Tier == row.Tier &&
                r.Mode == "advanced" &&
                r.Schema == row.Schema),
            Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_ClaimLost_DoesNothing()
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut();
        var row = StaleRow();
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>())
            .Returns(false);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_LateStageRow_FailsWithRestartMessage()
    {
        // Row last updated BEFORE this process started → the engine restarted on it.
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: "building", updatedAt: processStart.AddMinutes(-30));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow,
            Arg.Is<string>(m => m.Contains("restarted")),
            ErrorCategorizer.Internal,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_LateStageRowAfterProcessStart_FailsWithTimeoutMessage()
    {
        // Row updated AFTER this process started → it stalled while the engine was alive.
        var processStart = DateTimeOffset.UtcNow.AddHours(-2);
        var (sut, delivery, _, _, _) = BuildSut(processStart);
        var row = StaleRow(status: "uploading", updatedAt: processStart.AddHours(1));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow,
            Arg.Is<string>(m => m.Contains("stalled")),
            ErrorCategorizer.Internal,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_AttemptBudgetExhausted_FailsWithBudgetMessage()
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut(maxAttempts: 3);
        var row = StaleRow(attemptCount: 3);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow,
            Arg.Is<string>(m => m.Contains("3 attempts")),
            ErrorCategorizer.Internal,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_RowInFlightInThisProcess_IsSkipped()
    {
        var (sut, delivery, orchestrator, registry, _) = BuildSut();
        var row = StaleRow();
        registry.Add(row.Id);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_FlushesBufferedWritesBeforeSweeping()
    {
        var (sut, delivery, _, _, buffer) = BuildSut();
        var payload = new Dictionary<string, object?> { ["status"] = "success" };
        buffer.Enqueue(new PendingGenerationWrite("gen-buf", payload, DateTimeOffset.UtcNow));
        delivery.TryPatchOnceAsync("gen-buf", payload, Arg.Any<CancellationToken>())
            .Returns(true);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.Received(1).TryPatchOnceAsync("gen-buf", payload, Arg.Any<CancellationToken>());
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_FailedFlushReenqueuesYoungWrite()
    {
        var (sut, delivery, _, _, buffer) = BuildSut();
        var payload = new Dictionary<string, object?> { ["status"] = "failed" };
        buffer.Enqueue(new PendingGenerationWrite("gen-retry", payload, DateTimeOffset.UtcNow));
        delivery.TryPatchOnceAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        buffer.Count.Should().Be(1, "a young write that failed to flush stays buffered for the next tick");
    }

    [Fact]
    public async Task RunOnce_FailedFlushDropsExpiredWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var (sut, delivery, _, _, buffer) = BuildSut(processStart: now);
        var payload = new Dictionary<string, object?> { ["status"] = "failed" };
        buffer.Enqueue(new PendingGenerationWrite("gen-old", payload, now.AddHours(-2)));
        delivery.TryPatchOnceAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        buffer.Count.Should().Be(0, "writes past the age cap are dropped — the stale sweep owns the row");
    }

    [Fact]
    public async Task RunOnce_DeliveryThrows_DoesNotPropagateFromSafeWrapper()
    {
        var (sut, delivery, _, _, _) = BuildSut();
        delivery.GetStaleNonTerminalAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<GenerationSnapshot>>>(_ => throw new HttpRequestException("supabase down"));

        // RunOnceAsync itself propagates (so tests see real failures); the service's
        // periodic loop wraps it in RunOnceSafeAsync. Verify the raw call throws —
        // the safe wrapper is exercised via ExecuteAsync in integration.
        var act = () => sut.RunOnceAsync(StaleWindow, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
