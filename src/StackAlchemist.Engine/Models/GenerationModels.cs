namespace StackAlchemist.Engine.Models;

/// <summary>
/// State machine for the generation pipeline.
/// pending → generating → building → success | failed
/// </summary>
public enum GenerationState
{
    Pending,
    Generating,
    Building,
    Packing,
    Uploading,
    Success,
    Failed,
}

public enum GenerationEvent
{
    EnginePickedUp,
    CodeReconstructed,
    BuildPassed,
    BuildFailed,
    ZipCreated,
    UploadedToR2,
}

public enum ProjectType
{
    DotNetNextJs,
    PythonReact,
}

/// <summary>
/// Mutable context carried through the generation pipeline.
/// </summary>
public sealed class GenerationContext
{
    public required string GenerationId { get; init; }
    public required string Mode { get; init; } // "simple" | "advanced"
    public required int Tier { get; init; }
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
    public string? Prompt { get; init; }
    public GenerationSchema? Schema { get; init; }
    public GenerationPersonalization? Personalization { get; init; }
    public GenerationState State { get; set; } = GenerationState.Pending;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public int RetryCount { get; set; }
    public List<string> BuildErrorHistory { get; } = [];
    public string? OutputDirectory { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Mirrors the frontend GenerationSchema type.
/// </summary>
public sealed class GenerationSchema
{
    public List<SchemaEntity> Entities { get; init; } = [];
    public List<SchemaRelationship> Relationships { get; init; } = [];
    public List<SchemaEndpoint> Endpoints { get; init; } = [];
}

public sealed class SchemaEntity
{
    public required string Name { get; init; }
    public List<SchemaField> Fields { get; init; } = [];
}

public sealed class SchemaField
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Pk { get; init; }
    public bool Nullable { get; init; }
    public string? Default { get; init; }
}

public sealed class SchemaRelationship
{
    public required string From { get; init; }
    public required string Type { get; init; }
    public required string To { get; init; }
}

public sealed class SchemaEndpoint
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required string Entity { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// A single file block parsed from LLM output.
/// </summary>
public sealed record LlmOutputBlock(string FilePath, string Content);

/// <summary>
/// Structured response from an LLM call, including token accounting.
/// </summary>
public sealed record LlmResponse(
    string Text,
    int InputTokens,
    int OutputTokens,
    string Model);

/// <summary>
/// Variables for Handlebars template rendering.
/// </summary>
public sealed class TemplateVariables
{
    public required string ProjectName { get; init; }
    public required string ProjectNameKebab { get; init; }
    public required string ProjectNameLower { get; init; }
    public required string DbConnectionString { get; init; }
    public required string FrontendUrl { get; init; }
    public List<TemplateEntity> Entities { get; init; } = [];
}

public sealed class TemplateEntity
{
    public required string Name { get; init; }
    public required string NameLower { get; init; }
    public required string TableName { get; init; }
    public List<TemplateField> Fields { get; init; } = [];
}

public sealed class TemplateField
{
    public required string Name { get; init; }
    public required string NameLower { get; init; }
    public required string Type { get; init; }
    public required string SqlType { get; init; }
    public bool IsPrimaryKey { get; init; }
}

/// <summary>
/// Request to the Engine /api/extract-schema endpoint (Simple Mode).
/// </summary>
public sealed class ExtractSchemaRequest
{
    public required string GenerationId { get; init; }
    public required string Prompt { get; init; }
}

/// <summary>
/// Response from the Engine /api/extract-schema endpoint.
/// </summary>
public sealed class ExtractSchemaResponse
{
    public required string GenerationId { get; init; }
    public required GenerationSchema Schema { get; init; }
}

/// <summary>
/// Request to create a Stripe Checkout Session (paid tiers 1–3).
/// </summary>
public sealed class CreateCheckoutSessionRequest
{
    public required string GenerationId { get; init; }
    public required int Tier { get; init; }
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
    public required string SuccessUrl { get; init; }
    public required string CancelUrl { get; init; }
    public string? Prompt { get; init; }
}

/// <summary>
/// Response from the Stripe Checkout Session creation endpoint.
/// </summary>
public sealed class CreateCheckoutSessionResponse
{
    public required string SessionId { get; init; }
    public required string Url { get; init; }
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
}

/// <summary>
/// Personalization data collected from the wizard before generation.
/// Stored in generations.personalization_json and injected into prompts + templates.
/// </summary>
public sealed class GenerationPersonalization
{
    public string BusinessDescription { get; init; } = "";
    public string? ProjectName { get; init; }
    public string? Tagline { get; init; }
    public PersonalizationColorScheme? ColorScheme { get; init; }
    /// <summary>Entity name → domain description (e.g. "Order" → "a food delivery order")</summary>
    public Dictionary<string, string> DomainContext { get; init; } = [];
    public PersonalizationFeatureFlags? FeatureFlags { get; init; }
}

public sealed class PersonalizationColorScheme
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Primary { get; init; } = "#2563EB";
    public string Secondary { get; init; } = "#1D4ED8";
    public string Accent { get; init; } = "#60A5FA";
    public string Background { get; init; } = "#0F172A";
    public string Surface { get; init; } = "#1E293B";
}

public sealed class PersonalizationFeatureFlags
{
    public string AuthMethod { get; init; } = "jwt"; // jwt | cookie | oauth | none
    public bool SoftDelete { get; init; }
    public bool AuditTimestamps { get; init; } = true;
    public bool IncludeSwagger { get; init; } = true;
    public bool IncludeDockerCompose { get; init; } = true;
}

/// <summary>
/// Request to the Engine /api/generate endpoint.
/// </summary>
public sealed class GenerateRequest
{
    public required string GenerationId { get; init; }
    public required string Mode { get; init; }
    public required int Tier { get; init; }
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
    public string? Prompt { get; init; }
    public GenerationSchema? Schema { get; init; }
    public GenerationPersonalization? Personalization { get; init; }
}

/// <summary>
/// Response from the Engine /api/generate endpoint.
/// </summary>
public sealed class GenerateResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
}

/// <summary>
/// Aggregate result from running the Swiss Cheese injection path: filled templates
/// plus accumulated token usage across all per-zone LLM calls.
/// </summary>
public sealed record InjectionResult(
    Dictionary<string, string> FilledTemplates,
    int TotalInputTokens,
    int TotalOutputTokens,
    string Model,
    int ZonesFilled);

/// <summary>
/// Per-zone targeted prompt context for the Swiss Cheese injection path.
/// One context = one LLM call that fills a single zone in a rendered template file.
/// </summary>
public sealed record InjectionPromptContext(
    string FilePath,
    string ZoneName,
    string RenderedFileContent,
    GenerationSchema Schema)
{
    public ProjectType ProjectType { get; init; } = ProjectType.DotNetNextJs;
    public GenerationPersonalization? Personalization { get; init; }
    public TemplateEntity? Entity { get; init; }
}

/// <summary>
/// Result of running a build command.
/// </summary>
public sealed class BuildResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string ErrorOutput { get; init; }
    public bool IsSuccess => ExitCode == 0;
}
