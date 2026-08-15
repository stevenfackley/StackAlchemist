using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Executes a build command on a generated project directory and extracts
/// structured error information for the retry-prompt pipeline.
/// </summary>
public interface ICompileService
{
    Task<BuildResult> ExecuteBuildAsync(string projectDirectory, ProjectType projectType, CancellationToken ct = default);
    List<string> ExtractBuildErrors(string buildOutput, ProjectType projectType);
    /// <param name="treeRoots">
    /// Top-level directories of the generated project the corrections are merged back into.
    /// Supplied so the retry prompt can state where files may go: <paramref name="originalPrompt"/>
    /// is the user's natural-language brief and names no paths at all, which made this the only
    /// prompt in the pipeline that asked for <c>[[FILE:path]]</c> blocks without saying what a
    /// valid path is. Pass empty to omit the rule (nothing to state).
    /// </param>
    string BuildRetryContext(
        string originalPrompt,
        List<string> errorHistory,
        int retryAttempt,
        IReadOnlyCollection<string>? treeRoots = null);
}
