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
    BlueprintCompleted, // Tier 1 — schema/api-docs emitted, no codegen → straight to Packing
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

    /// <summary>
    /// One entry per compile attempt, oldest first. Source of <c>build-report.json</c> in the
    /// customer archive and of the per-half verdict the delivery UI badges.
    /// </summary>
    public List<BuildAttemptRecord> BuildAttempts { get; } = [];
    public string? OutputDirectory { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Resolved per-generation LLM routing (BYOK + per-user model). Set once at generation start
    /// by the orchestrator; the compile worker's build-repair loop reuses the SAME options so a
    /// repair call hits the same provider/model/key as the original codegen. Null = global config.
    /// </summary>
    public LlmCallOptions? LlmOptions { get; set; }
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
/// <paramref name="StopReason"/> carries the provider's stop reason verbatim
/// ("end_turn", "max_tokens", …) — "max_tokens" means the output was truncated
/// and must not be parsed as a complete response.
/// </summary>
public sealed record LlmResponse(
    string Text,
    int InputTokens,
    int OutputTokens,
    string Model,
    string? StopReason = null);

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

    /// <summary>
    /// Per-command breakdown of everything the strategy ran, in execution order.
    ///
    /// <see cref="StandardOutput"/> is a flat transcript, which is fine for the LLM repair
    /// prompt but cannot answer "did the Next.js half actually compile?" — the question the
    /// Compile Guarantee is selling an answer to. These entries are what
    /// <c>build-report.json</c> and the delivery UI's per-half badge are built from.
    ///
    /// Empty for strategies that have not been taught to record steps; consumers must treat
    /// an empty list as "no per-step detail available", never as "nothing ran".
    /// </summary>
    public IReadOnlyList<BuildStepResult> Steps { get; init; } = [];
}

/// <summary>
/// Which half of the deliverable a build step belongs to. Serialized into
/// <c>build-report.json</c>, so the wire values are part of the customer-facing contract
/// documented in <c>docs/advanced-docs/compile-guarantee.md</c>.
///
/// The halves are per stack: a <see cref="ProjectType.DotNetNextJs"/> archive has a .NET and
/// a Next.js half, a <see cref="ProjectType.PythonReact"/> archive has a FastAPI and a React
/// one. See <see cref="BuildHalves"/> for the mapping.
/// </summary>
public enum BuildHalf
{
    DotNet,
    NextJs,
    Python,
    React,
}

/// <summary>
/// The wire name and display label of every <see cref="BuildHalf"/>, and which halves each
/// <see cref="ProjectType"/> is verified in.
///
/// Single source of truth, because getting this wrong is customer-visible: the report used to
/// hardcode ".NET" and "Next.js" for every generation, so a FastAPI + React customer received a
/// <c>build-report.json</c> describing two halves of a stack they never chose — both of them
/// "not_run", under a top-level <c>"status": "verified"</c>.
///
/// The wire names are the published contract in <c>docs/advanced-docs/compile-guarantee.md</c>;
/// renaming one is a breaking change to a document customers are sold on.
/// </summary>
public static class BuildHalves
{
    private static readonly BuildHalf[] DotNetNextJsHalves = [BuildHalf.DotNet, BuildHalf.NextJs];
    private static readonly BuildHalf[] PythonReactHalves = [BuildHalf.Python, BuildHalf.React];

    private static readonly Dictionary<BuildHalf, (string Wire, string Label)> Descriptors = new()
    {
        [BuildHalf.DotNet] = ("dotnet", ".NET"),
        [BuildHalf.NextJs] = ("nextjs", "Next.js"),
        [BuildHalf.Python] = ("python", "FastAPI"),
        [BuildHalf.React] = ("react", "React"),
    };

    /// <summary>Value of the <c>half</c> field in <c>build-report.json</c>.</summary>
    public static string WireName(BuildHalf half) => Descriptors[half].Wire;

    /// <summary>Display name the delivery page badges, e.g. ".NET" / "FastAPI".</summary>
    public static string Label(BuildHalf half) => Descriptors[half].Label;

    /// <summary>
    /// The halves a generation of this project type is verified in, in build order.
    ///
    /// An unmapped project type reports NO halves rather than borrowing another stack's: an
    /// empty <c>halves</c> array reads as "no per-half verdict recorded", which the delivery
    /// page renders honestly, whereas defaulting to .NET + Next.js is the exact bug this map
    /// was introduced to kill.
    /// </summary>
    public static IReadOnlyList<BuildHalf> For(ProjectType projectType) => projectType switch
    {
        ProjectType.DotNetNextJs => DotNetNextJsHalves,
        ProjectType.PythonReact => PythonReactHalves,
        _ => [],
    };
}

/// <summary>Outcome of a single external command inside a build attempt.</summary>
public sealed record BuildStepResult
{
    /// <summary>Which half of the archive this command verified.</summary>
    public required BuildHalf Half { get; init; }

    /// <summary>
    /// The command as the customer would type it (e.g. <c>npm run build</c>) — never the
    /// resolved absolute launcher path, which leaks the build host's filesystem layout.
    /// </summary>
    public required string Command { get; init; }

    public required int ExitCode { get; init; }

    public required long DurationMs { get; init; }

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    /// <summary>
    /// True when the step was not run at all (e.g. no <c>nextjs/</c> directory, or the
    /// template has no <c>typecheck</c> script). A skipped step is not a failure, but it is
    /// also not evidence of compilation — the UI must not badge it as verified.
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// True when this step failed but a later step re-attempted the same work, so its exit
    /// code did not decide anything — <c>npm ci</c> hard-failing on a desynced lockfile and
    /// being retried as <c>npm install</c> is the documented common case.
    ///
    /// It stays in the report as evidence of what ran, but it must not condemn its half:
    /// without this flag an archive whose frontend compiled perfectly shipped a
    /// <c>build-report.json</c> reading <c>"status": "verified"</c> next to
    /// <c>halves[nextjs].status: "failed"</c>, and the delivery page rendered "compiled and
    /// verified" directly above a red "Next.js failed to compile".
    /// </summary>
    public bool Superseded { get; init; }

    public bool IsSuccess => !Skipped && ExitCode == 0;
}
