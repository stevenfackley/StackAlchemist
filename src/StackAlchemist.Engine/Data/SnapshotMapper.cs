using System.Text.Json;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Data;

/// <summary>
/// Builds a <see cref="GenerationSnapshot"/> from already-read column values, so every delivery
/// service parses <c>project_type</c>, <c>schema_json</c> and <c>personalization_json</c> by the
/// same rules whatever the transport. Null-tolerant: a missing value falls back to the column's
/// default (status <c>pending</c>, tier and attempt count 0).
/// </summary>
public static class SnapshotMapper
{
    /// <exception cref="JsonException">A jsonb column holds JSON that does not fit its model.</exception>
    public static GenerationSnapshot Map(
        string id,
        string? status,
        int? tier,
        string? mode,
        string? prompt,
        string? projectType,
        string? schemaJson,
        string? personalizationJson,
        int? attemptCount,
        DateTimeOffset? updatedAt) =>
        new(
            id,
            status ?? "pending",
            tier ?? 0,
            mode,
            prompt,
            Enum.TryParse<ProjectType>(projectType, ignoreCase: true, out var parsed) ? parsed : null,
            Deserialize<GenerationSchema>(schemaJson),
            Deserialize<GenerationPersonalization>(personalizationJson),
            attemptCount ?? 0,
            updatedAt ?? DateTimeOffset.MinValue);

    // DeliveryJson.Read, not the default options: schema_json arrives lowercase from the Engine (its
    // keys are all single words, so snake_case is lowercase) and camelCase from the web, and a
    // case-sensitive read of either silently yields an empty model.
    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, DeliveryJson.Read);
}
