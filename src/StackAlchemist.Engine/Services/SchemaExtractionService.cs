using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface ISchemaExtractionService
{
    /// <summary>
    /// Parses an LLM response (from the schema-extraction prompt) into a <see cref="GenerationSchema"/>.
    /// Strips markdown code fences, validates JSON, and validates relationship entity references.
    /// </summary>
    GenerationSchema ParseExtractionResponse(string llmResponse);
}

/// <summary>
/// Converts raw LLM text (JSON, possibly wrapped in markdown) into a validated <see cref="GenerationSchema"/>.
/// Used in Simple Mode after the schema-extraction LLM call.
/// </summary>
public sealed partial class SchemaExtractionService : ISchemaExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public GenerationSchema ParseExtractionResponse(string llmResponse)
    {
        var json = ExtractJson(llmResponse);

        LlmSchemaDto raw;
        try
        {
            raw = JsonSerializer.Deserialize<LlmSchemaDto>(json, JsonOptions)
                ?? throw new SchemaExtractionException("LLM returned a null schema response.");
        }
        catch (JsonException ex)
        {
            throw new SchemaExtractionException(
                $"Failed to parse LLM schema response as JSON: {ex.Message}", ex);
        }

        ValidateRelationships(raw);

        return MapToSchema(raw);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ExtractJson(string llmResponse)
    {
        // Strip ```json ... ``` or ``` ... ``` markdown fences
        var fenceMatch = JsonFenceRegex().Match(llmResponse);
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        // Fallback: find the outer { ... } braces
        var start = llmResponse.IndexOf('{');
        var end = llmResponse.LastIndexOf('}');
        if (start >= 0 && end > start)
            return llmResponse[start..(end + 1)];

        return llmResponse.Trim();
    }

    private static void ValidateRelationships(LlmSchemaDto raw)
    {
        var entityNames = raw.Entities
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rel in raw.Relationships)
        {
            if (!entityNames.Contains(rel.From))
                throw new SchemaValidationException(
                    $"Relationship references unknown entity '{rel.From}'. " +
                    $"Known entities: {string.Join(", ", entityNames)}");

            if (!entityNames.Contains(rel.To))
                throw new SchemaValidationException(
                    $"Relationship references unknown entity '{rel.To}'. " +
                    $"Known entities: {string.Join(", ", entityNames)}");
        }
    }

    private static GenerationSchema MapToSchema(LlmSchemaDto raw) => new()
    {
        Entities = raw.Entities.Select(e => new SchemaEntity
        {
            Name = e.Name,
            Fields = e.Fields.Select(f => new SchemaField
            {
                Name = f.Name,
                Type = f.Type,
                Pk = f.IsPrimaryKey,
                Nullable = f.Nullable,
            }).ToList(),
        }).ToList(),
        Relationships = raw.Relationships.Select(r => new SchemaRelationship
        {
            From = r.From,
            To = r.To,
            Type = r.Type,
        }).ToList(),
    };

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)\n?```", RegexOptions.Singleline)]
    private static partial Regex JsonFenceRegex();

    // ── Internal DTOs (mirrors LLM JSON shape) ────────────────────────────────

    private sealed class LlmSchemaDto
    {
        [JsonPropertyName("entities")]
        public List<LlmEntityDto> Entities { get; init; } = [];

        [JsonPropertyName("relationships")]
        public List<LlmRelationshipDto> Relationships { get; init; } = [];
    }

    private sealed class LlmEntityDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("fields")]
        public List<LlmFieldDto> Fields { get; init; } = [];
    }

    private sealed class LlmFieldDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "string";

        [JsonPropertyName("isPrimaryKey")]
        public bool IsPrimaryKey { get; init; }

        [JsonPropertyName("nullable")]
        public bool Nullable { get; init; }
    }

    private sealed class LlmRelationshipDto
    {
        [JsonPropertyName("from")]
        public string From { get; init; } = "";

        [JsonPropertyName("to")]
        public string To { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "one-to-many";
    }
}
