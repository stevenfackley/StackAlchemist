using System.Text.Json;

namespace StackAlchemist.Engine.Data;

/// <summary>JSON shapes are part of the schema: the web reads schema_json / personalization_json as written here.</summary>
public static class DeliveryJson
{
    /// <summary>Writes (payloads, jsonb columns): snake_case, the PostgREST-era convention the web app parses.</summary>
    public static readonly JsonSerializerOptions Write = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Reads: tolerant of either casing.</summary>
    public static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };
}
