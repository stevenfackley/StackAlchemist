using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Selects the appropriate ecosystem-specific build strategy and constructs retry prompts
/// for the LLM repair loop.
/// </summary>
public sealed partial class CompileService(
    IEnumerable<IBuildStrategy> strategies,
    ILogger<CompileService> logger) : ICompileService
{
    private const int MaxContextChars = 8_000;

    /// <summary>Floor for the truncation fallback, so the prompt always carries some error text.</summary>
    private const int MinTruncatedErrorChars = 1_000;

    private readonly Dictionary<ProjectType, IBuildStrategy> _strategies = strategies
        .GroupBy(strategy => strategy.SupportedProjectType)
        .ToDictionary(group => group.Key, group => group.Last());

    public Task<BuildResult> ExecuteBuildAsync(
        string projectDirectory,
        ProjectType projectType,
        CancellationToken ct = default)
    {
        return ResolveStrategy(projectType).ExecuteBuildAsync(projectDirectory, ct);
    }

    public List<string> ExtractBuildErrors(string buildOutput, ProjectType projectType)
    {
        return ResolveStrategy(projectType).ExtractBuildErrors(buildOutput);
    }

    public string BuildRetryContext(
        string originalPrompt,
        List<string> errorHistory,
        int retryAttempt,
        IReadOnlyCollection<string>? treeRoots = null)
    {
        var context = $"""
            The previous code generation attempt failed to compile. This is retry attempt {retryAttempt} of 3.

            ## Build Errors from Previous Attempts

            """;

        var totalLength = context.Length + originalPrompt.Length + 200;
        var includedErrors = new List<string>();

        for (var i = errorHistory.Count - 1; i >= 0; i--)
        {
            if (totalLength + errorHistory[i].Length > MaxContextChars)
                break;

            includedErrors.Insert(0, errorHistory[i]);
            totalLength += errorHistory[i].Length;
        }

        // Nothing fit: a single `next build` failure carrying file paths and code frames
        // can exceed the whole budget on its own. Truncate the most recent attempt instead
        // of emitting a retry prompt with an empty error section — that shape teaches the
        // LLM nothing, burns all three attempts, and ends in a Compile Guarantee refund.
        if (includedErrors.Count == 0 && errorHistory.Count > 0)
        {
            var budget = Math.Max(MinTruncatedErrorChars, MaxContextChars - totalLength);
            includedErrors.Add(Truncate(errorHistory[^1], budget));
        }

        for (var i = 0; i < includedErrors.Count; i++)
            context += $"### Attempt {i + 1}\n```\n{includedErrors[i]}\n```\n\n";

        context += $"""


            ## Instructions
            Fix ALL build errors listed above. Output the corrected files using the same [[FILE:path]]...[[END_FILE]] format.
            Only output files that need changes — do not repeat unchanged files.
            {BuildPathRules(treeRoots)}
            ## Original Prompt
            {originalPrompt}
            """;

        return context;
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars] + "\n… (errors truncated)";

    /// <summary>
    /// The path contract, restated on every repair attempt.
    ///
    /// A correction is merged back into a tree that already exists, and a block whose path
    /// falls outside it is dropped rather than written — so a model that answers with
    /// <c>src/lib/api.ts</c> gets no fix applied, fails the next build on the same errors, and
    /// walks the generation one step closer to a Compile Guarantee refund. The rule is cheap to
    /// state and the roots are known, so state them.
    /// </summary>
    private static string BuildPathRules(IReadOnlyCollection<string>? treeRoots)
    {
        if (treeRoots is null or { Count: 0 })
            return string.Empty;

        var roots = string.Join(", ", treeRoots.Select(r => $"`{r}/`"));

        return $"""


            ## Paths Are Not Negotiable
            Your corrections are merged into the EXISTING project tree, whose top-level directories are: {roots}.
            Every [[FILE:...]] path MUST start with one of them — in particular the frontend lives under
            `nextjs/src/...`, NOT `src/...`. A path starting anywhere else is REJECTED and never written,
            so the build fails again on the exact same errors.
            Do NOT emit `__zone__/...` blocks in a correction: the injection zones are already resolved,
            and a zone block at this stage can only be discarded. Emit the whole file instead.

            """;
    }

    private IBuildStrategy ResolveStrategy(ProjectType projectType)
    {
        if (_strategies.TryGetValue(projectType, out var strategy))
            return strategy;

        LogNoBuildStrategy(logger, projectType);
        throw new ArgumentOutOfRangeException(nameof(projectType), projectType, "Unsupported project type");
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "No build strategy registered for project type {ProjectType}")]
    private static partial void LogNoBuildStrategy(ILogger logger, ProjectType projectType);
}
