using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Persists generation pipeline state transitions back to the Supabase
/// <c>generations</c> table so the frontend can display real-time progress.
/// </summary>
public interface IDeliveryService
{
    /// <summary>
    /// Updates the <c>generations</c> row for <paramref name="generationId"/> with the
    /// current <paramref name="state"/> and optional <paramref name="downloadUrl"/> /
    /// <paramref name="errorMessage"/>.  Silently no-ops when Supabase is not configured.
    /// </summary>
    Task UpdateStatusAsync(
        string generationId,
        GenerationState state,
        string? downloadUrl = null,
        string? errorMessage = null,
        string? errorCategory = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status using a raw string value (for frontend-facing statuses
    /// like "extracting_schema" that don't map 1:1 to the engine state machine).
    /// </summary>
    Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null,
        string? errorCategory = null);

    /// <summary>
    /// Reads the current generations row for re-checking authoritative state
    /// (e.g. the tier after a concurrent Stripe webhook upgrade). Returns null
    /// when Supabase is not configured, the row is missing, or the read fails.
    /// </summary>
    Task<GenerationSnapshot?> GetGenerationSnapshotAsync(string generationId, CancellationToken ct);

    /// <summary>
    /// Tier-0 (free Spark preview) terminal write: stores the generated file map in
    /// <c>preview_files_json</c> and flips the row to <c>success</c> in a single atomic
    /// PATCH. No build, pack, R2 upload, or <c>download_url</c> — the frontend renders the
    /// files inline in an in-browser editor. Critical write (retried once); the UI blocks
    /// on it via Realtime.
    /// </summary>
    Task CompletePreviewAsync(
        string generationId,
        IReadOnlyDictionary<string, string> files,
        CancellationToken ct);

    /// <summary>
    /// Persists the extracted schema JSON to the generation row.
    /// </summary>
    Task UpdateSchemaAsync(
        string generationId,
        GenerationSchema schema,
        CancellationToken ct);

    /// <summary>
    /// Adds token usage from an LLM call to the generation row.
    /// </summary>
    Task UpdateTokenUsageAsync(
        string generationId,
        int inputTokens,
        int outputTokens,
        string model,
        CancellationToken ct);

    /// <summary>
    /// Appends build output to the build_log column.
    /// </summary>
    Task AppendBuildLogAsync(
        string generationId,
        string logChunk,
        CancellationToken ct);

    /// <summary>
    /// Looks up the email address of the user who owns the given generation, by
    /// joining the generations row to its profile. Returns null when the user is
    /// anonymous, the generation does not exist, or Supabase is not configured.
    /// </summary>
    Task<string?> GetGenerationOwnerEmailAsync(string generationId, CancellationToken ct);

    /// <summary>
    /// Lists generation rows still in a non-terminal state whose last update predates
    /// <paramref name="olderThan"/>. Used by the periodic reconciler to find jobs
    /// orphaned by a restart or stalled in flight. Empty when Supabase is not
    /// configured or the read fails.
    /// </summary>
    Task<IReadOnlyList<GenerationSnapshot>> GetStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct);

    /// <summary>
    /// Atomically claims a stale row for re-enqueue via a conditional PATCH on
    /// (id, status, attempt_count, updated_at &lt; cutoff): sets status=pending and
    /// increments attempt_count. Returns true only when this caller won the claim —
    /// a concurrent claimer's filter no longer matches after the first one commits.
    /// </summary>
    Task<bool> TryClaimForRequeueAsync(GenerationSnapshot row, TimeSpan olderThan, CancellationToken ct);

    /// <summary>
    /// Conditionally fails a stale row (same CAS filter shape as
    /// <see cref="TryClaimForRequeueAsync"/>), so a row that progressed since it was
    /// listed is left alone. Returns true when the row was failed by this call.
    /// </summary>
    Task<bool> TryFailStaleRowAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        string errorMessage,
        string errorCategory,
        CancellationToken ct);

    /// <summary>
    /// Atomically transitions a row from pending/failed to extracting_schema. Returns
    /// false when the row is already mid-extraction or terminal — the double-submit
    /// guard for /api/extract-schema. Returns true when Supabase is not configured so
    /// local dev keeps working.
    /// </summary>
    Task<bool> TryBeginExtractionAsync(string generationId, CancellationToken ct);

    /// <summary>
    /// Re-issues a previously buffered critical write exactly once (no retries, no
    /// re-buffering). Used by the reconciler to flush <see cref="IPendingWriteBuffer"/>.
    /// </summary>
    Task<bool> TryPatchOnceAsync(string generationId, Dictionary<string, object?> payload, CancellationToken ct);
}
