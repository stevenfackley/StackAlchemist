namespace StackAlchemist.Engine.Services;

/// <summary>
/// Resolves the launcher for an external build tool to something
/// <c>Process.Start(UseShellExecute: false)</c> can actually run on this OS.
///
/// On Linux (the build container) <c>npm</c> is a shebang script found on PATH, so the bare
/// name works. On Windows it is <c>npm.cmd</c>, and passing the bare name is worse than
/// useless: CreateProcess launches the shim through cmd.exe without a qualified path, so the
/// shim's own <c>%~dp0</c> expands to the *working directory* and npm dies with
/// "Cannot find module &lt;cwd&gt;\node_modules\npm\bin\npm-cli.js". Handing Process.Start the
/// absolute path is what lets the shim locate its own installation.
/// </summary>
public static class ProcessCommandResolver
{
    /// <summary>Launcher for npm on this machine.</summary>
    public static string Npm { get; } = Resolve("npm");

    /// <summary>Launcher for npx on this machine.</summary>
    public static string Npx { get; } = Resolve("npx");

    public static string Resolve(string command)
    {
        if (!OperatingSystem.IsWindows())
            return command;

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathDirectories)
        {
            foreach (var extension in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory.Trim('"'), command + extension);
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry (illegal characters) is not worth failing over.
                    break;
                }

                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Not installed. Return the shim name so the caller's failure message names the
        // tool rather than an empty string.
        return command + ".cmd";
    }
}
