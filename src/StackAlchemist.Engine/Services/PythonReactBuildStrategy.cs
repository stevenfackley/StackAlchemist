using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public sealed partial class PythonReactBuildStrategy(ILogger<PythonReactBuildStrategy> logger)
    : BuildStrategyBase(logger)
{
    public override ProjectType SupportedProjectType => ProjectType.PythonReact;

    public override async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        var pythonDir = Path.Combine(projectDirectory, "backend");
        var reactDir = Path.Combine(projectDirectory, "frontend");

        if (Directory.Exists(pythonDir))
        {
            Logger.LogInformation("Running Python validation in {Dir}", pythonDir);

            var pipResult = await RunProcessAsync("python", "-m pip install -r requirements.txt --quiet", pythonDir, ct);
            if (!pipResult.IsSuccess)
                return pipResult;

            var flake8Result = await RunProcessAsync("python", "-m flake8 .", pythonDir, ct);
            if (!flake8Result.IsSuccess)
                return flake8Result;

            var pytestResult = await RunProcessAsync("python", "-m pytest --collect-only -q", pythonDir, ct);
            if (!pytestResult.IsSuccess)
                return pytestResult;
        }

        if (Directory.Exists(reactDir))
        {
            Logger.LogInformation("Running React validation in {Dir}", reactDir);

            var npmInstallResult = await RunProcessAsync("npm", "install --silent", reactDir, ct);
            if (!npmInstallResult.IsSuccess)
                return npmInstallResult;

            var eslintResult = await RunProcessAsync("npm", "run lint -- --max-warnings=0", reactDir, ct);
            if (!eslintResult.IsSuccess)
                return eslintResult;

            var tscResult = await RunProcessAsync("npx", "tsc --noEmit", reactDir, ct);
            if (!tscResult.IsSuccess)
                return tscResult;
        }

        return new BuildResult
        {
            ExitCode = 0,
            StandardOutput = "All Python and React checks passed.",
            ErrorOutput = string.Empty,
        };
    }

    public override List<string> ExtractBuildErrors(string buildOutput)
    {
        return PythonAndFrontendErrorRegex()
            .Matches(buildOutput)
            .Select(match => match.Value.Trim())
            .ToList();
    }

    [GeneratedRegex(
        @"^.+:\d+:\d+:\s*(?:E|W|F|C)\d+\s+.+$|^\s*\d+:\d+\s+error\s+.+$|^.+:\s*error\s+TS\d+:.+$|^ERROR\s+.+$|^FAILED\s+.+$",
        RegexOptions.Multiline)]
    private static partial Regex PythonAndFrontendErrorRegex();
}
