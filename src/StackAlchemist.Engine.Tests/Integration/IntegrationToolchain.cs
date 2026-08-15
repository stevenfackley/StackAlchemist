using System.Diagnostics;
using System.Text;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Shared plumbing for the template gates: probing that an external toolchain is reachable,
/// and running one to completion with a readable transcript.
///
/// <see cref="V1TemplateCompileTests"/> carries its own private copies of these, written
/// before there was a second gate. Folding it onto this one is worth doing, but it is a
/// separate change to the repository's most safety-critical test file and does not belong in
/// the same commit as a Tier-3 fix.
/// </summary>
internal static class IntegrationToolchain
{
    /// <summary>
    /// Skips locally when a toolchain is missing, but FAILS when <paramref name="requiredOnCi"/>
    /// and we are on CI. A gate that quietly passes in 0.4s because a binary moved is not a gate.
    ///
    /// <paramref name="requiredOnCi"/> is a statement about the GitHub-hosted runner image, not a
    /// preference: <c>ubuntu-latest</c> (24.04) ships Node, npm and Helm 3.x, so their absence
    /// means the gate has stopped gating. It does NOT ship Terraform — HashiCorp's licence change
    /// got it removed from the image — so a Terraform check has to skip cleanly even on CI.
    /// </summary>
    public static bool Available(string fileName, string arguments, string label, bool requiredOnCi)
    {
        if (IsOnPath(fileName, arguments))
            return true;

        if (requiredOnCi)
        {
            Assert.False(
                IsContinuousIntegration,
                $"{label} is required to verify the Tier-3 deliverable and was not found on PATH " +
                $"(tried '{fileName}'). The backend CI job runs on ubuntu-latest, which provides " +
                "it — if this fires, the gate is no longer actually exercising the infrastructure " +
                "the customer paid for.");
        }

        Console.WriteLine($"Skipping — {label} not found on PATH.");
        return false;
    }

    public static bool IsContinuousIntegration =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 } ci
        && !ci.Equals("false", StringComparison.OrdinalIgnoreCase)
        && !ci.Equals("0", StringComparison.Ordinal);

    private static bool IsOnPath(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return false;

            process.WaitForExit(15_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs a process to completion, interleaving stdout and stderr into one transcript. Build
    /// tools routinely log to stderr, so capturing only stdout leaves a failing assertion with
    /// nothing useful in it.
    /// </summary>
    public static async Task<(int ExitCode, string Transcript)> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (value is null)
                    startInfo.Environment.Remove(key);
                else
                    startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        var transcript = new StringBuilder();
        void Append(object _, DataReceivedEventArgs e)
        {
            if (e.Data is null)
                return;
            lock (transcript)
                transcript.AppendLine(e.Data);
        }

        process.OutputDataReceived += Append;
        process.ErrorDataReceived += Append;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already gone */ }

            lock (transcript)
                return (-1, $"[killed after {timeout}]\n{transcript}");
        }

        lock (transcript)
            return (process.ExitCode, transcript.ToString());
    }

    /// <summary>Last <paramref name="lines"/> lines — an npm/cdk log runs to megabytes.</summary>
    public static string Tail(string transcript, int lines = 120)
    {
        var split = transcript.Split('\n');
        return split.Length <= lines ? transcript : string.Join('\n', split[^lines..]);
    }
}
