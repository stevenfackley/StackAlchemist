using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using StackAlchemist.Engine.Tests.Integration;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// <see cref="PostgresBillingStore"/> against a real Postgres carrying the web app's own migrations
/// (<see cref="PostgresFixture"/>), including the <c>process_checkout_completed</c> function. The
/// container is shared by the collection, so every Stripe id is unique per test and every assertion
/// reads only the rows that test seeded.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PostgresBillingStoreTests(PostgresFixture fx)
{
    private const string CheckoutCompleted = "checkout.session.completed";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private PostgresBillingStore Sut() => new(fx.DataSource, NullLogger<PostgresBillingStore>.Instance);

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// A tier-0 (try-before-buy) row, the state a checkout upgrades. Its own owner each time: the
    /// free-tier quota trigger rejects an ownerless tier-0 row and caps each owner per month.
    /// </summary>
    private Task<Guid> SeedTryBeforeBuyAsync() => fx.SeedGenerationAsync(userId: Guid.NewGuid(), tier: 0);

    private static NpgsqlDataSource Unreachable() =>
        new NpgsqlDataSourceBuilder("Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1").Build();

    /// <summary>A transaction for <paramref name="generationId"/>; an empty payment intent is stored as NULL.</summary>
    private Task<Guid> SeedTransactionAsync(
        Guid generationId, string status, string paymentIntent, string sessionId, string chargeId, TimeSpan age) =>
        fx.ReadAsync<Guid>(
            "insert into stackalchemist.transactions " +
            "(generation_id, tier, amount, status, stripe_payment_intent, stripe_session_id, stripe_charge_id, created_at) " +
            "values ($1, 2, 59900, $2, nullif($3, ''), $4, $5, now() - $6::interval) returning id",
            generationId, status, paymentIntent, sessionId, chargeId, age);

    private Task<Guid> SeedTransactionAsync(Guid generationId, string status, TimeSpan age) =>
        SeedTransactionAsync(generationId, status, Unique("pi"), Unique("cs"), Unique("ch"), age);

    private Task<string?> TransactionStatus(Guid id) =>
        fx.ReadAsync<string>("select status from stackalchemist.transactions where id = $1", id);

    private Task<long> TransactionCount(string sessionId) =>
        fx.ReadAsync<long>("select count(*) from stackalchemist.transactions where stripe_session_id = $1", sessionId);

    private Task<long> EventCount(string eventId) =>
        fx.ReadAsync<long>("select count(*) from stackalchemist.stripe_events where id = $1", eventId);

    [Fact]
    public async Task TryRecordEventAsync_records_a_new_event_once_then_reports_the_duplicate()
    {
        if (!fx.Available) return;
        var evt = Unique("evt");

        (await Sut().TryRecordEventAsync(evt, "charge.refunded", Ct)).Should().Be(EventRecord.New);
        (await Sut().TryRecordEventAsync(evt, "charge.refunded", Ct)).Should().Be(EventRecord.Duplicate);

        (await fx.ReadAsync<string>("select type from stackalchemist.stripe_events where id = $1", evt))
            .Should().Be("charge.refunded");
    }

    [Fact]
    public async Task ProcessCheckoutCompletedAsync_applies_once_and_a_replay_changes_nothing()
    {
        if (!fx.Available) return;
        var gen = await SeedTryBeforeBuyAsync();
        await fx.ExecuteAsync("update stackalchemist.generations set prompt = 'p' where id = $1", gen);
        var (evt, cs, pi) = (Unique("evt_c"), Unique("cs"), Unique("pi"));

        var first = await Sut().ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, cs, pi, gen.ToString(), 2, 59900, Ct);

        first.IsNew.Should().BeTrue();
        first.Mode.Should().Be("simple");
        first.Prompt.Should().Be("p");
        first.ProjectType.Should().Be(ProjectType.DotNetNextJs);
        first.Schema.Should().BeNull();
        first.Personalization.Should().BeNull();
        (await fx.ReadAsync<int>("select tier from stackalchemist.generations where id = $1", gen)).Should().Be(2);

        // Move the row on, as a later event would: a replay that re-ran the upsert would put it back.
        await fx.ExecuteAsync("update stackalchemist.transactions set status = 'refunded' where stripe_session_id = $1", cs);

        var replay = await Sut().ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, cs, pi, gen.ToString(), 2, 59900, Ct);

        replay.Should().Be(new CheckoutOutcome(false, null, null, null, null, null));
        (await TransactionCount(cs)).Should().Be(1, "the replay is recognised by its event id and inserts nothing");
        (await fx.ReadAsync<string>(
                "select status || '|' || tier || '|' || amount || '|' || generation_id || '|' || last_stripe_event_id || '|' || stripe_payment_intent " +
                "from stackalchemist.transactions where stripe_session_id = $1", cs))
            .Should().Be($"refunded|2|59900|{gen}|{evt}|{pi}", "the first call wrote the row and the replay left it alone");
        (await EventCount(evt)).Should().Be(1);
    }

    [Fact]
    public async Task ProcessCheckoutCompletedAsync_writes_a_completed_transaction()
    {
        if (!fx.Available) return;
        var gen = await SeedTryBeforeBuyAsync();
        var (evt, cs, pi) = (Unique("evt_c"), Unique("cs"), Unique("pi"));

        await Sut().ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, cs, pi, gen.ToString(), 2, 59900, Ct);

        (await TransactionCount(cs)).Should().Be(1);
        (await fx.ReadAsync<string>(
                "select status || '|' || tier || '|' || amount || '|' || generation_id || '|' || last_stripe_event_id " +
                "from stackalchemist.transactions where stripe_session_id = $1", cs))
            .Should().Be($"completed|2|59900|{gen}|{evt}");
    }

    [Fact]
    public async Task ProcessCheckoutCompletedAsync_reads_back_schema_and_personalization_in_either_casing()
    {
        if (!fx.Available) return;
        var gen = await SeedTryBeforeBuyAsync();
        // schema_json lowercase (as the Engine writes it), personalization_json camelCase (as the web does).
        await fx.ExecuteAsync(
            "update stackalchemist.generations set mode = 'advanced', project_type = 'PythonReact', " +
            "schema_json = $1::jsonb, personalization_json = $2::jsonb where id = $3",
            """{"entities":[{"name":"Widget","fields":[{"name":"id","type":"uuid","pk":true}]}]}""",
            """{"businessDescription":"Bike shop","projectName":"Spokes"}""",
            gen);

        var outcome = await Sut().ProcessCheckoutCompletedAsync(
            Unique("evt_c"), CheckoutCompleted, Unique("cs"), null, gen.ToString(), 3, 99900, Ct);

        outcome.IsNew.Should().BeTrue();
        outcome.Mode.Should().Be("advanced");
        outcome.Prompt.Should().BeNull();
        outcome.ProjectType.Should().Be(ProjectType.PythonReact);
        outcome.Schema!.Entities.Should().ContainSingle().Which.Fields.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Name = "id", Type = "uuid", Pk = true });
        outcome.Personalization!.BusinessDescription.Should().Be("Bike shop");
        outcome.Personalization.ProjectName.Should().Be("Spokes");
    }

    [Fact]
    public async Task ProcessCheckoutCompletedAsync_rejects_a_generation_id_that_is_not_a_uuid()
    {
        if (!fx.Available) return;
        var evt = Unique("evt_c");

        await Sut().Invoking(s => s.ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, Unique("cs"), null, "gen-77", 2, 59900, Ct))
            .Should().ThrowAsync<ArgumentException>();

        (await EventCount(evt)).Should().Be(0, "nothing was sent, so Stripe's redelivery starts clean");
    }

    [Fact]
    public async Task ProcessCheckoutCompletedAsync_surfaces_a_rejected_call_without_the_server_message()
    {
        if (!fx.Available) return;
        var gen = await SeedTryBeforeBuyAsync();
        var evt = Unique("evt_c");

        // Tier 7 breaks generations_tier_check inside the function, so the whole call rolls back.
        var thrown = await Sut().Invoking(s => s.ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, Unique("cs"), null, gen.ToString(), 7, 1, Ct))
            .Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.InnerException.Should().BeNull("the server's message can quote values, so it is not carried");
        thrown.Which.Message.Should().Contain("SQLSTATE 23514").And.Contain("generations_tier_check");
        (await EventCount(evt)).Should().Be(0, "the event insert rolled back with the rest");
    }

    [Fact]
    public async Task UpdateTransactionsAsync_overwrites_status_by_payment_intent_and_returns_the_generation()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync(status: "building");
        var pi = Unique("pi");
        var tx = await SeedTransactionAsync(gen, "completed", pi, Unique("cs"), Unique("ch"), TimeSpan.Zero);
        var bystander = await SeedTransactionAsync(gen, "completed", TimeSpan.Zero);
        var evt = Unique("evt_r");

        var generationId = await Sut().UpdateTransactionsAsync(
            TransactionKey.StripePaymentIntent, pi, "refunded", evt, returnGenerationId: true, Ct);

        generationId.Should().Be(gen.ToString());
        (await fx.ReadAsync<string>("select status || '|' || last_stripe_event_id from stackalchemist.transactions where id = $1", tx))
            .Should().Be($"refunded|{evt}");
        (await TransactionStatus(bystander)).Should().Be("completed");
    }

    [Fact]
    public async Task UpdateTransactionsAsync_matches_by_session_and_by_charge_too()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        var (cs, ch) = (Unique("cs"), Unique("ch"));
        var tx = await SeedTransactionAsync(gen, "pending", Unique("pi"), cs, ch, TimeSpan.Zero);

        (await Sut().UpdateTransactionsAsync(TransactionKey.StripeSessionId, cs, "failed", Unique("evt"), returnGenerationId: false, Ct))
            .Should().BeNull("the generation id was not asked for");
        (await TransactionStatus(tx)).Should().Be("failed");

        (await Sut().UpdateTransactionsAsync(TransactionKey.StripeChargeId, ch, "disputed", Unique("evt"), returnGenerationId: true, Ct))
            .Should().Be(gen.ToString());
        (await TransactionStatus(tx)).Should().Be("disputed");

        (await Sut().UpdateTransactionsAsync(TransactionKey.StripeChargeId, Unique("ch"), "disputed", Unique("evt"), returnGenerationId: true, Ct))
            .Should().BeNull("no row matched");
    }

    [Fact]
    public async Task UpdateTransactionsAsync_swallows_a_rejected_status()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        var pi = Unique("pi");
        var tx = await SeedTransactionAsync(gen, "completed", pi, Unique("cs"), Unique("ch"), TimeSpan.Zero);

        (await Sut().UpdateTransactionsAsync(TransactionKey.StripePaymentIntent, pi, "bogus", Unique("evt"), returnGenerationId: true, Ct))
            .Should().BeNull();
        (await TransactionStatus(tx)).Should().Be("completed");
    }

    [Fact]
    public async Task CancelUndeliveredGenerationAsync_fails_an_undelivered_generation_and_leaves_a_delivered_one()
    {
        if (!fx.Available) return;
        var undelivered = await fx.SeedGenerationAsync(status: "building");
        var delivered = await fx.SeedGenerationAsync(status: "success");

        await Sut().CancelUndeliveredGenerationAsync(undelivered.ToString(), "Refunded by Stripe", Ct);
        await Sut().CancelUndeliveredGenerationAsync(delivered.ToString(), "Refunded by Stripe", Ct);
        await Sut().CancelUndeliveredGenerationAsync("not-a-uuid", "Refunded by Stripe", Ct);

        (await fx.ReadAsync<string>("select status || '|' || error_message from stackalchemist.generations where id = $1", undelivered))
            .Should().Be("failed|Refunded by Stripe");
        (await fx.ReadAsync<string>("select status from stackalchemist.generations where id = $1", delivered)).Should().Be("success");
        (await fx.ReadAsync<string>("select error_message from stackalchemist.generations where id = $1", delivered)).Should().BeNull();
    }

    [Fact]
    public async Task FindCompletedTransactionAsync_returns_the_newest_completed_transaction()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        await SeedTransactionAsync(gen, "completed", TimeSpan.FromHours(2));
        var newerPi = Unique("pi");
        var newer = await SeedTransactionAsync(gen, "completed", newerPi, Unique("cs"), Unique("ch"), TimeSpan.FromHours(1));
        await SeedTransactionAsync(gen, "refunded", TimeSpan.Zero);

        (await Sut().FindCompletedTransactionAsync(gen.ToString(), Ct))
            .Should().Be(new EligibleTransaction(newer.ToString(), newerPi));
    }

    [Fact]
    public async Task FindCompletedTransactionAsync_finds_nothing_without_a_payment_intent_or_a_uuid()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        await SeedTransactionAsync(gen, "completed", "", Unique("cs"), Unique("ch"), TimeSpan.Zero);

        (await Sut().FindCompletedTransactionAsync(gen.ToString(), Ct)).Should().BeNull();
        (await Sut().FindCompletedTransactionAsync(Guid.NewGuid().ToString(), Ct)).Should().BeNull();
        (await Sut().FindCompletedTransactionAsync("gen-42", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task TryClaimForRefundAsync_claims_once_and_a_revert_makes_it_claimable_again()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        var tx = await SeedTransactionAsync(gen, "completed", TimeSpan.Zero);
        var sut = Sut();

        (await sut.TryClaimForRefundAsync(tx.ToString(), Ct)).Should().BeTrue();
        (await sut.TryClaimForRefundAsync(tx.ToString(), Ct)).Should().BeFalse("the row is no longer 'completed'");
        (await TransactionStatus(tx)).Should().Be("refund_pending");
        (await sut.FindCompletedTransactionAsync(gen.ToString(), Ct)).Should().BeNull();

        await sut.RevertRefundClaimAsync(tx.ToString(), Ct);

        (await TransactionStatus(tx)).Should().Be("completed");
        (await sut.TryClaimForRefundAsync(tx.ToString(), Ct)).Should().BeTrue();
        (await sut.TryClaimForRefundAsync("tx-not-a-uuid", Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task RevertRefundClaimAsync_only_reverts_a_refund_pending_row()
    {
        if (!fx.Available) return;
        var gen = await fx.SeedGenerationAsync();
        var refunded = await SeedTransactionAsync(gen, "refunded", TimeSpan.Zero);

        await Sut().RevertRefundClaimAsync(refunded.ToString(), Ct);

        (await TransactionStatus(refunded)).Should().Be("refunded");
    }

    [Fact]
    public async Task DeleteEventAsync_lets_the_same_checkout_event_replay_as_new_without_a_second_transaction()
    {
        if (!fx.Available) return;
        var gen = await SeedTryBeforeBuyAsync();
        var (evt, cs, pi) = (Unique("evt_c"), Unique("cs"), Unique("pi"));
        (await Sut().ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, cs, pi, gen.ToString(), 2, 59900, Ct)).IsNew.Should().BeTrue();

        await Sut().DeleteEventAsync(evt, Ct);

        (await EventCount(evt)).Should().Be(0);
        (await Sut().ProcessCheckoutCompletedAsync(evt, CheckoutCompleted, cs, pi, gen.ToString(), 2, 59900, Ct)).IsNew.Should().BeTrue();
        (await TransactionCount(cs)).Should().Be(1, "the upsert on stripe_session_id updates the existing row");
        (await EventCount(evt)).Should().Be(1);
    }

    [Fact]
    public async Task An_unreachable_database_is_Unavailable_for_the_idempotency_log_and_throws_for_the_checkout()
    {
        await using var dataSource = Unreachable();
        var sut = new PostgresBillingStore(dataSource, NullLogger<PostgresBillingStore>.Instance);

        (await sut.TryRecordEventAsync("evt_x", "charge.refunded", Ct)).Should().Be(EventRecord.Unavailable);
        await sut.Invoking(s => s.ProcessCheckoutCompletedAsync(
                "evt_x", CheckoutCompleted, "cs_x", null, Guid.NewGuid().ToString(), 2, 59900, Ct))
            .Should().ThrowAsync<NpgsqlException>();
    }

    [Fact]
    public async Task An_unreachable_database_throws_for_the_refund_reads_and_is_swallowed_by_the_writes()
    {
        await using var dataSource = Unreachable();
        var sut = new PostgresBillingStore(dataSource, NullLogger<PostgresBillingStore>.Instance);
        var id = Guid.NewGuid().ToString();

        await sut.Invoking(s => s.FindCompletedTransactionAsync(id, Ct)).Should().ThrowAsync<NpgsqlException>();
        await sut.Invoking(s => s.TryClaimForRefundAsync(id, Ct)).Should().ThrowAsync<NpgsqlException>();

        await sut.Invoking(s => s.DeleteEventAsync("evt_x", Ct)).Should().NotThrowAsync();
        (await sut.UpdateTransactionsAsync(TransactionKey.StripePaymentIntent, "pi_x", "refunded", "evt_x", returnGenerationId: true, Ct))
            .Should().BeNull();
        await sut.Invoking(s => s.CancelUndeliveredGenerationAsync(id, "Refunded by Stripe", Ct)).Should().NotThrowAsync();
        await sut.Invoking(s => s.RevertRefundClaimAsync(id, Ct)).Should().NotThrowAsync();
    }
}
