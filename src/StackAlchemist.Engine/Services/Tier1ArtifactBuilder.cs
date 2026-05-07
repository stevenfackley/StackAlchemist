using System.Globalization;
using System.Text;
using System.Text.Json;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Renders the deliverables for a Tier 1 (Blueprint, $299) generation: a pretty-printed
/// <c>schema.json</c> and a Markdown <c>api-docs.md</c> describing the CRUD contract per
/// entity. Tier 1 deliberately skips LLM code generation, so this is the entire
/// orchestrator-side output before pack+upload.
/// </summary>
public static class Tier1ArtifactBuilder
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static Dictionary<string, string> Build(GenerationSchema? schema, TemplateVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var effectiveSchema = schema ?? new GenerationSchema();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema.json"] = JsonSerializer.Serialize(effectiveSchema, IndentedJson),
            ["api-docs.md"] = BuildApiDocsMarkdown(effectiveSchema, variables),
        };
    }

    private static string BuildApiDocsMarkdown(GenerationSchema schema, TemplateVariables variables)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {variables.ProjectName} — API Reference");
        sb.AppendLine();
        sb.AppendLine("Tier 1 Blueprint deliverable: schema definition and CRUD endpoint contract.");
        sb.AppendLine("Implementation code is not included at this tier — upgrade to Tier 2 (Boilerplate)");
        sb.AppendLine("for the generated `.NET` / `React` codebase, or Tier 3 (Infrastructure) for IaC + Helm.");
        sb.AppendLine();

        if (schema.Entities.Count == 0)
        {
            sb.AppendLine("_No entities are defined in this schema._");
            return sb.ToString();
        }

        var entityLookup = variables.Entities.ToDictionary(e => e.Name, StringComparer.Ordinal);

        foreach (var entity in schema.Entities)
        {
            var nameLower = entityLookup.TryGetValue(entity.Name, out var te)
                ? te.NameLower
                : entity.Name.ToLowerInvariant();

            sb.AppendLine(CultureInfo.InvariantCulture, $"## {entity.Name}");
            sb.AppendLine();

            sb.AppendLine("### Fields");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Notes |");
            sb.AppendLine("|---|---|---|");
            foreach (var f in entity.Fields)
            {
                var notes = new List<string>();
                if (f.Pk) notes.Add("primary key");
                if (f.Nullable) notes.Add("nullable");
                if (!string.IsNullOrWhiteSpace(f.Default)) notes.Add($"default: `{f.Default}`");
                var notesText = notes.Count > 0 ? string.Join(", ", notes) : "—";
                sb.AppendLine(CultureInfo.InvariantCulture, $"| `{f.Name}` | `{f.Type}` | {notesText} |");
            }
            sb.AppendLine();

            sb.AppendLine("### Endpoints");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `GET /api/v1/{nameLower}s` — list all `{entity.Name}` records");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `GET /api/v1/{nameLower}s/{{id}}` — fetch a single `{entity.Name}` by id");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `POST /api/v1/{nameLower}s` — create a new `{entity.Name}`");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `PUT /api/v1/{nameLower}s/{{id}}` — update an existing `{entity.Name}`");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `DELETE /api/v1/{nameLower}s/{{id}}` — delete a `{entity.Name}`");
            sb.AppendLine();
        }

        if (schema.Relationships.Count > 0)
        {
            sb.AppendLine("## Relationships");
            sb.AppendLine();
            foreach (var r in schema.Relationships)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{r.From}` `{r.Type}` `{r.To}`");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
