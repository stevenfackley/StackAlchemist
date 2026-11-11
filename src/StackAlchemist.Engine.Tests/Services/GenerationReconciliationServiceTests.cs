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

    /// <summary>A clock that stands still until a test moves it, as a running process's does not.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
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
        BuildSut(
            DateTimeOffset? processStart = null, int maxAttempts = 3, IBillingStore? billing = null,
            IFailedBuildRefunder? refunder = null, FixedTimeProvider? clock = null)
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
            clock ?? new FixedTimeProvider(processStart ?? DateTimeOffset.UtcNow),
            NullLogger<GenerationReconciliationService>.Instance,
            billing,
            refunder);
        return (sut, delivery, orchestrator, registry, buffer);
    }

    private static GenerationSnapshot StaleRow(
        string id = "gen-stale",
        string status = "generating_code",
        int attemptCount = 0,
        DateTimeOffset? updatedAt = null,
        int tier = 2) =>
        new(id, status, tier, "advanced", "build an app", ProjectType.DotNetNextJs,
            new GenerationSchema(), null, attemptCount,
            updatedAt ?? DateTimeOffset.UtcNow.AddMinutes(-30));

    // ── #425: a row whose stored JSON no longer parses is failed, not skipped forever ──

    private static GenerationSnapshot UnreadableRow(
        string id = "gen-unreadable",
        string status = "generating_code",
        int attemptCount = 0,
        DateTimeOffset? updatedAt = null,
        int tier = 0) =>
        new(id, status, tier, null, null, null, null, null, attemptCount,
            updatedAt ?? DateTimeOffset.UtcNow.AddMinutes(-30))
        {
            IsUnreadable = true,
        };

    [Theory]
    [InlineData("pending")]
    [InlineData("generating_code")]
    [InlineData("building")]
    public async Task RunOnce_UnreadableRow_IsFailedAsInternal_NeverRequeued(string status)
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut();
        var row = UnreadableRow(status: status);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow,
            Arg.Is<string>(m => m.Contains("could not be read")),
            ErrorCategorizer.Internal,
            Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_UnreadableRow_DoesNotStopTheSweepForOtherRows()
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut();
        var unreadable = UnreadableRow();
        var healthy = StaleRow(id: "gen-healthy");
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([unreadable, healthy]);
        delivery.TryClaimForRequeueAsync(healthy, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.Received(1).TryFailStaleRowAsync(
            unreadable, StaleWindow, Arg.Any<string>(), ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-healthy"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_UnreadableRowInFlightInThisProcess_IsLeftAlone()
    {
        var (sut, delivery, _, registry, _) = BuildSut();
        var row = UnreadableRow();
        registry.Add(row.Id);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_UnreadableUnpaidPaidTierRow_StillWaitsOnItsCheckout()
    {
        // The payment gate runs first: failing an unpaid row would leave a checkout the user can
        // still complete pointing at a failed generation.
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unreadable", Arg.Any<CancellationToken>())
            .Returns((EligibleTransaction?)null);
        var (sut, delivery, _, _, _) = BuildSut(billing: billing);
        var row = UnreadableRow(status: "pending", tier: 2, updatedAt: DateTimeOffset.UtcNow.AddHours(-2));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_UnreadablePaidTierRow_IsFailedOncePaid()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unreadable", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_1"));
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = UnreadableRow(status: "pending", tier: 2);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow, Arg.Any<string>(), ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
    }

    // ── #421: unpaid paid-tier checkouts are never built ─────────────────────────

    [Fact]
    public async Task RunOnce_UnpaidPaidTierPendingRow_IsLeftAloneWhileTheCheckoutCanStillComplete()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unpaid", Arg.Any<CancellationToken>())
            .Returns((EligibleTransaction?)null);
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-unpaid", status: "pending", updatedAt: DateTimeOffset.UtcNow.AddHours(-2));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_UnpaidPaidTierPendingRow_FailsOnceItsCheckoutCanNoLongerBePaid()
    {
        var billing = Substitute.For<IBillingStore>();
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-abandoned", status: "pending", updatedAt: DateTimeOffset.UtcNow.AddHours(-26));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow, "Checkout was not completed.", ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_PaidPaidTierPendingRow_IsRequeued()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-paid", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_1"));
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-paid", status: "pending");
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-paid" && r.Tier == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_PendingSparkRow_NeedsNoPayment()
    {
        var billing = Substitute.For<IBillingStore>();
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-spark", status: "pending", tier: 0);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await billing.DidNotReceive().FindCompletedTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-spark" && r.Tier == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_PaymentLookupThrows_SkipsTheRowRatherThanBuildingItUnpaid()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unknown", Arg.Any<CancellationToken>())
            .Returns<EligibleTransaction?>(_ => throw new InvalidOperationException("db down"));
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-unknown", status: "pending");
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_PaidTierPendingRowWithoutABillingStore_IsNeverBuilt()
    {
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: null);
        var row = StaleRow(id: "gen-nostore", status: "pending");
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

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

    [Theory]
    [InlineData("building")]
    [InlineData("packing")]
    [InlineData("uploading")]
    public async Task RunOnce_LateStageRowCutOffByARestart_IsRunAgainFromTheTop(string status)
    {
        // Row last updated BEFORE this process started: a restart (every merge deploys prod) cut
        // the job off. The temp directory is gone but the row has the prompt and schema, so the
        // build runs again. Failing it would refund a sale the customer still wants.
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: status, updatedAt: processStart.AddMinutes(-30));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == row.Id && r.Tier == row.Tier && r.Schema == row.Schema),
            Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_LateStageRowCutOffByARestart_WhosePaymentIsNoLongerCompleted_IsFailedNotRebuilt()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((EligibleTransaction?)null);
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart, billing: billing);
        var row = StaleRow(status: "building", updatedAt: processStart.AddMinutes(-30));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow, Arg.Is<string>(m => m.Contains("restarted")), ErrorCategorizer.Internal,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_SchemaExtractionCutOffByARestart_FailsWithRestartMessage()
    {
        // extracting_schema is driven by a browser request this sweep cannot replay.
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: "extracting_schema", updatedAt: processStart.AddMinutes(-30), tier: 0);
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
        // Row updated AFTER this process started → it stalled while the engine was alive. The
        // worker dropped it with an outcome this sweep cannot know, so it is not run again.
        var processStart = DateTimeOffset.UtcNow.AddHours(-2);
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: "uploading", updatedAt: processStart.AddHours(1));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>())
            .Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
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
            .Returns<Task<IReadOnlyList<GenerationSnapshot>>>(_ => throw new HttpRequestException("store down"));

        // RunOnceAsync itself propagates (so tests see real failures); the service's
        // periodic loop wraps it in RunOnceSafeAsync. Verify the raw call throws —
        // the safe wrapper is exercised via ExecuteAsync in integration.
        var act = () => sut.RunOnceAsync(StaleWindow, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── A paid build the sweep ends is refunded ──────────────────────────────────

    private static void FailStaleWins(IDeliveryService delivery, GenerationSnapshot row, bool won = true) =>
        delivery.TryFailStaleRowAsync(
                row, StaleWindow, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(won);

    [Theory]
    [InlineData("building", 3, -30)]        // cut off by a restart, and out of attempts
    [InlineData("generating_code", 3, -30)] // out of attempts
    [InlineData("uploading", 0, 30)]        // dropped in a late stage while this process was alive
    public async Task RunOnce_PaidRowTheSweepFails_IsRefunded(string status, int attemptCount, int updatedMinutesFromProcessStart)
    {
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var processStart = DateTimeOffset.UtcNow.AddHours(-2);
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart, refunder: refunder);
        var row = StaleRow(
            id: "gen-paid-dead", status: status, attemptCount: attemptCount, tier: 2,
            updatedAt: processStart.AddMinutes(updatedMinutesFromProcessStart));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            delivery.TryFailStaleRowAsync(
                row, StaleWindow, Arg.Any<string>(), ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
            refunder.TryRefundAsync("gen-paid-dead", 2, RefundReason.BuildCouldNotFinish, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RunOnce_RefundDoesNotRideTheSweepsToken()
    {
        // The row is failed by the time the refund runs. On the sweep's token, a host that
        // starts stopping in between cancels the refund, and nothing ever tries it again.
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, _, _, _) = BuildSut(refunder: refunder);
        var row = StaleRow(status: "building", attemptCount: 3, tier: 2);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);
        using var sweep = new CancellationTokenSource();

        await sut.RunOnceAsync(StaleWindow, sweep.Token);

        await refunder.Received(1).TryRefundAsync(row.Id, 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);
    }

    [Fact]
    public async Task RunOnce_RowWithATerminalWriteStillBuffered_IsLeftAlone()
    {
        // The build finished and uploaded during a store outage; its success write is buffered
        // and this tick's flush could not land it. Failing the row here would be overwritten by
        // that write on the next tick, after the refund had already gone out.
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, orchestrator, _, buffer) = BuildSut(refunder: refunder);
        var row = StaleRow(id: "gen-delivered", status: "uploading", tier: 2);
        buffer.Enqueue(new PendingGenerationWrite(
            "gen-delivered", new Dictionary<string, object?> { ["status"] = "success" }, DateTimeOffset.UtcNow));
        delivery.TryPatchOnceAsync("gen-delivered", Arg.Any<Dictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        buffer.HasPendingFor("gen-delivered").Should().BeTrue("the failed flush put the write back");
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnce_PaidUnreadableRow_IsRefunded_BecauseARetryCannotHelpIt()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unreadable", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_1"));
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, _, _, _) = BuildSut(billing: billing, refunder: refunder);
        var row = UnreadableRow(status: "generating_code", tier: 2);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await refunder.Received(1).TryRefundAsync(
            "gen-unreadable", 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);
    }

    // ── The process-start time is fixed at construction; "now" moves on ─────────

    [Fact]
    public async Task RunOnce_LateStageRowDroppedByThisProcess_IsStillFailedHoursLater()
    {
        // The row was touched an hour after this process started, then abandoned. Two hours on
        // it is older than "now" by a wide margin, and it must still not be run again: what
        // matters is whether THIS process was alive when it was last touched, and that instant
        // is taken once, at construction. A comparison against the current time would rebuild
        // every late-stage row there is.
        var processStart = DateTimeOffset.UtcNow.AddHours(-3);
        var clock = new FixedTimeProvider(processStart);
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, orchestrator, _, _) = BuildSut(refunder: refunder, clock: clock);
        var row = StaleRow(status: "building", updatedAt: processStart.AddHours(1));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        clock.Advance(TimeSpan.FromHours(3));
        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await refunder.Received(1).TryRefundAsync(row.Id, 2, RefundReason.BuildCouldNotFinish, CancellationToken.None);
    }

    [Fact]
    public async Task RunOnce_LateStageRowCutOffByARestart_IsStillRunAgainHoursLater()
    {
        var processStart = DateTimeOffset.UtcNow.AddHours(-3);
        var clock = new FixedTimeProvider(processStart);
        var (sut, delivery, orchestrator, _, _) = BuildSut(clock: clock);
        var row = StaleRow(status: "building", updatedAt: processStart.AddMinutes(-5));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        clock.Advance(TimeSpan.FromHours(3));
        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == row.Id), Arg.Any<CancellationToken>());
    }

    // ── The boot pass: restart at once what the restart itself cut off ──────────

    [Theory]
    [InlineData("generating_code")]
    [InlineData("generating")]
    [InlineData("building")]
    [InlineData("packing")]
    [InlineData("uploading")]
    public async Task BootPass_RowARunWasWorkingOnWhenTheProcessStopped_IsRestartedWithNoStaleWindow(string status)
    {
        // Without this the customer watches a frozen page for the 15-minute stale window after
        // every deploy before the reconciler picks the build back up.
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: status, updatedAt: processStart.AddSeconds(-20));
        delivery.GetStaleNonTerminalAsync(TimeSpan.Zero, Arg.Any<CancellationToken>()).Returns([row]);
        delivery.TryClaimForRequeueAsync(row, TimeSpan.Zero, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(TimeSpan.Zero, CancellationToken.None, restartOrphansOnly: true);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == row.Id), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("pending")]            // a checkout, or a request still on its way in: not ours to start
    [InlineData("extracting_schema")]  // driven by a browser request
    public async Task BootPass_RowNoRunWasWorkingOn_IsLeftForTheRegularSweep(string status)
    {
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: status, updatedAt: processStart.AddSeconds(-20), tier: 0);
        delivery.GetStaleNonTerminalAsync(TimeSpan.Zero, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(TimeSpan.Zero, CancellationToken.None, restartOrphansOnly: true);

        delivery.ReceivedCalls().Should().ContainSingle("only the listing: nothing is claimed, failed or refunded");
        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootPass_RowTouchedByThisProcess_IsLeftAlone()
    {
        var processStart = DateTimeOffset.UtcNow.AddMinutes(-1);
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart);
        var row = StaleRow(status: "building", updatedAt: processStart.AddSeconds(30));
        delivery.GetStaleNonTerminalAsync(TimeSpan.Zero, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(TimeSpan.Zero, CancellationToken.None, restartOrphansOnly: true);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(3, true)]   // out of restart budget
    [InlineData(0, false)]  // payment no longer completed
    public async Task BootPass_NeverFailsOrRefundsARow_ThatIsTheRegularSweepsJob(int attemptCount, bool paid)
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(paid ? new EligibleTransaction("tx-1", "pi_1") : null);
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var processStart = DateTimeOffset.UtcNow;
        var (sut, delivery, orchestrator, _, _) = BuildSut(processStart, billing: billing, refunder: refunder);
        var row = StaleRow(status: "building", attemptCount: attemptCount, updatedAt: processStart.AddSeconds(-20));
        var unreadable = UnreadableRow(id: "gen-unreadable-boot", status: "building", tier: 2, updatedAt: processStart.AddSeconds(-20));
        delivery.GetStaleNonTerminalAsync(TimeSpan.Zero, Arg.Any<CancellationToken>()).Returns([row, unreadable]);

        await sut.RunOnceAsync(TimeSpan.Zero, CancellationToken.None, restartOrphansOnly: true);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    // ── A started paid row is rebuilt only while its payment is still completed ──

    [Fact]
    public async Task RunOnce_StartedPaidRowStillPaid_IsRequeued()
    {
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-started", Arg.Any<CancellationToken>())
            .Returns(new EligibleTransaction("tx-1", "pi_1"));
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-started", status: "generating_code");
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        delivery.TryClaimForRequeueAsync(row, StaleWindow, Arg.Any<CancellationToken>()).Returns(true);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-started"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("generating_code")]
    [InlineData("generating")]
    public async Task RunOnce_StartedPaidRowWhosePaymentIsNoLongerCompleted_IsFailedNotRebuilt(string status)
    {
        // Refunded (by the Engine after a failure whose failed write was lost, or from the Stripe
        // dashboard) while the row sat in an in-progress status. Rebuilding it delivers a build
        // nobody is paying for.
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-refunded", Arg.Any<CancellationToken>())
            .Returns((EligibleTransaction?)null);
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing);
        var row = StaleRow(id: "gen-refunded", status: status);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.DidNotReceive().TryClaimForRequeueAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow, Arg.Any<string>(), ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("generating_code")]
    [InlineData("building")]
    public async Task RunOnce_StartedPaidRow_PaymentLookupThrows_IsSkippedThisTick(string status)
    {
        // Nothing is known about the payment, so the row is neither rebuilt, failed nor refunded.
        var billing = Substitute.For<IBillingStore>();
        billing.FindCompletedTransactionAsync("gen-unknown", Arg.Any<CancellationToken>())
            .Returns<EligibleTransaction?>(_ => throw new InvalidOperationException("db down"));
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, orchestrator, _, _) = BuildSut(billing: billing, refunder: refunder);
        var row = StaleRow(id: "gen-unknown", status: status);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
        await delivery.DidNotReceive().TryFailStaleRowAsync(
            Arg.Any<GenerationSnapshot>(), Arg.Any<TimeSpan>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    [Fact]
    public async Task RunOnce_PaidRowThatMovedOnBeforeTheSweepCouldFailIt_IsNotRefunded()
    {
        // The conditional write lost: the row progressed (or another sweep took it). Refunding
        // on a lost write would refund a build that may be about to deliver.
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, _, _, _) = BuildSut(refunder: refunder);
        var row = StaleRow(status: "building", attemptCount: 3, tier: 2);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row, won: false);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    [Fact]
    public async Task RunOnce_FreeRowTheSweepFails_IsNotRefunded()
    {
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, _, _, _) = BuildSut(refunder: refunder);
        var row = StaleRow(status: "generating_code", attemptCount: 3, tier: 0);
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }

    [Fact]
    public async Task RunOnce_AbandonedCheckout_IsFailedButNeverSentToTheRefunder()
    {
        // Nothing was paid. The refund service would find no transaction, but the sweep should
        // not ask: an abandoned checkout is not a failed build.
        var refunder = Substitute.For<IFailedBuildRefunder>();
        var (sut, delivery, _, _, _) = BuildSut(billing: Substitute.For<IBillingStore>(), refunder: refunder);
        var row = StaleRow(id: "gen-abandoned", status: "pending", updatedAt: DateTimeOffset.UtcNow.AddHours(-26));
        delivery.GetStaleNonTerminalAsync(StaleWindow, Arg.Any<CancellationToken>()).Returns([row]);
        FailStaleWins(delivery, row);

        await sut.RunOnceAsync(StaleWindow, CancellationToken.None);

        await delivery.Received(1).TryFailStaleRowAsync(
            row, StaleWindow, "Checkout was not completed.", ErrorCategorizer.Internal, Arg.Any<CancellationToken>());
        await refunder.DidNotReceiveWithAnyArgs().TryRefundAsync(default!, default, default, default);
    }
}
