using System.Diagnostics;
using System.Text;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Toolchain guard + process runner for template compile gates.
///
/// Extracted for <see cref="V0SparkCompileTests"/>; <see cref="V1TemplateCompileTests"/>
/// deliberately keeps its own private copies — that gate is the proven one and stays
/// untouched. Behaviour matches it: a missing toolchain skips locally but FAILS on CI,
/// because a gate that quietly passes in 0.4s when npm moves is not a gate.
/// </summary>
internal static class TemplateGateToolchain
{
    /// <summary>Generous ceiling per process: a timeout should mean "hung", not "slow runner".</summary>
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(10);

    public static bool Available(string fileName, string arguments, string label)
    {
        if (IsOnPath(fileName, arguments))
            return true;

        Assert.False(
            IsContinuousIntegration,
            $"{label} is required by this template gate and was not found on PATH " +
            $"(tried '{fileName}'). The backend CI job runs on ubuntu-latest, which provides " +
            "the .NET SDK, Node and Docker — if this fires, the gate is no longer actually " +
            "building the template.");

        Console.WriteLine($"Skipping — {label} not found on PATH.");
        return false;
    }

    /// <summary>
    /// Runs a process to completion, interleaving stdout and stderr into one transcript —
    /// npm and next write plenty of both, and a failing assertion needs all of it.
    /// </summary>
    public static async Task<(int ExitCode, string Transcript)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

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

        using var cts = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already gone */ }

            lock (transcript)
                return (-1, $"[killed after {ProcessTimeout}]\n{transcript}");
        }

        lock (transcript)
            return (process.ExitCode, transcript.ToString());
    }

    /// <summary>Last <see cref="TranscriptTailLines"/> lines — an npm/next log can be huge.</summary>
    public static string Tail(string transcript)
    {
        var lines = transcript.Split('\n');
        return lines.Length <= TranscriptTailLines
            ? transcript
            : string.Join('\n', lines[^TranscriptTailLines..]);
    }

    private const int TranscriptTailLines = 120;

    private static bool IsContinuousIntegration =>
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
}
