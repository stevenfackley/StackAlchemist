using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Engine.Tests.Integration;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// <see cref="PostgresDeliveryService"/> against a real Postgres carrying the web app's own
/// migrations (<see cref="PostgresFixture"/>). The container is shared by the collection, so every
/// test seeds and asserts on its own rows only.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PostgresDeliveryServiceTests(PostgresFixture fx)
{
    private static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private PostgresDeliveryService Sut() =>
        new(fx.DataSource, new PendingWriteBuffer(), NullLogger<PostgresDeliveryService>.Instance);

    private Task<T?> Column<T>(Guid id, string column) =>
        fx.ReadAsync<T>($"select {column} from stackalchemist.generations where id = $1", id);

    // The container's clock, not the host's: WSL2's drifts after a sleep.
    private async Task<DateTimeOffset> DatabaseNow() =>
        new(await fx.ReadAsync<DateTime>("select now()"));

    private static NpgsqlDataSource Unreachable() =>
        new NpgsqlDataSourceBuilder("Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1").Build();

    /// <summary>Records the EventId of every log call, so a test can count attempts and outcomes.</summary>
    private sealed class EventRecorder : ILogger<PostgresDeliveryService>
    {
        private readonly ConcurrentQueue<int> _events = new();

        public int Count(int eventId) => _events.Count(e => e == eventId);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _events.Enqueue(eventId.Id);
    }

    [Fact]
    public async Task UpdateStatusAsync_success_writes_status_download_url_and_completed_at()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "uploading");

        await Sut().UpdateStatusAsync(id.ToString(), GenerationState.Success, downloadUrl: "https://r2.example/x.zip");

        (await Column<string>(id, "status")).Should().Be("success");
        (await Column<string>(id, "download_url")).Should().Be("https://r2.example/x.zip");
        (await Column<bool>(id, "completed_at is not null")).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTokenUsageAsync_adds_to_the_running_totals()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync();
        var sut = Sut();

        await sut.UpdateTokenUsageAsync(id.ToString(), 10, 20, "claude-x", Ct);
        await sut.UpdateTokenUsageAsync(id.ToString(), 5, 5, "claude-x", Ct);

        (await Column<int>(id, "input_tokens")).Should().Be(15);
        (await Column<int>(id, "output_tokens")).Should().Be(25);
        (await Column<string>(id, "model_used")).Should().Be("claude-x");
    }

    [Fact]
    public async Task AppendBuildLogAsync_joins_chunks_with_newlines()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "building");
        var sut = Sut();

        await sut.AppendBuildLogAsync(id.ToString(), "a", Ct);
        await sut.AppendBuildLogAsync(id.ToString(), "b", Ct);

        (await Column<string>(id, "build_log")).Should().Be("a\nb");
    }

    [Fact]
    public async Task GetGenerationOwnerEmailAsync_joins_to_the_profile()
    {
        if (!fx.Available) return;
        var user = Guid.NewGuid();
        var owned = await fx.SeedGenerationAsync(userId: user);
        var anonymous = await fx.SeedGenerationAsync();
        var sut = Sut();

        (await sut.GetGenerationOwnerEmailAsync(owned.ToString(), Ct)).Should().Be($"{user}@example.test");
        (await sut.GetGenerationOwnerEmailAsync(anonymous.ToString(), Ct)).Should().BeNull();
        (await sut.GetGenerationOwnerEmailAsync(Guid.NewGuid().ToString(), Ct)).Should().BeNull();
        (await sut.GetGenerationOwnerEmailAsync("demo-not-a-uuid", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetGenerationCredentialAsync_returns_the_ciphertext_and_preferred_model()
    {
        if (!fx.Available) return;
        var user = Guid.NewGuid();
        var owned = await fx.SeedGenerationAsync(userId: user);
        await fx.ExecuteAsync(
            "update stackalchemist.profiles set api_key_override = $1, preferred_model = $2 where id = $3",
            "v1:salt:iv:ciphertext", "gpt-x", user);
        var anonymous = await fx.SeedGenerationAsync();
        var sut = Sut();

        (await sut.GetGenerationCredentialAsync(owned.ToString(), Ct))
            .Should().Be(new ProfileCredential("v1:salt:iv:ciphertext", "gpt-x"));
        (await sut.GetGenerationCredentialAsync(anonymous.ToString(), Ct)).Should().BeNull();
        (await sut.GetGenerationCredentialAsync("demo-not-a-uuid", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task TryBeginExtractionAsync_claims_only_pending_or_failed_rows()
    {
        if (!fx.Available) return;
        var pending = await fx.SeedGenerationAsync();
        var done = await fx.SeedGenerationAsync(status: "success");
        var failed = await fx.SeedGenerationAsync(status: "failed");
        await fx.ExecuteAsync(
            "update stackalchemist.generations set error_message = 'old', error_category = 'schema' where id = $1", failed);
        var sut = Sut();

        (await sut.TryBeginExtractionAsync(pending.ToString(), Ct)).Should().BeTrue();
        (await Column<string>(pending, "status")).Should().Be("extracting_schema");
        (await sut.TryBeginExtractionAsync(pending.ToString(), Ct)).Should().BeFalse("a double-submit loses the CAS");

        (await sut.TryBeginExtractionAsync(done.ToString(), Ct)).Should().BeFalse();
        (await Column<string>(done, "status")).Should().Be("success");

        (await sut.TryBeginExtractionAsync(failed.ToString(), Ct)).Should().BeTrue("failed is the user-retry entry state");
        (await Column<string>(failed, "status")).Should().Be("extracting_schema");
        (await Column<string>(failed, "error_message")).Should().BeNull();
        (await Column<string>(failed, "error_category")).Should().BeNull();

        (await sut.TryBeginExtractionAsync("demo-not-a-uuid", Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task GetGenerationSnapshotAsync_round_trips_every_field()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "pending", tier: 2, attempts: 1);
        // personalization_json is written by the web app, in its camelCase TypeScript shape.
        await fx.ExecuteAsync(
            "update stackalchemist.generations set prompt = $1, project_type = 'PythonReact', personalization_json = $2::jsonb where id = $3",
            "a CRM for dog groomers",
            """
            {"businessDescription":"Dog grooming","projectName":"Groomr","tagline":"Clean dogs",
             "colorScheme":{"id":"bold-saas","name":"Bold SaaS","primary":"#7C3AED","secondary":"#6D28D9",
                            "accent":"#A78BFA","background":"#0D0520","surface":"#1A0B33"},
             "domainContext":{"Customer":"a pet owner"},
             "featureFlags":{"authMethod":"cookie","softDelete":true,"auditTimestamps":false,
                             "includeSwagger":false,"includeDockerCompose":true}}
            """,
            id);
        var schema = new GenerationSchema
        {
            Entities =
            [
                new SchemaEntity
                {
                    Name = "Customer",
                    Fields =
                    [
                        new SchemaField { Name = "id", Type = "uuid", Pk = true },
                        new SchemaField { Name = "email", Type = "string", Nullable = true, Default = "none" },
                    ],
                },
            ],
            Relationships = [new SchemaRelationship { From = "Order", Type = "many-to-one", To = "Customer" }],
            Endpoints = [new SchemaEndpoint { Method = "GET", Path = "/customers", Entity = "Customer", Description = "List" }],
        };
        var sut = Sut();

        await sut.UpdateSchemaAsync(id.ToString(), schema, Ct);
        var snapshot = await sut.GetGenerationSnapshotAsync(id.ToString(), Ct);

        // The column carries the snake_case write shape the web app parses.
        (await fx.ReadAsync<string>(
                "select schema_json->'entities'->0->'fields'->0->>'pk' from stackalchemist.generations where id = $1", id))
            .Should().Be("true");
        snapshot.Should().NotBeNull();
        snapshot!.Id.Should().Be(id.ToString());
        snapshot.Status.Should().Be("pending");
        snapshot.Tier.Should().Be(2);
        snapshot.Mode.Should().Be("simple");
        snapshot.Prompt.Should().Be("a CRM for dog groomers");
        snapshot.ProjectType.Should().Be(ProjectType.PythonReact);
        snapshot.AttemptCount.Should().Be(1);
        snapshot.UpdatedAt.Should().BeCloseTo(await DatabaseNow(), TimeSpan.FromMinutes(1));
        snapshot.Schema.Should().BeEquivalentTo(schema);
        snapshot.Personalization.Should().NotBeNull();
        snapshot.Personalization!.BusinessDescription.Should().Be("Dog grooming");
        snapshot.Personalization.ProjectName.Should().Be("Groomr");
        snapshot.Personalization.ColorScheme!.Primary.Should().Be("#7C3AED");
        snapshot.Personalization.DomainContext.Should().Equal(new Dictionary<string, string> { ["Customer"] = "a pet owner" });
        snapshot.Personalization.FeatureFlags!.AuthMethod.Should().Be("cookie");
        snapshot.Personalization.FeatureFlags.AuditTimestamps.Should().BeFalse();

        (await sut.GetGenerationSnapshotAsync(Guid.NewGuid().ToString(), Ct)).Should().BeNull();
        (await sut.GetGenerationSnapshotAsync("demo-not-a-uuid", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetStaleNonTerminalAsync_lists_only_old_non_terminal_rows()
    {
        if (!fx.Available) return;
        var stale = await fx.SeedGenerationAsync(status: "building");
        await fx.SetUpdatedAtAsync(stale, OneHour);
        var fresh = await fx.SeedGenerationAsync(status: "building");
        var done = await fx.SeedGenerationAsync(status: "success");
        await fx.SetUpdatedAtAsync(done, OneHour);

        var rows = await Sut().GetStaleNonTerminalAsync(StaleWindow, Ct);

        rows.Select(r => r.Id).Should()
            .Contain(stale.ToString())
            .And.NotContain(fresh.ToString())
            .And.NotContain(done.ToString());
        var row = rows.Single(r => r.Id == stale.ToString());
        row.Status.Should().Be("building");
        row.UpdatedAt.Should().BeBefore(await DatabaseNow() - StaleWindow);
    }

    [Fact]
    public async Task GetStaleNonTerminalAsync_flags_a_row_whose_schema_does_not_fit_and_keeps_the_rest()
    {
        if (!fx.Available) return;
        // schema_json can come from the client; this one lacks SchemaField's required "type".
        var bad = await fx.SeedGenerationAsync(status: "building", attempts: 1);
        try
        {
            await fx.ExecuteAsync(
                "update stackalchemist.generations set schema_json = $1::jsonb where id = $2",
                """{"entities":[{"name":"x","fields":[{"name":"id"}]}]}""", bad);
            await fx.SetUpdatedAtAsync(bad, OneHour);
            var good = await fx.SeedGenerationAsync(status: "building");
            await fx.SetUpdatedAtAsync(good, OneHour);
            var log = new EventRecorder();
            var sut = new PostgresDeliveryService(fx.DataSource, new PendingWriteBuffer(), log);

            var rows = await sut.GetStaleNonTerminalAsync(StaleWindow, Ct);

            rows.Single(r => r.Id == good.ToString()).IsUnreadable.Should().BeFalse();
            var unreadable = rows.Single(r => r.Id == bad.ToString());
            unreadable.IsUnreadable.Should().BeTrue(
                "#425: a dropped row stayed stale and was skipped on every tick, forever");
            unreadable.Status.Should().Be("building");
            unreadable.AttemptCount.Should().Be(1);
            unreadable.Schema.Should().BeNull();
            unreadable.UpdatedAt.Should().BeBefore(await DatabaseNow() - StaleWindow);
            log.Count(512).Should().BeGreaterThanOrEqualTo(1, "the unreadable row is logged by id");
            log.Count(510).Should().Be(0, "the sweep itself did not fail");
        }
        finally
        {
            // Terminal even when an assertion above fails, so the unreadable row never leaks into
            // the other tests' sweeps on the shared container.
            await fx.ExecuteAsync("update stackalchemist.generations set status = 'failed' where id = $1", bad);
        }
    }

    [Fact]
    public async Task TryFailStaleRowAsync_fails_an_unreadable_row_from_its_flagged_snapshot()
    {
        if (!fx.Available) return;
        var bad = await fx.SeedGenerationAsync(status: "generating_code", attempts: 2);
        try
        {
            await fx.ExecuteAsync(
                "update stackalchemist.generations set schema_json = $1::jsonb where id = $2",
                """{"entities":[{"name":"x","fields":[{"name":"id"}]}]}""", bad);
            await fx.SetUpdatedAtAsync(bad, OneHour);
            var sut = Sut();
            var snapshot = (await sut.GetStaleNonTerminalAsync(StaleWindow, Ct)).Single(r => r.Id == bad.ToString());

            // The plain columns the flagged snapshot carries are exactly the compare-and-set filter.
            (await sut.TryFailStaleRowAsync(snapshot, StaleWindow, "unreadable", "schema", Ct)).Should().BeTrue();

            (await Column<string>(bad, "status")).Should().Be("failed");
            (await Column<string>(bad, "error_message")).Should().Be("unreadable");
            (await Column<string>(bad, "error_category")).Should().Be("schema");
            (await sut.GetStaleNonTerminalAsync(StaleWindow, Ct)).Should().NotContain(r => r.Id == bad.ToString(),
                "once failed, the row leaves the sweep");
        }
        finally
        {
            await fx.ExecuteAsync("update stackalchemist.generations set status = 'failed' where id = $1", bad);
        }
    }

    [Fact]
    public async Task TryClaimForRequeueAsync_wins_once_per_stale_snapshot()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "generating_code", attempts: 1);
        await fx.ExecuteAsync(
            "update stackalchemist.generations set error_message = 'boom', error_category = 'internal' where id = $1", id);
        await fx.SetUpdatedAtAsync(id, OneHour);
        var sut = Sut();
        var snapshot = (await sut.GetStaleNonTerminalAsync(StaleWindow, Ct)).Single(r => r.Id == id.ToString());

        (await sut.TryClaimForRequeueAsync(snapshot, StaleWindow, Ct)).Should().BeTrue();

        (await Column<string>(id, "status")).Should().Be("pending");
        (await Column<int>(id, "attempt_count")).Should().Be(2);
        (await Column<string>(id, "error_message")).Should().BeNull();
        (await Column<string>(id, "error_category")).Should().BeNull();

        (await sut.TryClaimForRequeueAsync(snapshot, StaleWindow, Ct)).Should().BeFalse("the first claim moved the row");
        (await Column<int>(id, "attempt_count")).Should().Be(2);
    }

    [Fact]
    public async Task TryFailStaleRowAsync_leaves_a_row_that_moved_and_fails_a_stale_one()
    {
        if (!fx.Available) return;
        var sut = Sut();

        var moved = await fx.SeedGenerationAsync(status: "building");
        await fx.SetUpdatedAtAsync(moved, OneHour);
        var movedSnapshot = (await sut.GetGenerationSnapshotAsync(moved.ToString(), Ct))!;
        await fx.ExecuteAsync("update stackalchemist.generations set status = 'packing' where id = $1", moved);

        (await sut.TryFailStaleRowAsync(movedSnapshot, StaleWindow, "stalled", "internal", Ct)).Should().BeFalse();
        (await Column<string>(moved, "status")).Should().Be("packing");

        var stale = await fx.SeedGenerationAsync(status: "building");
        await fx.SetUpdatedAtAsync(stale, OneHour);
        var staleSnapshot = (await sut.GetGenerationSnapshotAsync(stale.ToString(), Ct))!;

        (await sut.TryFailStaleRowAsync(staleSnapshot, StaleWindow, "stalled", "internal", Ct)).Should().BeTrue();
        (await Column<string>(stale, "status")).Should().Be("failed");
        (await Column<string>(stale, "error_message")).Should().Be("stalled");
        (await Column<string>(stale, "error_category")).Should().Be("internal");
        (await Column<bool>(stale, "completed_at is not null")).Should().BeTrue();
    }

    [Fact]
    public async Task TryFailStaleRowAsync_loses_to_progress_that_only_moved_updated_at()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "building");
        await fx.SetUpdatedAtAsync(id, OneHour);
        var sut = Sut();
        var snapshot = (await sut.GetStaleNonTerminalAsync(StaleWindow, Ct)).Single(r => r.Id == id.ToString());

        // Build output arriving: status and attempt_count stay put, only updated_at moves.
        await sut.AppendBuildLogAsync(id.ToString(), "still compiling", Ct);
        (await Column<string>(id, "status")).Should().Be(snapshot.Status);
        (await Column<int>(id, "attempt_count")).Should().Be(snapshot.AttemptCount);

        (await sut.TryFailStaleRowAsync(snapshot, StaleWindow, "stalled", "internal", Ct))
            .Should().BeFalse("the updated_at guard alone must reject a row that made progress");
        (await Column<string>(id, "status")).Should().Be("building");
    }

    [Fact]
    public async Task TryPatchOnceAsync_reports_whether_a_row_was_written()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "building");
        var sut = Sut();

        (await sut.TryPatchOnceAsync(id.ToString(), new() { ["status"] = "packing" }, Ct)).Should().BeTrue();
        (await Column<string>(id, "status")).Should().Be("packing");

        (await sut.TryPatchOnceAsync(Guid.NewGuid().ToString(), new() { ["status"] = "packing" }, Ct)).Should().BeFalse();
        (await sut.TryPatchOnceAsync(id.ToString(), new() { ["status"] = "not-a-status" }, Ct))
            .Should().BeFalse("a CHECK violation is caught and reported as not written");
        (await Column<string>(id, "status")).Should().Be("packing");

        await FluentActions.Awaiting(() => sut.TryPatchOnceAsync(id.ToString(), new() { ["build_log"] = "x" }, Ct))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Critical_write_to_an_unreachable_database_is_buffered_and_a_progress_ping_is_not()
    {
        // No container needed: this data source points at a port nothing listens on.
        await using var unreachable = Unreachable();
        var buffer = new PendingWriteBuffer();
        var sut = new PostgresDeliveryService(
            unreachable, buffer, NullLogger<PostgresDeliveryService>.Instance,
            criticalMaxAttempts: 2, criticalBaseDelay: TimeSpan.FromMilliseconds(10));
        var id = Guid.NewGuid().ToString();

        await sut.UpdateStatusAsync(id, GenerationState.Failed, errorMessage: "boom");
        buffer.Count.Should().Be(1);

        await sut.UpdateStatusAsync(id, GenerationState.Building);
        buffer.Count.Should().Be(1, "progress pings are disposable");

        await sut.UpdateStatusAsync("demo-not-a-uuid", GenerationState.Failed, errorMessage: "boom");
        buffer.Count.Should().Be(1, "an id that can never match a row is not worth re-flushing");

        buffer.TryDequeue(out var write).Should().BeTrue();
        write!.GenerationId.Should().Be(id);
        write.Payload.Should().Contain("status", "failed").And.Contain("error_message", "boom");
    }

    [Fact]
    public async Task Critical_write_retries_a_transient_failure_for_its_whole_budget()
    {
        await using var unreachable = Unreachable();
        var log = new EventRecorder();
        var buffer = new PendingWriteBuffer();
        var sut = new PostgresDeliveryService(
            unreachable, buffer, log, criticalMaxAttempts: 2, criticalBaseDelay: TimeSpan.FromMilliseconds(10));

        await sut.UpdateStatusAsync(Guid.NewGuid().ToString(), GenerationState.Success, downloadUrl: "https://r2.example/x.zip");

        log.Count(504).Should().Be(2, "a refused connection is transient, so every attempt is spent");
        log.Count(503).Should().Be(0);
        log.Count(508).Should().Be(1);
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public async Task Critical_write_rejected_by_a_check_constraint_fails_fast_and_is_buffered()
    {
        if (!fx.Available) return;
        var id = await fx.SeedGenerationAsync(status: "building");
        var log = new EventRecorder();
        var buffer = new PendingWriteBuffer();
        var sut = new PostgresDeliveryService(fx.DataSource, buffer, log, criticalBaseDelay: TimeSpan.FromSeconds(5));
        var clock = Stopwatch.StartNew();

        await sut.UpdateStatusAsync(id.ToString(), GenerationState.Failed, errorCategory: "bogus");

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "a CHECK violation cannot succeed on retry, so there is no backoff sleep");
        log.Count(503).Should().Be(1);
        log.Count(504).Should().Be(0);
        buffer.Count.Should().Be(1, "terminal writes are buffered whatever the failure");
        (await Column<string>(id, "status")).Should().Be("building");
    }
}
