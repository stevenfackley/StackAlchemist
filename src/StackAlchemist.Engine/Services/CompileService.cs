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

    public string BuildRetryContext(string originalPrompt, List<string> errorHistory, int retryAttempt)
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

        for (var i = 0; i < includedErrors.Count; i++)
            context += $"### Attempt {i + 1}\n```\n{includedErrors[i]}\n```\n\n";

        context += $"""


            ## Instructions
            Fix ALL build errors listed above. Output the corrected files using the same [[FILE:path]]...[[END_FILE]] format.
            Only output files that need changes — do not repeat unchanged files.

            ## Original Prompt
            {originalPrompt}
            """;

        return context;
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
