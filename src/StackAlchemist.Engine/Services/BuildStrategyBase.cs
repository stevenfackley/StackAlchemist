using System.Diagnostics;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public abstract class BuildStrategyBase(ILogger logger) : IBuildStrategy
{
    public abstract ProjectType SupportedProjectType { get; }

    public abstract Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default);

    public abstract List<string> ExtractBuildErrors(string buildOutput);

    protected ILogger Logger { get; } = logger;

    protected static async Task<BuildResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new BuildResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            ErrorOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
        };
    }
}
