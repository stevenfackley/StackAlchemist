using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Executes <c>dotnet build</c> on a generated project directory and extracts
/// structured error information for the retry-prompt pipeline.
/// </summary>
public interface ICompileService
{
    Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default);
    List<string> ExtractBuildErrors(string buildOutput);
    string BuildRetryContext(string originalPrompt, List<string> errorHistory, int retryAttempt);
}
