using System.Diagnostics;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Runs <c>dotnet build</c> in the generated project directory, extracts C# compiler
/// error lines, and constructs retry prompts for the LLM repair loop.
/// </summary>
public sealed partial class CompileService(ILogger<CompileService> logger) : ICompileService
{
    private const int MaxContextChars = 8_000; // ~2 000 tokens budget for error context

    public async Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
    {
        // Prefer the dotnet/ subdirectory if it exists (template layout)
        var dotnetDir = Path.Combine(projectDirectory, "dotnet");
        if (!Directory.Exists(dotnetDir))
            dotnetDir = projectDirectory;

        logger.LogInformation("Running dotnet build in {Dir}", dotnetDir);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --no-restore",
            WorkingDirectory = dotnetDir,
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

        var result = new BuildResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            ErrorOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
        };

        if (result.IsSuccess)
            logger.LogInformation("Build succeeded in {Dir}", dotnetDir);
        else
            logger.LogWarning("Build failed in {Dir} — exit code {Code}", dotnetDir, result.ExitCode);

        return result;
    }

    public List<string> ExtractBuildErrors(string buildOutput)
        => BuildErrorRegex()
            .Matches(buildOutput)
            .Select(m => m.Value.Trim())
            .ToList();

    public string BuildRetryContext(string originalPrompt, List<string> errorHistory, int retryAttempt)
    {
        var context = $"""
            The previous code generation attempt failed to compile. This is retry attempt {retryAttempt} of 3.

            ## Build Errors from Previous Attempts

            """;

        // Include errors newest-first, respecting the char budget
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

    [GeneratedRegex(@"^.+:\s*error\s+CS\d+:.+$", RegexOptions.Multiline)]
    private static partial Regex BuildErrorRegex();
}
