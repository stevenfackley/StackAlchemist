using System.Diagnostics;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Where and as whom Compile Guarantee builds run (StackAlchemist#454). Read once at startup
/// from the image's environment: <c>SA_BUILD_UID</c>, <c>SA_BUILD_GID</c> and
/// <c>SA_BUILD_ROOT</c>, set by the Dockerfile's engine stage.
/// </summary>
public sealed record BuildSandboxSettings(int Uid, int Gid, string BuildRoot, string WrapperPath)
{
    /// <summary>The root-owned wrapper that drops to the build user (docker/engine/sa-sandbox-exec).</summary>
    public const string DefaultWrapperPath = "/usr/local/lib/stackalchemist/sa-sandbox-exec";

    /// <summary>
    /// The settings when every precondition holds, else null and the reason. Unset
    /// <c>SA_BUILD_UID</c> means "not configured" (local runs, unit hosts); set but unusable is a
    /// misconfiguration the caller should refuse in Production.
    /// </summary>
    public static (BuildSandboxSettings? Settings, string Reason) Detect(
        Func<string, string?> env,
        bool isLinux,
        bool isPrivileged,
        Func<string, bool> fileExists,
        string wrapperPath = DefaultWrapperPath)
    {
        var uidText = env("SA_BUILD_UID");
        if (string.IsNullOrWhiteSpace(uidText))
            return (null, "SA_BUILD_UID is not set");
        if (!isLinux)
            return (null, "the build sandbox needs Linux");
        if (!int.TryParse(uidText, out var uid) || uid <= 0)
            return (null, "SA_BUILD_UID is not a positive integer");
        var gidText = env("SA_BUILD_GID");
        var gid = uid;
        if (!string.IsNullOrWhiteSpace(gidText) && (!int.TryParse(gidText, out gid) || gid <= 0))
            return (null, "SA_BUILD_GID is not a positive integer");
        var root = env("SA_BUILD_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
            return (null, "SA_BUILD_ROOT is not an absolute path");
        if (!isPrivileged)
            return (null, "the Engine is not running as root, so it cannot drop to the build user");
        if (!fileExists(wrapperPath))
            return (null, $"the sandbox wrapper {wrapperPath} is missing");

        return (new BuildSandboxSettings(uid, gid, Path.TrimEndingDirectorySeparator(root), wrapperPath), "enabled");
    }

    public static (BuildSandboxSettings? Settings, string Reason) FromEnvironment() => Detect(
        Environment.GetEnvironmentVariable,
        OperatingSystem.IsLinux(),
        Environment.IsPrivilegedProcess,
        File.Exists);
}

/// <summary>
/// Runs Compile Guarantee builds, which execute LLM-generated code, as an unprivileged user in a
/// throwaway copy of the generated tree (StackAlchemist#454).
///
/// <para>The rules, and why each one exists:</para>
/// <list type="bullet">
/// <item><b>Another uid.</b> Every build command goes through a root-owned wrapper that drops to
/// <see cref="BuildSandboxSettings.Uid"/> with no-new-privs and an empty capability set. The
/// Engine's <c>/proc/&lt;pid&gt;/environ</c>, which holds every secret, is then unreadable.</item>
/// <item><b>A copy, never the source tree.</b> The build runs in <c>&lt;root&gt;/&lt;job&gt;</c>,
/// copied fresh from the Engine's tree on every attempt. The Engine (root) keeps writing repair
/// files, the build report and the archive in its own tree, which the build user cannot reach.
/// A build that plants a symlink therefore never steers a root write or read. Only
/// <c>package-lock.json</c> comes back, because <c>npm install</c> legitimately rewrites it
/// when a repair adds a dependency.</item>
/// <item><b>A private HOME per job.</b> <c>&lt;root&gt;/&lt;job&gt;.home</c>, kept across the
/// attempts of one job (warm NuGet and npm caches) and deleted when the next job starts, so one
/// customer's build cannot poison another's package cache.</item>
/// <item><b>Nothing survives the build.</b> After every build, every process of the build user
/// is killed. A daemon left behind could otherwise watch later jobs.</item>
/// </list>
/// The egress firewall for the same uid (no private ranges, no instance metadata) is installed by
/// the container entrypoint, not here.
/// </summary>
public sealed partial class BuildSandbox(BuildSandboxSettings settings, ILogger<BuildSandbox> logger)
{
    /// <summary>The one file a build may hand back to the Engine's tree.</summary>
    internal const string CopyBackFileName = "package-lock.json";

    /// <summary>A lockfile larger than this is not a lockfile.</summary>
    private const long CopyBackMaxBytes = 64L * 1024 * 1024;

    /// <summary>Variables that would point a build at a root-owned cache shared across jobs.</summary>
    private static readonly string[] CacheVariables =
    [
        "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_PLUGINS_CACHE_PATH", "NUGET_SCRATCH",
        "NPM_CONFIG_CACHE", "npm_config_cache", "NPM_CONFIG_PREFIX", "npm_config_prefix",
        "PIP_CACHE_DIR", "DOTNET_CLI_HOME", "DOTNET_INSTALL_DIR",
    ];

    public BuildSandboxSettings Settings { get; } = settings;

    /// <summary>Test seam: runs a root-side tool (chown, rm) and returns its exit code.</summary>
    internal Func<string, IReadOnlyList<string>, CancellationToken, Task<int>> RunTool { get; init; } = RunToolAsync;

    /// <summary>The build directory for <paramref name="projectDirectory"/>.</summary>
    public string BuildDirectoryFor(string projectDirectory) =>
        Path.Combine(Settings.BuildRoot, JobName(projectDirectory));

    /// <summary>
    /// Copies <paramref name="projectDirectory"/> into a fresh build directory owned by the build
    /// user, makes sure the job's HOME exists, and returns the build directory. Deletes what an
    /// earlier job left behind.
    /// </summary>
    public async Task<string> PrepareAsync(string projectDirectory, CancellationToken ct)
    {
        var job = JobName(projectDirectory);
        var buildDir = Path.Combine(Settings.BuildRoot, job);
        var homeDir = HomeDirectoryFor(buildDir);

        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The build sandbox runs on Linux only.");

        Directory.CreateDirectory(Settings.BuildRoot,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);

        // Another job's leftovers (a crash, a restart): remove them, HOME included.
        foreach (var entry in Directory.EnumerateFileSystemEntries(Settings.BuildRoot))
        {
            var name = Path.GetFileName(entry);
            if (name != job && name != job + HomeSuffix)
                await RemoveAsync(entry, ct);
        }

        await RemoveAsync(buildDir, ct);
        CopyTree(projectDirectory, buildDir);
        Directory.CreateDirectory(Path.Combine(homeDir, "tmp"));

        await RequireAsync("chown", ["-hR", $"{Settings.Uid}:{Settings.Gid}", "--", buildDir, homeDir], ct);
        await RequireAsync("chmod", ["0700", "--", buildDir, homeDir], ct);

        LogPrepared(logger, job);
        return buildDir;
    }

    /// <summary>
    /// After a build: kills every process of the build user, then copies the lockfiles back into
    /// the Engine's tree and deletes the build directory. The HOME stays for the next attempt.
    /// </summary>
    public async Task FinishAsync(string projectDirectory, string buildDir, CancellationToken ct)
    {
        // CancellationToken.None throughout: a shutdown mid-build must still stop the build
        // user's processes, or they outlive the job they belong to.
        await KillBuildUserProcessesAsync();
        CopyBack(buildDir, projectDirectory);
        await RemoveAsync(buildDir, CancellationToken.None);
    }

    /// <summary>
    /// Rewrites <paramref name="psi"/> to run <paramref name="fileName"/> through the wrapper as the
    /// build user, with HOME and TMPDIR inside the job's private HOME.
    /// </summary>
    public void Apply(ProcessStartInfo psi, string fileName, string arguments, string workingDirectory)
    {
        var buildDir = BuildDirectoryContaining(workingDirectory);
        var home = HomeDirectoryFor(buildDir);

        psi.FileName = Settings.WrapperPath;
        psi.Arguments = $"{Quote(fileName)} {arguments}";

        foreach (var name in CacheVariables)
            psi.Environment.Remove(name);
        foreach (var name in psi.Environment.Keys.Where(k => k.StartsWith("XDG_", StringComparison.Ordinal)).ToList())
            psi.Environment.Remove(name);

        psi.Environment["HOME"] = home;
        psi.Environment["TMPDIR"] = Path.Combine(home, "tmp");
        psi.Environment["USER"] = "sa-builder";
        psi.Environment["LOGNAME"] = "sa-builder";
    }

    private const string HomeSuffix = ".home";

    internal static string HomeDirectoryFor(string buildDir) => buildDir + HomeSuffix;

    /// <summary>The job's build directory: the first segment of <paramref name="path"/> under the build root.</summary>
    internal string BuildDirectoryContaining(string path)
    {
        var relative = Path.GetRelativePath(Settings.BuildRoot, Path.GetFullPath(path));
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (relative == "." || first == ".." || Path.IsPathRooted(relative) || first.EndsWith(HomeSuffix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Sandboxed builds run under {Settings.BuildRoot}, not {path}.");
        return Path.Combine(Settings.BuildRoot, first);
    }

    internal static string JobName(string projectDirectory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(projectDirectory));
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.EndsWith(HomeSuffix, StringComparison.Ordinal)
            || name.IndexOfAny([.. Path.GetInvalidFileNameChars()]) >= 0)
            throw new ArgumentException($"Not a job directory: {projectDirectory}", nameof(projectDirectory));
        return name;
    }

    /// <summary>
    /// Copies the generated tree, skipping build residue and symlinks. The Engine wrote this tree
    /// itself and no build ever ran in it, so neither should exist; skipping them is the cheap
    /// guarantee.
    /// </summary>
    internal static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        Copy(source, destination, relativeDirectory: string.Empty);

        static void Copy(string sourceRoot, string destRoot, string relativeDirectory)
        {
            var from = relativeDirectory.Length == 0 ? sourceRoot : Path.Combine(sourceRoot, relativeDirectory);
            var to = relativeDirectory.Length == 0 ? destRoot : Path.Combine(destRoot, relativeDirectory);
            Directory.CreateDirectory(to);

            foreach (var file in Directory.EnumerateFiles(from))
            {
                if (IsLink(file))
                    continue;
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }

            foreach (var directory in Directory.EnumerateDirectories(from))
            {
                var name = Path.GetFileName(directory);
                var childRelative = relativeDirectory.Length == 0 ? name : $"{relativeDirectory}/{name}";
                if (IsLink(directory) || BuildResiduePaths.IsResidueDirectory(childRelative))
                    continue;
                Copy(sourceRoot, destRoot, childRelative);
            }
        }
    }

    /// <summary>
    /// Copies every regular-file <c>package-lock.json</c> outside build residue from
    /// <paramref name="buildDir"/> back to the same place in <paramref name="projectDirectory"/>,
    /// when that directory exists there. Runs after every build-user process is dead, so the tree
    /// cannot change between the checks and the copy.
    /// </summary>
    internal static void CopyBack(string buildDir, string projectDirectory)
    {
        if (!Directory.Exists(buildDir) || IsLink(buildDir))
            return;
        Walk(relativeDirectory: string.Empty);

        void Walk(string relativeDirectory)
        {
            var from = relativeDirectory.Length == 0 ? buildDir : Path.Combine(buildDir, relativeDirectory);
            var lockfile = Path.Combine(from, CopyBackFileName);
            var target = relativeDirectory.Length == 0 ? projectDirectory : Path.Combine(projectDirectory, relativeDirectory);
            if (File.Exists(lockfile) && !IsLink(lockfile) && Directory.Exists(target)
                && new FileInfo(lockfile).Length <= CopyBackMaxBytes)
            {
                File.Copy(lockfile, Path.Combine(target, CopyBackFileName), overwrite: true);
            }

            foreach (var directory in Directory.EnumerateDirectories(from))
            {
                var name = Path.GetFileName(directory);
                var childRelative = relativeDirectory.Length == 0 ? name : $"{relativeDirectory}/{name}";
                if (IsLink(directory) || BuildResiduePaths.IsResidueDirectory(childRelative))
                    continue;
                Walk(childRelative);
            }
        }
    }

    internal static bool IsLink(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true; // unreadable: treat as hostile
        }
    }

    internal static string Quote(string value) =>
        value.Length > 0 && value.IndexOfAny([' ', '\t', '"', '\'', '\\']) < 0 ? value : "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private async Task KillBuildUserProcessesAsync()
    {
        // Runs as the build user: kill(-1) then reaches every process that uid owns and nothing
        // else. It exits non-zero when there was nothing left to kill, which is the normal case.
        try
        {
            await RunTool(Settings.WrapperPath, ["kill", "-KILL", "--", "-1"], CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LogPurgeFailed(logger, ex);
        }
    }

    private Task RemoveAsync(string path, CancellationToken ct) =>
        // GNU rm never follows a symlink it is deleting, and --one-file-system keeps a bind mount
        // or a mount point planted in the tree from widening the delete.
        Path.Exists(path) || IsDanglingLink(path)
            ? RequireAsync("rm", ["-rf", "--one-file-system", "--", path], ct)
            : Task.CompletedTask;

    private static bool IsDanglingLink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private async Task RequireAsync(string tool, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var exitCode = await RunTool(tool, arguments, ct);
        if (exitCode != 0)
            throw new InvalidOperationException($"Build sandbox: `{tool}` exited {exitCode}.");
    }

    private static async Task<int> RunToolAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        BuildProcessEnvironment.Apply(psi.Environment);

        using var process = Process.Start(psi)!;
        var drain = Task.WhenAll(process.StandardOutput.ReadToEndAsync(ct), process.StandardError.ReadToEndAsync(ct));
        await process.WaitForExitAsync(ct);
        await drain;
        return process.ExitCode;
    }

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Build sandbox prepared for job {Job}")]
    private static partial void LogPrepared(ILogger logger, string job);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Error, Message = "Build sandbox: killing the build user's leftover processes failed")]
    private static partial void LogPurgeFailed(ILogger logger, Exception ex);
}
