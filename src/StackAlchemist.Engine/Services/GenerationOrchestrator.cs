using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Telemetry;

namespace StackAlchemist.Engine.Services;

public interface IGenerationOrchestrator
{
    Task<GenerateResponse> EnqueueAsync(GenerateRequest request, CancellationToken ct = default);
}

/// <summary>
/// Coordinates the generation pipeline:
/// 1. Load templates
/// 2. Render Handlebars variables
/// 3. Call LLM (or mock) to generate code
/// 4. Reconstruct files by parsing LLM output and injecting into templates
/// 5. Write output to temp directory
/// 6. Push job to compile worker queue
/// </summary>
public sealed class GenerationOrchestrator(
    ITemplateProvider templateProvider,
    IReconstructionService reconstructionService,
    ILlmClient llmClient,
    IPromptBuilderService promptBuilder,
    IInjectionEngine injectionEngine,
    IDeliveryService deliveryService,
    IFileSystem fileSystem,
    IConfiguration configuration,
    ChannelWriter<GenerationContext> jobQueue,
    ILogger<GenerationOrchestrator> logger) : IGenerationOrchestrator
{
    private const string Tier3InfrastructureTemplateSet = "Tier3-Infrastructure";

    private bool UseSwissCheese => configuration.GetValue("Generation:UseSwissCheese", false);

    private static string ResolveTemplateSet(ProjectType projectType, bool useSwissCheese) => (projectType, useSwissCheese) switch
    {
        (ProjectType.DotNetNextJs, true) => "V2-DotNet-NextJs",
        (ProjectType.DotNetNextJs, false) => "V1-DotNet-NextJs",
        (ProjectType.PythonReact, _) => "V1-Python-React",  // V2 not yet authored for Python-React
        _ => "V1-DotNet-NextJs",
    };

    public async Task<GenerateResponse> EnqueueAsync(GenerateRequest request, CancellationToken ct = default)
    {
        var context = new GenerationContext
        {
            GenerationId = request.GenerationId,
            Mode = request.Mode,
            Tier = request.Tier,
            ProjectType = request.ProjectType,
            Prompt = request.Prompt,
            Schema = request.Schema,
            Personalization = request.Personalization,
        };

        Meters.Started.Add(1, BuildTags(request));

        // Transition: pending → generating
        context.State = GenerationStateMachine.Transition(
            context.State, GenerationEvent.EnginePickedUp, context);

        logger.LogInformation("Generation {Id} started — mode={Mode}, tier={Tier}",
            request.GenerationId, request.Mode, request.Tier);

        try
        {
            // Step 1: Load and render templates
            var swissCheese = UseSwissCheese;
            var templateSet = ResolveTemplateSet(request.ProjectType, swissCheese);
            var rawTemplates = templateProvider.LoadTemplate(templateSet);
            var variables = BuildVariables(request);
            var renderedTemplates = templateProvider.Render(rawTemplates, variables);

            Dictionary<string, string> finalFiles;
            if (swissCheese)
            {
                // Swiss Cheese: per-zone parallel LLM dispatch.
                var injection = await injectionEngine.FillZonesAsync(
                    renderedTemplates,
                    request.Schema ?? new GenerationSchema(),
                    variables,
                    request.ProjectType,
                    request.Personalization,
                    ct);

                await deliveryService.UpdateTokenUsageAsync(
                    request.GenerationId,
                    injection.TotalInputTokens,
                    injection.TotalOutputTokens,
                    injection.Model,
                    ct);

                finalFiles = injection.FilledTemplates;

                logger.LogInformation(
                    "Generation {Id} Swiss Cheese: filled {Zones} zones",
                    request.GenerationId, injection.ZonesFilled);
            }
            else
            {
                // V1 one-shot path: whole-codebase LLM call + Reconstruct.
                var systemPrompt = LoadPromptTemplate(request);
                var userPrompt = BuildUserPrompt(request);
                var llmResponse = await llmClient.GenerateAsync(systemPrompt, userPrompt, ct);
                await deliveryService.UpdateTokenUsageAsync(
                    request.GenerationId,
                    llmResponse.InputTokens,
                    llmResponse.OutputTokens,
                    llmResponse.Model,
                    ct);

                var llmBlocks = reconstructionService.Parse(llmResponse.Text);
                finalFiles = reconstructionService.Reconstruct(renderedTemplates, llmBlocks, templateProvider);
            }

            AppendTier3InfrastructureFiles(request, variables, finalFiles);

            // Step 5: Write to temp directory
            var outputDir = WriteFilesToDisk(request.GenerationId, finalFiles);
            context.OutputDirectory = outputDir;

            // Transition: generating → building
            context.State = GenerationStateMachine.Transition(
                context.State, GenerationEvent.CodeReconstructed, context);

            // Step 6: Push to compile worker queue
            await jobQueue.WriteAsync(context, ct);

            logger.LogInformation("Generation {Id} enqueued for compilation at {Dir}",
                request.GenerationId, outputDir);
        }
        catch (Exception ex)
        {
            context.State = GenerationState.Failed;
            context.ErrorMessage = ex.Message;
            logger.LogError(ex, "Generation {Id} failed during orchestration", request.GenerationId);

            Meters.Failed.Add(1, BuildTags(request, stage: "orchestration"));
            Meters.DurationMs.Record(
                (DateTimeOffset.UtcNow - context.StartedAt).TotalMilliseconds,
                BuildTags(request, stage: "orchestration", outcome: "failed"));
        }

        return new GenerateResponse
        {
            JobId = request.GenerationId,
            Status = context.State.ToString().ToLowerInvariant(),
            ProjectType = request.ProjectType,
        };
    }

    private static TagList BuildTags(GenerateRequest request, string? stage = null, string? outcome = null)
    {
        var t = new TagList
        {
            { "tier", request.Tier },
            { "project_type", request.ProjectType.ToString() },
            { "mode", request.Mode },
        };
        if (stage is not null) t.Add("stage", stage);
        if (outcome is not null) t.Add("outcome", outcome);
        return t;
    }

    private void AppendTier3InfrastructureFiles(
        GenerateRequest request,
        TemplateVariables variables,
        Dictionary<string, string> finalFiles)
    {
        if (request.Tier != 3)
        {
            return;
        }

        var infrastructureTemplates = templateProvider.LoadTemplate(Tier3InfrastructureTemplateSet);
        var renderedInfrastructureFiles = templateProvider.Render(infrastructureTemplates, variables);

        foreach (var (path, content) in renderedInfrastructureFiles)
        {
            finalFiles[path] = content;
        }

        logger.LogInformation(
            "Generation {Id} appended {Count} Tier 3 infrastructure files",
            request.GenerationId,
            renderedInfrastructureFiles.Count);
    }

    private static TemplateVariables BuildVariables(GenerateRequest request)
    {
        var projectName = DeriveProjectName(request);
        var kebab = ToKebabCase(projectName);
        return new TemplateVariables
        {
            ProjectName = projectName,
            ProjectNameKebab = kebab,
            ProjectNameLower = projectName.ToLowerInvariant(),
            DbConnectionString = $"Host=localhost;Port=5432;Database={projectName.ToLowerInvariant()};Username=postgres;Password=postgres",
            FrontendUrl = "http://localhost:3000",
            Entities = request.Schema?.Entities.Select(e => new TemplateEntity
            {
                Name = e.Name,
                NameLower = e.Name.ToLowerInvariant(),
                TableName = e.Name.ToLowerInvariant() + "s",
                Fields = e.Fields.Select(f => new TemplateField
                {
                    Name = f.Name,
                    NameLower = f.Name.ToLowerInvariant(),
                    Type = MapFieldType(f.Type),
                    SqlType = MapSqlType(f.Type),
                    IsPrimaryKey = f.Pk,
                }).ToList(),
            }).ToList() ?? [],
        };
    }

    /// <summary>
    /// Derives a PascalCase project name from the schema entities (preferred) or
    /// the natural-language prompt (Simple Mode fallback).
    /// </summary>
    private static string DeriveProjectName(GenerateRequest request)
    {
        // 0. Explicit personalization project name takes highest priority
        if (!string.IsNullOrWhiteSpace(request.Personalization?.ProjectName))
        {
            var name = request.Personalization.ProjectName.Trim();
            // Sanitize to PascalCase identifier
            var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9]", "");
            if (sanitized.Length > 0)
                return char.ToUpperInvariant(sanitized[0]) + sanitized[1..];
        }

        // 1. Schema entities — most reliable signal
        var entities = request.Schema?.Entities;
        if (entities is { Count: > 0 })
        {
            return entities.Count == 1
                ? $"{entities[0].Name}App"
                : $"{entities[0].Name}{entities[1].Name}";
        }

        // 2. Natural-language prompt (Simple Mode)
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            return ExtractProjectNameFromPrompt(request.Prompt);

        return "GeneratedApp";
    }

    private static readonly HashSet<string> StopWords =
        ["a", "an", "the", "for", "and", "or", "of", "to", "in", "on", "with",
         "that", "is", "are", "app", "application", "system", "platform", "tool",
         "build", "create", "generate", "make", "i", "want", "need", "my"];

    private static string ExtractProjectNameFromPrompt(string prompt)
    {
        var words = prompt
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Where(w => !StopWords.Contains(w.ToLowerInvariant()))
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant())
            .ToArray();

        return words.Length > 0 ? string.Join("", words) : "GeneratedApp";
    }

    private static string ToKebabCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
            return pascal;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(pascal[i]));
        }
        return sb.ToString();
    }

    private string LoadPromptTemplate(GenerateRequest request)
    {
        if (request.Schema is not null)
            return promptBuilder.BuildGenerationPrompt(request.Schema, request.ProjectType, request.Personalization);

        var promptPath = fileSystem.Path.Combine(
            AppContext.BaseDirectory, "Prompts", "V1-generation.md");

        if (!fileSystem.File.Exists(promptPath))
        {
            // Fallback: try relative to working directory
            promptPath = fileSystem.Path.Combine("Prompts", "V1-generation.md");
        }

        if (fileSystem.File.Exists(promptPath))
        {
            var template = fileSystem.File.ReadAllText(promptPath);
            var schemaJson = request.Schema is not null
                ? JsonSerializer.Serialize(request.Schema, new JsonSerializerOptions { WriteIndented = true })
                : "{}";
            return template
                .Replace("{{SCHEMA_JSON}}", schemaJson)
                .Replace("{{PROJECT_NAME}}", "GeneratedApp");
        }

        // Inline fallback for testing
        return "Generate code for the provided schema using [[FILE:path]]...[[END_FILE]] format.";
    }

    private static string BuildUserPrompt(GenerateRequest request)
    {
        if (request.Mode == "simple" && request.Prompt is not null)
        {
            return $"Generate a full-stack application based on this description:\n\n{request.Prompt}";
        }

        if (request.Schema is not null)
        {
            return $"Generate code for this schema:\n\n{JsonSerializer.Serialize(request.Schema, new JsonSerializerOptions { WriteIndented = true })}";
        }

        return "Generate a sample CRUD application with a Product entity.";
    }

    private string WriteFilesToDisk(string generationId, Dictionary<string, string> files)
    {
        var baseDir = fileSystem.Path.Combine(
            fileSystem.Path.GetTempPath(), "stackalchemist", generationId);

        if (fileSystem.Directory.Exists(baseDir))
        {
            fileSystem.Directory.Delete(baseDir, recursive: true);
        }

        foreach (var (relativePath, content) in files)
        {
            var fullPath = fileSystem.Path.Combine(baseDir, relativePath);
            var dir = fileSystem.Path.GetDirectoryName(fullPath)!;
            fileSystem.Directory.CreateDirectory(dir);
            fileSystem.File.WriteAllText(fullPath, content);
        }

        return baseDir;
    }

    private static string MapFieldType(string schemaType) => schemaType.ToLowerInvariant() switch
    {
        "string" or "text" => "string",
        "int" or "integer" => "int",
        "decimal" or "number" or "float" => "decimal",
        "bool" or "boolean" => "bool",
        "guid" or "uuid" => "Guid",
        "datetime" or "date" => "DateTime",
        _ => "string",
    };

    private static string MapSqlType(string schemaType) => schemaType.ToLowerInvariant() switch
    {
        "string" or "text" => "TEXT",
        "int" or "integer" => "INTEGER",
        "decimal" or "number" => "NUMERIC(10,2)",
        "float" => "REAL",
        "bool" or "boolean" => "BOOLEAN",
        "guid" or "uuid" => "UUID",
        "datetime" or "date" => "TIMESTAMPTZ",
        _ => "TEXT",
    };
}
