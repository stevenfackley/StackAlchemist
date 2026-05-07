using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface IPromptBuilderService
{
    string BuildGenerationPrompt(GenerationSchema schema, ProjectType projectType = ProjectType.DotNetNextJs, GenerationPersonalization? personalization = null);
    string BuildRetryPrompt(string originalPrompt, IReadOnlyList<string> errorHistory, int retryAttempt);
    string BuildSchemaExtractionPrompt(string userPrompt);
    string BuildInjectionPrompt(InjectionPromptContext context);
}

/// <summary>
/// Constructs structured prompts sent to Claude 3.5 Sonnet (or local Ollama in dev).
/// All code-generation prompts enforce the [[FILE:path]]…[[END_FILE]] output format.
/// </summary>
public sealed class PromptBuilderService : IPromptBuilderService
{
    // Rough budget: keep well under Claude 3.5's 200 k context window.
    private const int MaxRetryPromptChars = 8_000;
    private static readonly char[] ControlCharsToStrip =
        Enumerable.Range(0, 32)
            .Select(i => (char)i)
            .Where(ch => ch is not '\r' and not '\n' and not '\t')
            .ToArray();

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    // ── Generation prompt ─────────────────────────────────────────────────────

    public string BuildGenerationPrompt(GenerationSchema schema, ProjectType projectType = ProjectType.DotNetNextJs, GenerationPersonalization? personalization = null)
    {
        var schemaJson = JsonSerializer.Serialize(schema, IndentedJson);
        var sb = new StringBuilder();

        var stackDescription = projectType switch
        {
            ProjectType.PythonReact =>
                "You are an expert Python (FastAPI, SQLAlchemy, Pydantic, Alembic) and React (Vite, TypeScript, Tailwind CSS, TanStack Query) developer generating production-quality code.",
            _ =>
                "You are an expert .NET 10 and Next.js 15 developer generating production-quality code.",
        };

        sb.AppendLine(stackDescription);
        sb.AppendLine();
        sb.AppendLine("## Output Format");
        sb.AppendLine("ONLY output file blocks using this exact format. Output NOTHING else — no prose, no explanation, no markdown outside the blocks:");
        sb.AppendLine();
        sb.AppendLine("[[FILE:relative/path/to/file.ext]]");
        sb.AppendLine("... file content here ...");
        sb.AppendLine("[[END_FILE]]");
        sb.AppendLine();

        if (projectType == ProjectType.PythonReact)
        {
            sb.AppendLine("## Stack");
            sb.AppendLine("- Backend: FastAPI with SQLAlchemy ORM, Pydantic schemas, Alembic migrations");
            sb.AppendLine("- Frontend: React 19 with Vite, TypeScript strict mode, Tailwind CSS, TanStack Query");
            sb.AppendLine("- Use `backend/` prefix for Python files and `frontend/src/` prefix for React files");
            sb.AppendLine();
        }

        sb.AppendLine("## Schema");
        sb.AppendLine("Generate a full-stack application for the following data schema:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(schemaJson);
        sb.AppendLine("```");

        if (schema.Entities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Entities");
            foreach (var entity in schema.Entities)
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {entity.Name}");
        }

        if (schema.Relationships.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Relationships");
            foreach (var rel in schema.Relationships)
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {rel.From} → {rel.To} ({rel.Type})");
        }

        // ── Personalization context ───────────────────────────────────────────
        if (personalization is not null)
        {
            var sanitizedProjectName = SanitizeUserInput(personalization.ProjectName, 50);
            var sanitizedTagline = SanitizeUserInput(personalization.Tagline, 50);
            var sanitizedBusinessDescription = SanitizeUserInput(personalization.BusinessDescription, 500);
            var sanitizedDomainContext = personalization.DomainContext
                .Select(pair => new KeyValuePair<string, string>(
                    SanitizeUserInput(pair.Key, 50),
                    SanitizeUserInput(pair.Value, 500)))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToList();
            var sanitizedAuthMethod = SanitizeUserInput(personalization.FeatureFlags?.AuthMethod, 50);

            if (!string.IsNullOrWhiteSpace(sanitizedBusinessDescription))
            {
                sb.AppendLine();
                sb.AppendLine("## Business Context");
                sb.AppendLine("Use the following context to inform code comments, seed data, UI copy, validation messages, and README content:");
                if (!string.IsNullOrWhiteSpace(sanitizedProjectName))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **Project name:** {sanitizedProjectName}");
                if (!string.IsNullOrWhiteSpace(sanitizedTagline))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **Tagline:** {sanitizedTagline}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Description:** {sanitizedBusinessDescription}");
            }

            if (sanitizedDomainContext.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Domain Vocabulary");
                sb.AppendLine("Use realistic domain language in controller logic, validation, comments, and seed data:");
                foreach (var (entity, context) in sanitizedDomainContext)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **{entity}:** {context}");
            }

            if (personalization.ColorScheme is not null)
            {
                sb.AppendLine();
                sb.AppendLine("## Color Theme");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Use this palette in the generated frontend's Tailwind config (tailwind.config.ts):");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- primary: {personalization.ColorScheme.Primary}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- secondary: {personalization.ColorScheme.Secondary}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- accent: {personalization.ColorScheme.Accent}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- background: {personalization.ColorScheme.Background}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- surface: {personalization.ColorScheme.Surface}");
            }

            if (personalization.FeatureFlags is not null)
            {
                var ff = personalization.FeatureFlags;
                sb.AppendLine();
                sb.AppendLine("## Feature Flags");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Authentication method: {sanitizedAuthMethod}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Soft delete (deleted_at): {(ff.SoftDelete ? "yes — add deleted_at TIMESTAMPTZ to all entities and filter in queries" : "no")}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Audit timestamps (created_at/updated_at): {(ff.AuditTimestamps ? "yes — include on all tables" : "no")}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Swagger/OpenAPI docs: {(ff.IncludeSwagger ? "yes" : "no")}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Docker Compose for local dev: {(ff.IncludeDockerCompose ? "yes — include in output" : "no")}");
            }
        }

        return sb.ToString();
    }

    // ── Retry prompt ──────────────────────────────────────────────────────────

    public string BuildRetryPrompt(string originalPrompt, IReadOnlyList<string> errorHistory, int retryAttempt)
    {
        var header = $"""
            The previous code generation attempt failed to compile. This is retry attempt {retryAttempt} of 3.

            ## Build Errors from Previous Attempts

            """;

        var footer = $"""


            ## Instructions
            Fix ALL build errors listed above. Output the corrected files using the same [[FILE:path]]...[[END_FILE]] format.
            Only output files that need to change — do not repeat unchanged files.

            ## Original Prompt
            {originalPrompt}
            """;

        var baseLength = header.Length + footer.Length;

        // Collect error sections newest-first, respecting the char budget
        var sections = new List<string>();
        var used = baseLength;

        for (var i = errorHistory.Count - 1; i >= 0; i--)
        {
            var section = $"### Attempt {i + 1}\n```\n{errorHistory[i]}\n```\n\n";
            if (used + section.Length > MaxRetryPromptChars)
                break;
            sections.Insert(0, section);
            used += section.Length;
        }

        var sb = new StringBuilder(header);
        foreach (var s in sections)
            sb.Append(s);
        sb.Append(footer);

        return sb.ToString();
    }

    // ── Injection prompt (Swiss Cheese, per-zone) ─────────────────────────────

    public string BuildInjectionPrompt(InjectionPromptContext context)
    {
        var sb = new StringBuilder();

        var stackDescription = context.ProjectType switch
        {
            ProjectType.PythonReact =>
                "You are an expert Python (FastAPI, SQLAlchemy, Pydantic) and React (TypeScript, Tailwind) developer.",
            _ =>
                "You are an expert .NET 10 (Minimal API, Dapper, PostgreSQL) developer.",
        };

        sb.AppendLine(stackDescription);
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Fill exactly one LLM injection zone in `{context.FilePath}`.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Zone name: `{context.ZoneName}`");
        sb.AppendLine();
        sb.AppendLine("## Output Rules");
        sb.AppendLine("- Output ONLY the body of the zone — raw code only.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Your output replaces everything between [[LLM_INJECTION_START: {context.ZoneName}]] and [[LLM_INJECTION_END: {context.ZoneName}]].");
        sb.AppendLine("- Do NOT include the marker lines themselves.");
        sb.AppendLine("- Do NOT wrap the response in markdown fences (no ```).");
        sb.AppendLine("- Do NOT use [[FILE:...]] / [[END_FILE]] block syntax.");
        sb.AppendLine("- Do NOT add prose, comments outside the zone, or explanations.");
        sb.AppendLine("- Match the surrounding file's indentation and code style.");
        sb.AppendLine();
        sb.AppendLine("## Surrounding File (zone is between START/END markers)");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.Append(context.RenderedFileContent);
        if (!context.RenderedFileContent.EndsWith('\n'))
            sb.AppendLine();
        sb.AppendLine("```");

        if (context.Entity is not null)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Entity: {context.Entity.Name}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Table: `{context.Entity.TableName}`");
            if (context.Entity.Fields.Count > 0)
            {
                sb.AppendLine("- Fields:");
                foreach (var field in context.Entity.Fields)
                {
                    var pk = field.IsPrimaryKey ? " (PK)" : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - `{field.Name}` ({field.Type} / {field.SqlType}){pk}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Full Schema (for cross-entity references)");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(context.Schema, IndentedJson));
        sb.AppendLine("```");

        if (context.ProjectType == ProjectType.DotNetNextJs)
        {
            sb.AppendLine();
            sb.AppendLine("## .NET Constraints");
            sb.AppendLine("- Use Dapper for data access (not Entity Framework).");
            sb.AppendLine("- Use parameterized SQL — no string interpolation in queries.");
            sb.AppendLine("- All async methods use `await`. Never `.Result` or `.Wait()`.");
            sb.AppendLine("- Use `Guid` for IDs, `DateTime` for timestamps.");
            sb.AppendLine("- The `db` parameter (IDbConnectionFactory) is already in scope.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("## Python Constraints");
            sb.AppendLine("- Use SQLAlchemy ORM with async sessions.");
            sb.AppendLine("- Use Pydantic v2 for request/response schemas.");
            sb.AppendLine("- Type-annotate everything.");
        }

        return sb.ToString();
    }

    // ── Schema extraction prompt ──────────────────────────────────────────────

    public string BuildSchemaExtractionPrompt(string userPrompt)
    {
        return $$"""
            You are a software architect. Extract a data schema from the description below.

            ## Description
            {{userPrompt}}

            ## Output Format
            Respond with ONLY a valid JSON object — no prose, no explanation:

            {
              "entities": [
                {
                  "name": "EntityName",
                  "fields": [
                    { "name": "Id",        "type": "uuid",    "isPrimaryKey": true,  "nullable": false },
                    { "name": "FieldName", "type": "string",  "isPrimaryKey": false, "nullable": false }
                  ]
                }
              ],
              "relationships": [
                { "from": "EntityA", "to": "EntityB", "type": "one-to-many" }
              ]
            }

            Rules:
            - Include a UUID primary key for every entity.
            - Use snake_case-friendly names (PascalCase is fine for entity names).
            - Valid field types: uuid, string, integer, decimal, boolean, datetime.
            - Valid relationship types: one-to-many, many-to-many, one-to-one.
            """;
    }

    internal static string SanitizeUserInput(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input.Trim();

        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];

        sanitized = Regex.Replace(
            sanitized,
            @"\[\[FILE:[^\]]*\]\]",
            "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        sanitized = Regex.Replace(
            sanitized,
            @"\[\[END_FILE\]\]",
            "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var lines = sanitized
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith("##", StringComparison.Ordinal))
            .Select(line => new string(line.Where(ch => !char.IsControl(ch) || ch is '\r' or '\n' or '\t').ToArray()));

        sanitized = string.Join('\n', lines)
            .Trim()
            .Trim(ControlCharsToStrip);

        return sanitized;
    }
}
