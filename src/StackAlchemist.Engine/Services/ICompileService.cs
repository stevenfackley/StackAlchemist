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
    string BuildRetryContext(string originalPrompt, List<string> errorHistory, int retryAttempt);
}
