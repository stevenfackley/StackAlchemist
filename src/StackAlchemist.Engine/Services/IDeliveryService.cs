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
}
