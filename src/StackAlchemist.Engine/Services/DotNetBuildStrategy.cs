using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public sealed partial class DotNetBuildStrategy(ILogger<DotNetBuildStrategy> logger)
    : BuildStrategyBase(logger)
{
    public override ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

    public override async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        var dotnetDir = Path.Combine(projectDirectory, "dotnet");
        if (!Directory.Exists(dotnetDir))
            dotnetDir = projectDirectory;

        LogRunningBuild(Logger, dotnetDir);
        return await RunProcessAsync("dotnet", "build --no-restore", dotnetDir, ct);
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Running dotnet build in {Dir}")]
    private static partial void LogRunningBuild(ILogger logger, string dir);

    public override List<string> ExtractBuildErrors(string buildOutput)
    {
        return CSharpErrorRegex()
            .Matches(buildOutput)
            .Select(match => match.Value.Trim())
            .ToList();
    }

    [GeneratedRegex(@"^.+:\s*error\s+CS\d+:.+$", RegexOptions.Multiline)]
    private static partial Regex CSharpErrorRegex();
}
