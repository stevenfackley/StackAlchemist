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
}
