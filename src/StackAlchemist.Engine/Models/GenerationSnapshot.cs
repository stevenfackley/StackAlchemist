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
    DateTimeOffset UpdatedAt);
