using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// <see cref="IBillingStore"/> over Npgsql against the qavren-db <c>stackalchemist</c> schema: the
/// same statements <see cref="SupabaseBillingStore"/> sends through PostgREST, as parameterised SQL.
/// Registered instead of the Supabase store when DATABASE_URL is set.
/// <para>
/// Ids that must be uuids (generation and transaction ids) arrive as strings. One that is not a uuid
/// can never match a row, so the lookups and writes treat it as "not found" (null / false / no-op)
/// without a round trip; only <see cref="ProcessCheckoutCompletedAsync"/> rejects it, because a paid
/// checkout that silently matched nothing would lose the payment's effect.
/// </para>
/// <para>
/// Nothing a failure carries that could quote a value reaches the logs or the callers: the swallowing
/// methods log through <see cref="PostgresErrors.Redact"/>, and the throwing ones replace a
/// <see cref="PostgresException"/> or <see cref="JsonException"/> with an
/// <see cref="InvalidOperationException"/> naming only the SQLSTATE and constraint, or the JSON path.
/// </para>
/// </summary>
public sealed partial class PostgresBillingStore(
    NpgsqlDataSource dataSource,
    ILogger<PostgresBillingStore> logger) : IBillingStore
{
    /// <inheritdoc />
    public async Task<EventRecord> TryRecordEventAsync(string eventId, string eventType, CancellationToken ct)
    {
        try
        {
            await using var cmd = CreateCommand(
                "insert into stackalchemist.stripe_events (id, type) values ($1, $2) on conflict (id) do nothing",
                (eventId, NpgsqlDbType.Text), (eventType, NpgsqlDbType.Text));
            return await cmd.ExecuteNonQueryAsync(ct) == 1 ? EventRecord.New : EventRecord.Duplicate;
        }
        catch (Exception ex)
        {
            // Never throws, as the Supabase store: the caller turns Unavailable into a Stripe retry
            // rather than processing the event unlogged.
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogStripeEventRecordFailed(logger, loggable, eventId, reason);
            return EventRecord.Unavailable;
        }
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(string eventId, CancellationToken ct)
    {
        try
        {
            await using var cmd = CreateCommand(
                "delete from stackalchemist.stripe_events where id = $1",
                (eventId, NpgsqlDbType.Text));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // Manual-recovery breadcrumb: the event stays recorded, so the redelivery will report
            // "duplicate"; the generation row is the place to look.
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogCompensationDeleteFailed(logger, loggable, eventId, reason);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="generationId"/> is not a uuid.</exception>
    public async Task<CheckoutOutcome> ProcessCheckoutCompletedAsync(
        string eventId, string eventType, string sessionId, string? paymentIntentId,
        string generationId, int tier, long amount, CancellationToken ct)
    {
        // Thrown before any I/O, so nothing is recorded and the caller's Retry reaches a clean slate.
        if (!Guid.TryParse(generationId, out var id))
            throw new ArgumentException("The checkout's generation id is not a uuid.", nameof(generationId));

        try
        {
            await using var cmd = CreateCommand(
                "select is_new, mode, prompt, project_type, schema_json, personalization_json " +
                "from stackalchemist.process_checkout_completed($1, $2, $3, $4, $5, $6, $7)",
                (eventId, NpgsqlDbType.Text),
                (eventType, NpgsqlDbType.Text),
                (sessionId, NpgsqlDbType.Text),
                (paymentIntentId, NpgsqlDbType.Text),
                (id, NpgsqlDbType.Uuid),
                (tier, NpgsqlDbType.Integer),
                (amount, NpgsqlDbType.Bigint));
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("process_checkout_completed returned no rows.");

            if (reader.IsDBNull(0) || !reader.GetBoolean(0))
                return new CheckoutOutcome(false, null, null, null, null, null);

            return new CheckoutOutcome(
                true,
                NullableString(reader, 1),
                NullableString(reader, 2),
                Enum.TryParse<ProjectType>(NullableString(reader, 3), ignoreCase: true, out var projectType) ? projectType : null,
                Deserialize<GenerationSchema>(NullableString(reader, 4)),
                Deserialize<GenerationPersonalization>(NullableString(reader, 5)));
        }
        catch (Exception ex) when (ex is PostgresException or JsonException)
        {
            throw Sanitized("process_checkout_completed", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string?> UpdateTransactionsAsync(
        TransactionKey key, string value, string status, string eventId, bool returnGenerationId, CancellationToken ct)
    {
        // The column comes from this switch, never from input, so it is the only text spliced into the SQL.
        var column = key switch
        {
            TransactionKey.StripeSessionId => "stripe_session_id",
            TransactionKey.StripePaymentIntent => "stripe_payment_intent",
            TransactionKey.StripeChargeId => "stripe_charge_id",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown transaction key."),
        };

        try
        {
            await using var cmd = CreateCommand(
                "update stackalchemist.transactions set status = $1, last_stripe_event_id = $2, updated_at = now() " +
                $"where {column} = $3 returning generation_id",
                (status, NpgsqlDbType.Text), (eventId, NpgsqlDbType.Text), (value, NpgsqlDbType.Text));
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            string? generationId = null;
            while (await reader.ReadAsync(ct))
            {
                if (generationId is null && !reader.IsDBNull(0))
                    generationId = reader.GetGuid(0).ToString();
            }

            return returnGenerationId ? generationId : null;
        }
        catch (Exception ex)
        {
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogTxUpdateFailed(logger, loggable, column, status, reason);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task CancelUndeliveredGenerationAsync(string generationId, string reason, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
        {
            LogCancelSkippedNotUuid(logger, generationId);
            return;
        }

        try
        {
            // Only flip generations that haven't already been delivered to the user.
            await using var cmd = CreateCommand(
                "update stackalchemist.generations set status = 'failed', error_message = $2 " +
                "where id = $1 and status <> 'success'",
                (id, NpgsqlDbType.Uuid), (reason, NpgsqlDbType.Text));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            var (loggable, failure) = PostgresErrors.Redact(ex);
            LogGenerationCancelFailed(logger, loggable, generationId, failure);
        }
    }

    /// <inheritdoc />
    public async Task<EligibleTransaction?> FindCompletedTransactionAsync(string generationId, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
            return null;

        try
        {
            await using var cmd = CreateCommand(
                "select id, stripe_payment_intent from stackalchemist.transactions " +
                "where generation_id = $1 and status = 'completed' order by created_at desc limit 1",
                (id, NpgsqlDbType.Uuid));
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            var paymentIntent = NullableString(reader, 1);
            return string.IsNullOrWhiteSpace(paymentIntent)
                ? null
                : new EligibleTransaction(reader.GetGuid(0).ToString(), paymentIntent);
        }
        catch (PostgresException ex)
        {
            throw Sanitized("completed-transaction lookup", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimForRefundAsync(string transactionId, CancellationToken ct)
    {
        if (!Guid.TryParse(transactionId, out var id))
            return false;

        try
        {
            // Compare-and-set: Postgres serializes competing UPDATEs, and the loser's
            // status = 'completed' filter no longer matches, so it affects zero rows.
            await using var cmd = CreateCommand(
                "update stackalchemist.transactions set status = 'refund_pending', updated_at = now() " +
                "where id = $1 and status = 'completed'",
                (id, NpgsqlDbType.Uuid));
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }
        catch (PostgresException ex)
        {
            throw Sanitized("refund claim", ex);
        }
    }

    /// <inheritdoc />
    public async Task RevertRefundClaimAsync(string transactionId, CancellationToken ct)
    {
        if (!Guid.TryParse(transactionId, out var id))
            return;

        try
        {
            await using var cmd = CreateCommand(
                "update stackalchemist.transactions set status = 'completed', updated_at = now() " +
                "where id = $1 and status = 'refund_pending'",
                (id, NpgsqlDbType.Uuid));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogRevertClaimFailed(logger, loggable, transactionId, reason);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>A fresh command with positional (<c>$1..$n</c>) parameters, each bound as its given type.</summary>
    private NpgsqlCommand CreateCommand(string sql, params (object? Value, NpgsqlDbType Type)[] args)
    {
        var cmd = dataSource.CreateCommand(sql);
        foreach (var (value, type) in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value, NpgsqlDbType = type });
        return cmd;
    }

    private static string? NullableString(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, DeliveryJson.Read);

    /// <summary>
    /// The redacted stand-in for a server rejection or unreadable JSON: the callers log what they
    /// catch whole, so the original (whose message can quote a value) is not carried along.
    /// </summary>
    private static InvalidOperationException Sanitized(string operation, Exception ex) =>
        new($"{operation} failed ({PostgresErrors.Redact(ex).Reason})");

    // ── LoggerMessage source-gen (same EventIds as SupabaseBillingStore) ─────

    [LoggerMessage(EventId = 304, Level = LogLevel.Warning, Message = "Failed to record Stripe event {Id} for idempotency in Postgres ({Reason})")]
    private static partial void LogStripeEventRecordFailed(ILogger logger, Exception? ex, string id, string reason);

    [LoggerMessage(EventId = 306, Level = LogLevel.Error, Message = "Failed to update Postgres transactions (by {Column}, status={Status}) ({Reason})")]
    private static partial void LogTxUpdateFailed(ILogger logger, Exception? ex, string column, string status, string reason);

    [LoggerMessage(EventId = 307, Level = LogLevel.Error, Message = "Failed to cancel undelivered generation {Id} in Postgres ({Reason})")]
    private static partial void LogGenerationCancelFailed(ILogger logger, Exception? ex, string id, string reason);

    [LoggerMessage(EventId = 308, Level = LogLevel.Warning, Message = "Generation id {Id} is not a uuid — skipping the Postgres cancel")]
    private static partial void LogCancelSkippedNotUuid(ILogger logger, string id);

    [LoggerMessage(EventId = 312, Level = LogLevel.Error, Message = "MANUAL RECOVERY: compensating Postgres delete of stripe_event {EventId} failed ({Reason}) — redelivery will report duplicate; check the generation row")]
    private static partial void LogCompensationDeleteFailed(ILogger logger, Exception? ex, string eventId, string reason);

    [LoggerMessage(EventId = 807, Level = LogLevel.Error, Message = "MANUAL RECOVERY: failed to revert Postgres transaction {TransactionId} back to completed after a failed Stripe refund call ({Reason})")]
    private static partial void LogRevertClaimFailed(ILogger logger, Exception? ex, string transactionId, string reason);
}
