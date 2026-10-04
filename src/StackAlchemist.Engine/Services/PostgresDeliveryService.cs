using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using StackAlchemist.Engine.Data;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Persists generation pipeline state to the qavren-db <c>stackalchemist.generations</c> table over
/// Npgsql, with per-write retry budgets, compare-and-set filters and pending-write buffering for
/// terminal writes. The store's delivery service: registered whenever DATABASE_URL is set.
/// <para>
/// Generation ids arrive as strings. One that is not a uuid can never match a row, so every method
/// treats it as "not found" (null / false / no-op) without a round trip.
/// </para>
/// </summary>
public sealed partial class PostgresDeliveryService(
    NpgsqlDataSource dataSource,
    IPendingWriteBuffer pendingWrites,
    ILogger<PostgresDeliveryService> logger,
    int criticalMaxAttempts = 5,
    TimeSpan? criticalBaseDelay = null) : IDeliveryService
{
    // Terminal writes (success/failed) are what the UI blocks on — a dropped one strands the row,
    // so they get a deep exponential-backoff budget (1s/2s/4s/8s between 5 tries by default).
    // Progress pings are disposable (the next ping supersedes them): 2 quick tries. The two
    // function calls (token usage, build log) are additive nice-to-haves: one retry after 1s.
    private const int NonCriticalMaxAttempts = 2;
    private const int FunctionMaxAttempts = 2;
    private static readonly TimeSpan NonCriticalDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FunctionDelay = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _criticalBaseDelay = criticalBaseDelay ?? TimeSpan.FromSeconds(1);

    // The only columns a payload may name, with the type each binds as. The names are spliced into
    // SQL, so this map is also what keeps a payload key from ever becoming SQL text.
    private static readonly Dictionary<string, NpgsqlDbType> PatchableColumns = new(StringComparer.Ordinal)
    {
        ["status"] = NpgsqlDbType.Text,
        ["updated_at"] = NpgsqlDbType.TimestampTz,
        ["completed_at"] = NpgsqlDbType.TimestampTz,
        ["download_url"] = NpgsqlDbType.Text,
        ["error_message"] = NpgsqlDbType.Text,
        ["error_category"] = NpgsqlDbType.Text,
        ["preview_files_json"] = NpgsqlDbType.Jsonb,
        ["schema_json"] = NpgsqlDbType.Jsonb,
        ["attempt_count"] = NpgsqlDbType.Integer,
    };

    private const string SnapshotColumns =
        "select id, status, tier, mode, prompt, project_type, schema_json, personalization_json, attempt_count, updated_at " +
        "from stackalchemist.generations";

    // "Every status except the two terminal ones (success, failed)" — a restart can orphan a job
    // mid-packing/uploading just as easily as mid-build.
    private const string NonTerminalStatuses =
        "('pending', 'extracting_schema', 'generating_code', 'generating', 'building', 'packing', 'uploading')";

    public async Task UpdateStatusAsync(
        string generationId,
        GenerationState state,
        string? downloadUrl = null,
        string? errorMessage = null,
        string? errorCategory = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = state.ToString().ToLowerInvariant(),
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (downloadUrl is not null)
            payload["download_url"] = downloadUrl;

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

        if (errorCategory is not null)
            payload["error_category"] = errorCategory;

        if (state is GenerationState.Success or GenerationState.Failed)
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(
            generationId,
            payload,
            ct,
            critical: state is GenerationState.Success or GenerationState.Failed);
        LogUpdated(logger, generationId, state);
    }

    public async Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null,
        string? errorCategory = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        if (errorMessage is not null)
            payload["error_message"] = errorMessage;

        if (errorCategory is not null)
            payload["error_category"] = errorCategory;

        if (status is "success" or "failed")
            payload["completed_at"] = DateTime.UtcNow.ToString("O");

        await PatchGenerationAsync(
            generationId,
            payload,
            ct,
            critical: status is "success" or "failed");
    }

    public async Task CompletePreviewAsync(
        string generationId,
        IReadOnlyDictionary<string, string> files,
        CancellationToken ct)
    {
        var nowIso = DateTime.UtcNow.ToString("O");

        // One atomic terminal write: the file map lands in the jsonb column and the row flips to
        // success together. Dictionary keys (file paths) serialize verbatim — the naming policy
        // only touches POCO property names.
        var payload = new Dictionary<string, object?>
        {
            ["preview_files_json"] = files,
            ["status"] = "success",
            ["updated_at"] = nowIso,
            ["completed_at"] = nowIso,
        };

        await PatchGenerationAsync(generationId, payload, ct, critical: true);
        LogPreviewCompleted(logger, generationId, files.Count);
    }

    public async Task UpdateSchemaAsync(
        string generationId,
        GenerationSchema schema,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schema_json"] = schema,
            ["updated_at"] = DateTime.UtcNow.ToString("O"),
        };

        await PatchGenerationAsync(generationId, payload, ct);
    }

    public async Task UpdateTokenUsageAsync(
        string generationId,
        int inputTokens,
        int outputTokens,
        string model,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model))
            return;

        await InvokeFunctionAsync(
            "increment_token_usage",
            "select stackalchemist.increment_token_usage($1, $2, $3, $4)",
            generationId,
            [new(inputTokens, NpgsqlDbType.Integer), new(outputTokens, NpgsqlDbType.Integer), new(model, NpgsqlDbType.Text)],
            ct);
    }

    public async Task AppendBuildLogAsync(
        string generationId,
        string logChunk,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(logChunk))
            return;

        await InvokeFunctionAsync(
            "append_build_log",
            "select stackalchemist.append_build_log($1, $2)",
            generationId,
            [new(logChunk, NpgsqlDbType.Text)],
            ct);
    }

    public async Task<string?> GetGenerationOwnerEmailAsync(string generationId, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
            return null;

        try
        {
            await using var cmd = CreateCommand(
                "select p.email from stackalchemist.generations g " +
                "join stackalchemist.profiles p on p.id = g.user_id where g.id = $1",
                [new(id, NpgsqlDbType.Uuid)]);
            return await cmd.ExecuteScalarAsync(ct) as string;
        }
        catch (Exception ex)
        {
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogOwnerEmailLookupFailed(logger, loggable, generationId, reason);
            return null;
        }
    }

    public async Task<ProfileCredential?> GetGenerationCredentialAsync(string generationId, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
            return null;

        try
        {
            // The ciphertext is opaque here — ByokKeyProtector decrypts it downstream.
            await using var cmd = CreateCommand(
                "select p.api_key_override, p.preferred_model from stackalchemist.generations g " +
                "join stackalchemist.profiles p on p.id = g.user_id where g.id = $1",
                [new(id, NpgsqlDbType.Uuid)]);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            return new ProfileCredential(NullableString(reader, 0), NullableString(reader, 1));
        }
        catch (Exception ex)
        {
            // Never log the ciphertext — only the id and the redacted failure.
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogCredentialLookupFailed(logger, loggable, generationId, reason);
            return null;
        }
    }

    public async Task<IReadOnlyList<GenerationSnapshot>> GetStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct)
    {
        try
        {
            await using var cmd = CreateCommand(
                $"{SnapshotColumns} where status in {NonTerminalStatuses} and updated_at < now() - $1",
                [new(olderThan, NpgsqlDbType.Interval)]);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var rows = new List<GenerationSnapshot>();
            while (await reader.ReadAsync(ct))
            {
                // Per row: schema_json can be client-supplied, so one row that does not fit the model
                // must cost only that row, not blind the whole sweep for every other user. It is still
                // returned, flagged unreadable, so the reconciler can fail it (#425).
                var id = reader.GetGuid(0).ToString();
                try
                {
                    rows.Add(ReadSnapshot(reader));
                }
                catch (JsonException ex)
                {
                    var (loggable, reason) = PostgresErrors.Redact(ex);
                    LogSnapshotReadFailed(logger, loggable, id, reason);
                    rows.Add(ReadUnreadableSnapshot(reader));
                }
            }
            return rows;
        }
        catch (Exception ex)
        {
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogStaleReconcileFailed(logger, loggable, reason);
            return [];
        }
    }

    public async Task<bool> TryClaimForRequeueAsync(GenerationSnapshot row, TimeSpan olderThan, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = "pending",
            ["attempt_count"] = row.AttemptCount + 1,
            ["error_message"] = null,
            ["error_category"] = null,
        };

        return await TryConditionalPatchAsync(row, olderThan, payload, "requeue-claim", ct);
    }

    public async Task<bool> TryFailStaleRowAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        string errorMessage,
        string errorCategory,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = "failed",
            ["error_message"] = errorMessage,
            ["error_category"] = errorCategory,
            ["completed_at"] = DateTime.UtcNow.ToString("O"),
        };

        var failed = await TryConditionalPatchAsync(row, olderThan, payload, "stale-fail", ct);
        if (failed)
            LogStaleReconciled(logger, 1);
        return failed;
    }

    /// <summary>
    /// Compare-and-set: the WHERE clause pins (id, status, attempt_count, updated_at &lt; cutoff).
    /// Postgres serializes competing UPDATEs; the loser's filter no longer matches (status changed,
    /// attempt_count changed, and the set_updated_at trigger bumped updated_at past the cutoff), so
    /// it affects zero rows. Exactly one affected row means this caller won.
    /// </summary>
    private async Task<bool> TryConditionalPatchAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        Dictionary<string, object?> payload,
        string operation,
        CancellationToken ct)
    {
        var (set, args) = BuildPatch(payload);
        if (!Guid.TryParse(row.Id, out var id))
            return false;

        var n = args.Count;
        args.Add(new(id, NpgsqlDbType.Uuid));
        args.Add(new(row.Status, NpgsqlDbType.Text));
        args.Add(new(row.AttemptCount, NpgsqlDbType.Integer));
        args.Add(new(olderThan, NpgsqlDbType.Interval));
        var sql =
            $"update stackalchemist.generations set {set} " +
            $"where id = ${n + 1} and status = ${n + 2} and attempt_count = ${n + 3} and updated_at < now() - ${n + 4}";

        try
        {
            await using var cmd = CreateCommand(sql, args);
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }
        catch (Exception ex)
        {
            LogWriteFailure(ex, operation, row.Id);
            return false;
        }
    }

    public async Task<bool> TryBeginExtractionAsync(string generationId, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
            return false;

        // pending and failed are the only legal entry states (failed = user retry). A double-submit
        // loses this CAS (zero rows) and gets rejected by the endpoint.
        try
        {
            await using var cmd = CreateCommand(
                "update stackalchemist.generations " +
                "set status = 'extracting_schema', error_message = null, error_category = null, updated_at = now() " +
                "where id = $1 and status in ('pending', 'failed')",
                [new(id, NpgsqlDbType.Uuid)]);
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }
        catch (Exception ex)
        {
            LogWriteFailure(ex, "begin-extraction", generationId);
            return false;
        }
    }

    public async Task<GenerationSnapshot?> GetGenerationSnapshotAsync(string generationId, CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
            return null;

        try
        {
            await using var cmd = CreateCommand($"{SnapshotColumns} where id = $1", [new(id, NpgsqlDbType.Uuid)]);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadSnapshot(reader) : null;
        }
        catch (Exception ex)
        {
            var (loggable, reason) = PostgresErrors.Redact(ex);
            LogSnapshotReadFailed(logger, loggable, generationId, reason);
            return null;
        }
    }

    public async Task<bool> TryPatchOnceAsync(
        string generationId, Dictionary<string, object?> payload, CancellationToken ct)
    {
        var (set, args) = BuildPatch(payload);
        if (!Guid.TryParse(generationId, out var id))
            return false;

        args.Add(new(id, NpgsqlDbType.Uuid));
        try
        {
            await using var cmd = CreateCommand($"update stackalchemist.generations set {set} where id = ${args.Count}", args);
            return await cmd.ExecuteNonQueryAsync(ct) >= 1;
        }
        catch (Exception ex)
        {
            LogWriteFailure(ex, "patch-once", generationId);
            return false;
        }
    }

    // ── Shared write paths ──────────────────────────────────────────────────

    private async Task PatchGenerationAsync(
        string generationId,
        Dictionary<string, object?> payload,
        CancellationToken ct,
        bool critical = false)
    {
        // Validated before anything else: an unknown column is a programming error, so it throws
        // here — never retried, never buffered.
        var (set, args) = BuildPatch(payload);
        if (!Guid.TryParse(generationId, out var id))
        {
            LogPatchSkippedNotUuid(logger, generationId);
            return;
        }

        args.Add(new(id, NpgsqlDbType.Uuid));
        var sql = $"update stackalchemist.generations set {set} where id = ${args.Count}";

        // Success means the statement ran, as PostgREST's return=minimal did: a missing row is
        // not a failure to buffer.
        var succeeded = await ExecuteWithRetriesAsync(
            sql,
            args,
            critical ? criticalMaxAttempts : NonCriticalMaxAttempts,
            attempt => critical ? _criticalBaseDelay * Math.Pow(2, attempt - 1) : NonCriticalDelay,
            ex => LogWriteFailure(ex, "patch", generationId),
            ct);

        // !succeeded: every attempt failed, or a non-transient error (a CHECK violation, say) cut the
        // budget short. A terminal write is buffered either way, for the periodic reconciler to
        // re-flush once per tick until its age cap, so an outage no longer strands the row in a
        // non-terminal state. A write that can never succeed is buffered too; it costs one retry per
        // tick until the age cap drops it.
        if (!succeeded && critical)
        {
            pendingWrites.Enqueue(new PendingGenerationWrite(generationId, payload, DateTimeOffset.UtcNow));
            LogCriticalPatchFailed(logger, generationId);
        }
    }

    private async Task InvokeFunctionAsync(
        string functionName,
        string sql,
        string generationId,
        SqlArg[] args,
        CancellationToken ct)
    {
        if (!Guid.TryParse(generationId, out var id))
        {
            LogFunctionSkippedNotUuid(logger, functionName, generationId);
            return;
        }

        await ExecuteWithRetriesAsync(
            sql,
            [new(id, NpgsqlDbType.Uuid), .. args],
            FunctionMaxAttempts,
            _ => FunctionDelay,
            ex => LogFunctionFailure(ex, functionName),
            ct);
    }

    /// <summary>
    /// Runs one statement under a retry budget and reports whether it ran. Only failures Npgsql
    /// classes as transient (a broken or refused connection, a timeout, and the SQLSTATEs that mean
    /// "try again": serialization failure, deadlock, too many connections) are retried — the
    /// analogue of PostgREST's 5xx/408/429. Anything else (a CHECK violation, a bad value,
    /// cancellation) cannot succeed on retry and ends the budget at once, as a 4xx did.
    /// </summary>
    private async Task<bool> ExecuteWithRetriesAsync(
        string sql,
        IReadOnlyList<SqlArg> args,
        int maxAttempts,
        Func<int, TimeSpan> delayAfterAttempt,
        Action<Exception> logFailure,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var cmd = CreateCommand(sql, args);
                await cmd.ExecuteNonQueryAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                logFailure(ex);
                if (ex is not (NpgsqlException { IsTransient: true } or TimeoutException))
                    return false;
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    await Task.Delay(delayAfterAttempt(attempt), ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }

    // ── SQL building ────────────────────────────────────────────────────────

    /// <summary>One positional parameter: its value (never null — <see cref="DBNull"/>) and the type it binds as.</summary>
    private readonly record struct SqlArg(object Value, NpgsqlDbType Type);

    /// <summary>
    /// Turns a payload into a <c>SET</c> list bound as <c>$1..$n</c> in payload order. Throws
    /// <see cref="ArgumentException"/> for a key outside <see cref="PatchableColumns"/> (or a value
    /// its column cannot take) before any I/O happens.
    /// </summary>
    private static (string Set, List<SqlArg> Args) BuildPatch(Dictionary<string, object?> payload)
    {
        if (payload.Count == 0)
            throw new ArgumentException("A generations patch needs at least one column.", nameof(payload));

        var sets = new List<string>(payload.Count);
        var args = new List<SqlArg>(payload.Count + 4);
        foreach (var (column, value) in payload)
        {
            if (!PatchableColumns.TryGetValue(column, out var type))
                throw new ArgumentException($"'{column}' is not a patchable generations column.", nameof(payload));

            args.Add(new(ToParameterValue(column, type, value), type));
            sets.Add($"{column} = ${args.Count}");
        }

        return (string.Join(", ", sets), args);
    }

    private static object ToParameterValue(string column, NpgsqlDbType type, object? value)
    {
        if (value is null)
            return DBNull.Value;

        return type switch
        {
            // Same serializer the PostgREST payloads used, so jsonb columns keep their shape.
            NpgsqlDbType.Jsonb => JsonSerializer.Serialize(value, value.GetType(), DeliveryJson.Write),
            // timestamptz only accepts UTC from Npgsql; payloads carry DateTime.UtcNow.ToString("O").
            NpgsqlDbType.TimestampTz => value switch
            {
                string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    => parsed.ToUniversalTime(),
                DateTimeOffset dto => dto.ToUniversalTime(),
                DateTime dt => new DateTimeOffset(dt.ToUniversalTime()),
                _ => throw new ArgumentException($"'{column}' needs an ISO-8601 timestamp.", nameof(value)),
            },
            NpgsqlDbType.Integer => value is int i
                ? i
                : throw new ArgumentException($"'{column}' needs an int.", nameof(value)),
            _ => value as string ?? throw new ArgumentException($"'{column}' needs a string.", nameof(value)),
        };
    }

    private NpgsqlCommand CreateCommand(string sql, IReadOnlyList<SqlArg> args)
    {
        // A fresh command and fresh parameters per execution: an NpgsqlParameter belongs to one
        // collection, and a data-source command takes a pooled connection for each execution.
        var cmd = dataSource.CreateCommand(sql);
        foreach (var arg in args)
            cmd.Parameters.Add(new NpgsqlParameter { Value = arg.Value, NpgsqlDbType = arg.Type });
        return cmd;
    }

    private static GenerationSnapshot ReadSnapshot(NpgsqlDataReader r) =>
        SnapshotMapper.Map(
            id: r.GetGuid(0).ToString(),
            status: NullableString(r, 1),
            tier: r.IsDBNull(2) ? null : r.GetInt32(2),
            mode: NullableString(r, 3),
            prompt: NullableString(r, 4),
            projectType: NullableString(r, 5),
            schemaJson: NullableString(r, 6),
            personalizationJson: NullableString(r, 7),
            attemptCount: r.IsDBNull(8) ? null : r.GetInt32(8),
            updatedAt: r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));

    // Same row, none of the jsonb columns: what the reconciler needs to fail it and nothing more.
    private static GenerationSnapshot ReadUnreadableSnapshot(NpgsqlDataReader r) =>
        SnapshotMapper.Unreadable(
            id: r.GetGuid(0).ToString(),
            status: NullableString(r, 1),
            tier: r.IsDBNull(2) ? null : r.GetInt32(2),
            attemptCount: r.IsDBNull(8) ? null : r.GetInt32(8),
            updatedAt: r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));

    private static string? NullableString(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private void LogWriteFailure(Exception ex, string operation, string generationId)
    {
        var (loggable, reason) = PostgresErrors.Redact(ex);
        if (ex is PostgresException)
            LogWriteRejected(logger, operation, generationId, reason);
        else
            LogWriteFailed(logger, loggable, operation, generationId, reason);
    }

    private void LogFunctionFailure(Exception ex, string functionName)
    {
        var (loggable, reason) = PostgresErrors.Redact(ex);
        if (ex is PostgresException)
            LogFunctionRejected(logger, functionName, reason);
        else
            LogFunctionFailed(logger, loggable, functionName, reason);
    }

    // ── LoggerMessage source-gen (EventIds 500–513, kept from the original delivery service) ─

    [LoggerMessage(EventId = 500, Level = LogLevel.Information, Message = "Postgres updated: generation {Id} → {State}")]
    private static partial void LogUpdated(ILogger logger, string id, GenerationState state);

    [LoggerMessage(EventId = 501, Level = LogLevel.Warning, Message = "Failed to look up owner email for generation {Id} ({Reason})")]
    private static partial void LogOwnerEmailLookupFailed(ILogger logger, Exception? ex, string id, string reason);

    [LoggerMessage(EventId = 502, Level = LogLevel.Debug, Message = "Generation id {Id} is not a uuid — skipping Postgres update")]
    private static partial void LogPatchSkippedNotUuid(ILogger logger, string id);

    [LoggerMessage(EventId = 503, Level = LogLevel.Warning, Message = "Postgres rejected {Operation} for generation {Id}: {Reason}")]
    private static partial void LogWriteRejected(ILogger logger, string operation, string id, string reason);

    [LoggerMessage(EventId = 504, Level = LogLevel.Error, Message = "Failed to run Postgres {Operation} for generation {Id} ({Reason})")]
    private static partial void LogWriteFailed(ILogger logger, Exception? ex, string operation, string id, string reason);

    [LoggerMessage(EventId = 505, Level = LogLevel.Debug, Message = "Generation id {Id} is not a uuid — skipping Postgres function {Function}")]
    private static partial void LogFunctionSkippedNotUuid(ILogger logger, string function, string id);

    [LoggerMessage(EventId = 506, Level = LogLevel.Warning, Message = "Postgres function {Function} was rejected: {Reason}")]
    private static partial void LogFunctionRejected(ILogger logger, string function, string reason);

    [LoggerMessage(EventId = 507, Level = LogLevel.Error, Message = "Failed to invoke Postgres function {Function} ({Reason})")]
    private static partial void LogFunctionFailed(ILogger logger, Exception? ex, string function, string reason);

    [LoggerMessage(EventId = 508, Level = LogLevel.Error, Message = "CRITICAL: terminal status update for generation {Id} failed after retries — row may be stranded in a non-terminal state")]
    private static partial void LogCriticalPatchFailed(ILogger logger, string id);

    [LoggerMessage(EventId = 509, Level = LogLevel.Information, Message = "Reconciliation: marked {Count} stale generation(s) as failed")]
    private static partial void LogStaleReconciled(ILogger logger, int count);

    [LoggerMessage(EventId = 510, Level = LogLevel.Error, Message = "Reconciliation sweep failed ({Reason})")]
    private static partial void LogStaleReconcileFailed(ILogger logger, Exception? ex, string reason);

    [LoggerMessage(EventId = 511, Level = LogLevel.Information, Message = "Tier-0 preview written: generation {Id} → success with {Count} inline files")]
    private static partial void LogPreviewCompleted(ILogger logger, string id, int count);

    [LoggerMessage(EventId = 512, Level = LogLevel.Warning, Message = "Failed to read generation snapshot for {Id} ({Reason})")]
    private static partial void LogSnapshotReadFailed(ILogger logger, Exception? ex, string id, string reason);

    [LoggerMessage(EventId = 513, Level = LogLevel.Warning, Message = "Failed to look up BYOK credential for generation {Id} ({Reason})")]
    private static partial void LogCredentialLookupFailed(ILogger logger, Exception? ex, string id, string reason);
}
