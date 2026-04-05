using System.Text;
using System.Text.Json;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface IPromptBuilderService
{
    string BuildGenerationPrompt(GenerationSchema schema, ProjectType projectType = ProjectType.DotNetNextJs);
    string BuildRetryPrompt(string originalPrompt, IReadOnlyList<string> errorHistory, int retryAttempt);
    string BuildSchemaExtractionPrompt(string userPrompt);
}

/// <summary>
/// Constructs structured prompts sent to Claude 3.5 Sonnet (or local Ollama in dev).
/// All code-generation prompts enforce the [[FILE:path]]…[[END_FILE]] output format.
/// </summary>
public sealed class PromptBuilderService : IPromptBuilderService
{
    // Rough budget: keep well under Claude 3.5's 200 k context window.
    private const int MaxRetryPromptChars = 8_000;

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    // ── Generation prompt ─────────────────────────────────────────────────────

    public string BuildGenerationPrompt(GenerationSchema schema, ProjectType projectType = ProjectType.DotNetNextJs)
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
                sb.AppendLine($"- {entity.Name}");
        }

        if (schema.Relationships.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Relationships");
            foreach (var rel in schema.Relationships)
                sb.AppendLine($"- {rel.From} → {rel.To} ({rel.Type})");
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
}
