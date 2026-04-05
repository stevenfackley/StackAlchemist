using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface IBuildStrategy
{
    ProjectType SupportedProjectType { get; }
    Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default);
    List<string> ExtractBuildErrors(string buildOutput);
}
