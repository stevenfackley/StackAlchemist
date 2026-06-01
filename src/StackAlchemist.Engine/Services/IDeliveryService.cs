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
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status using a raw string value (for frontend-facing statuses
    /// like "extracting_schema" that don't map 1:1 to the engine state machine).
    /// </summary>
    Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null);

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
    /// Marks any generation rows still in a non-terminal state
    /// (pending / extracting_schema / generating_code / building) and older than
    /// <paramref name="olderThan"/> as failed. Used at startup to clean up rows
    /// orphaned by an engine restart mid-flight (the in-process queue is volatile).
    /// Returns the number of rows reconciled; 0 when Supabase is not configured or
    /// the sweep fails.
    /// </summary>
    Task<int> FailStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct);
}
