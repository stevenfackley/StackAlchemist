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

        // Restore as its own process invocation (not `dotnet build` implicitly restoring)
        // so a restore failure (missing feed, NU1101, etc.) is distinguishable in the log
        // from a compile failure, and so `build --no-restore` below never runs against a
        // freshly-written temp dir that was never restored (was NETSDK1004 every time).
        LogRunningRestore(Logger, dotnetDir);
        var restoreResult = await RunProcessAsync("dotnet", "restore", dotnetDir, ct);
        if (!restoreResult.IsSuccess)
            return restoreResult;

        LogRunningBuild(Logger, dotnetDir);
        return await RunProcessAsync("dotnet", "build --no-restore", dotnetDir, ct);
    }

    [LoggerMessage(EventId = 1099, Level = LogLevel.Information, Message = "Running dotnet restore in {Dir}")]
    private static partial void LogRunningRestore(ILogger logger, string dir);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Running dotnet build in {Dir}")]
    private static partial void LogRunningBuild(ILogger logger, string dir);

    public override List<string> ExtractBuildErrors(string buildOutput)
    {
        return DotNetErrorRegex()
            .Matches(buildOutput)
            .Select(match => match.Value.Trim())
            .ToList();
    }

    // Covers compiler errors (CS), SDK resolution failures (NETSDK, e.g. missing
    // restore), MSBuild engine errors (MSB), and NuGet restore errors (NU) — the four
    // families `dotnet restore`/`dotnet build` actually emit for this pipeline.
    [GeneratedRegex(@"^(?:.+:\s*)?error\s+(?:CS|NETSDK|MSB|NU)\d+:.+$", RegexOptions.Multiline)]
    private static partial Regex DotNetErrorRegex();
}
