namespace StackAlchemist.Engine.Models;

/// <summary>
/// Point-in-time read of a <c>generations</c> row, used to re-read authoritative
/// state (tier after a Stripe webhook race) and to rebuild a
/// <see cref="GenerateRequest"/> when re-enqueueing an orphaned job.
/// </summary>
public sealed record GenerationSnapshot(
    string Id,
    string Status,
    int Tier,
    string? Mode,
    string? Prompt,
    ProjectType? ProjectType,
    GenerationSchema? Schema,
    GenerationPersonalization? Personalization,
    int AttemptCount,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// True when the row's jsonb (<c>schema_json</c>, <c>personalization_json</c>) holds JSON that
    /// does not fit its model. Only the plain columns the reconciler decides and compare-and-sets on
    /// are populated (id, status, tier, attempt count, updated_at); a null <see cref="Schema"/> here
    /// means "unknown", not "absent". Such a row can never be rebuilt, so the reconciler fails it
    /// rather than skipping it on every tick (StackAlchemist#425).
    /// </summary>
    public bool IsUnreadable { get; init; }
}
