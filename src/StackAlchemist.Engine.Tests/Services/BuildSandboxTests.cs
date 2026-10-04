using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The build sandbox (#454): generated code runs as another uid, in a copy of the tree that the
/// Engine never writes into again. The OS half (setpriv, the firewall) is proven inside the
/// container by docker/engine/sandbox-selftest.sh in CI; these tests pin the Engine half.
/// </summary>
public sealed class BuildSandboxTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "sa-sandbox-tests-" + Guid.NewGuid().ToString("N"));

    public BuildSandboxTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { }
    }

    // ── Detect ────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string?> ImageEnv = new()
    {
        ["SA_BUILD_UID"] = "10001",
        ["SA_BUILD_GID"] = "10001",
        ["SA_BUILD_ROOT"] = "/var/lib/stackalchemist/build/",
    };

    private static (BuildSandboxSettings? Settings, string Reason) Detect(
        Dictionary<string, string?> env, bool isLinux = true, bool isPrivileged = true, bool wrapperExists = true) =>
        BuildSandboxSettings.Detect(n => env.GetValueOrDefault(n), isLinux, isPrivileged, _ => wrapperExists);

    [Fact]
    public void Detect_TheEngineImage_EnablesTheSandbox()
    {
        var (settings, reason) = Detect(ImageEnv);

        reason.Should().Be("enabled");
        settings.Should().Be(new BuildSandboxSettings(10001, 10001, "/var/lib/stackalchemist/build", BuildSandboxSettings.DefaultWrapperPath));
    }

    [Fact]
    public void Detect_WithoutSaBuildUid_IsNotConfigured() =>
        Detect(new Dictionary<string, string?>()).Settings.Should().BeNull();

    [Theory]
    [InlineData(false, true, true, "Linux")]
    [InlineData(true, false, true, "root")]
    [InlineData(true, true, false, "wrapper")]
    public void Detect_ConfiguredButUnusable_SaysWhy(bool isLinux, bool isPrivileged, bool wrapperExists, string reasonMentions)
    {
        var (settings, reason) = Detect(ImageEnv, isLinux, isPrivileged, wrapperExists);

        settings.Should().BeNull();
        reason.Should().Contain(reasonMentions);
    }

    [Theory]
    [InlineData("SA_BUILD_UID", "0")]
    [InlineData("SA_BUILD_UID", "builder")]
    [InlineData("SA_BUILD_GID", "-1")]
    [InlineData("SA_BUILD_ROOT", "relative/build")]
    public void Detect_RejectsMalformedValues(string name, string value)
    {
        var env = new Dictionary<string, string?>(ImageEnv) { [name] = value };

        Detect(env).Settings.Should().BeNull();
    }

    // ── Apply: the command and the environment a build sees ──────────────────

    private BuildSandbox Sandbox(string? root = null) =>
        new(new BuildSandboxSettings(10001, 10001, root ?? Path.Combine(_scratch, "build"), "/usr/local/lib/stackalchemist/sa-sandbox-exec"),
            NullLogger<BuildSandbox>.Instance);

    [Fact]
    public void Apply_RunsTheCommandThroughTheWrapper_WithAPrivateHome()
    {
        var sandbox = Sandbox();
        var workDir = Path.Combine(sandbox.Settings.BuildRoot, "gen-1", "nextjs");
        var psi = new ProcessStartInfo("npm") { Arguments = "ci --no-audit --no-fund" };
        psi.Environment["HOME"] = "/root";
        psi.Environment["TMPDIR"] = "/var/lib/stackalchemist/tmp";
        psi.Environment["NUGET_PACKAGES"] = "/root/.nuget/packages";
        psi.Environment["XDG_CACHE_HOME"] = "/root/.cache";
        psi.Environment["PATH"] = "/usr/bin";

        sandbox.Apply(psi, "npm", "ci --no-audit --no-fund", workDir);

        psi.FileName.Should().Be("/usr/local/lib/stackalchemist/sa-sandbox-exec");
        psi.Arguments.Should().Be("npm ci --no-audit --no-fund");
        var home = Path.Combine(sandbox.Settings.BuildRoot, "gen-1") + ".home";
        psi.Environment["HOME"].Should().Be(home);
        psi.Environment["TMPDIR"].Should().Be(Path.Combine(home, "tmp"));
        psi.Environment.Should().NotContainKey("NUGET_PACKAGES", "a shared root cache would let one job poison another's restore");
        psi.Environment.Should().NotContainKey("XDG_CACHE_HOME");
        psi.Environment["PATH"].Should().Be("/usr/bin");
    }

    [Fact]
    public void Apply_QuotesAnExecutablePathWithSpaces()
    {
        var sandbox = Sandbox();
        var psi = new ProcessStartInfo();

        sandbox.Apply(psi, "/opt/my tools/python", "-m flake8 .", Path.Combine(sandbox.Settings.BuildRoot, "gen-1", "backend"));

        psi.Arguments.Should().Be("\"/opt/my tools/python\" -m flake8 .");
    }

    [Theory]
    [InlineData("elsewhere")]
    [InlineData("gen-1.home")]
    public void Apply_OutsideAJobBuildDirectory_Refuses(string relative)
    {
        var sandbox = Sandbox();
        var workDir = relative == "elsewhere" ? Path.Combine(_scratch, "elsewhere") : Path.Combine(sandbox.Settings.BuildRoot, relative);

        var act = () => sandbox.Apply(new ProcessStartInfo(), "npm", "ci", workDir);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("gen-1.home")]
    public void JobName_RejectsWhatIsNotAJobDirectory(string name) =>
        FluentActions.Invoking(() => BuildSandbox.JobName(_scratch + Path.DirectorySeparatorChar + name))
            .Should().Throw<ArgumentException>();

    // ── The copy in and the copy back ────────────────────────────────────────

    private string Write(string root, string relative, string content = "x")
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CopyTree_CopiesSourceAndSkipsBuildResidue()
    {
        var source = Path.Combine(_scratch, "src");
        Write(source, "dotnet/Program.cs");
        Write(source, "nextjs/app/page.tsx");
        Write(source, "nextjs/node_modules/react/index.js");
        Write(source, "dotnet/obj/project.assets.json");
        Write(source, "infra/cdk/bin/app.ts");
        var dest = Path.Combine(_scratch, "build", "gen-1");

        BuildSandbox.CopyTree(source, dest);

        File.Exists(Path.Combine(dest, "dotnet", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "nextjs", "app", "page.tsx")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "infra", "cdk", "bin", "app.ts")).Should().BeTrue("a CDK bin/ is source, not .NET output");
        Directory.Exists(Path.Combine(dest, "nextjs", "node_modules")).Should().BeFalse();
        Directory.Exists(Path.Combine(dest, "dotnet", "obj")).Should().BeFalse();
    }

    [Fact]
    public void CopyBack_ReturnsOnlyTheLockfiles()
    {
        var project = Path.Combine(_scratch, "src");
        Write(project, "nextjs/package-lock.json", "old");
        Write(project, "nextjs/app/page.tsx", "original");
        var build = Path.Combine(_scratch, "build", "gen-1");
        Write(build, "nextjs/package-lock.json", "rewritten by npm install");
        Write(build, "nextjs/app/page.tsx", "changed by the build");
        Write(build, "nextjs/tsconfig.tsbuildinfo", "junk");
        Write(build, "nextjs/node_modules/x/package-lock.json", "residue");
        Write(build, "backend/package-lock.json", "no such directory in the project");

        BuildSandbox.CopyBack(build, project);

        File.ReadAllText(Path.Combine(project, "nextjs", "package-lock.json")).Should().Be("rewritten by npm install");
        File.ReadAllText(Path.Combine(project, "nextjs", "app", "page.tsx")).Should().Be("original");
        File.Exists(Path.Combine(project, "nextjs", "tsconfig.tsbuildinfo")).Should().BeFalse();
        Directory.Exists(Path.Combine(project, "nextjs", "node_modules")).Should().BeFalse();
        Directory.Exists(Path.Combine(project, "backend")).Should().BeFalse();
    }

    [Fact]
    public void CopyBack_NeverFollowsALockfileSymlink()
    {
        if (OperatingSystem.IsWindows()) return; // guarded, like the toolchain tests: symlinks here need Unix
        var project = Path.Combine(_scratch, "src");
        Write(project, "nextjs/package-lock.json", "old");
        var secret = Write(_scratch, "engine-secret.txt", "DATABASE_URL=postgres://...");
        var build = Path.Combine(_scratch, "build", "gen-1");
        Directory.CreateDirectory(Path.Combine(build, "nextjs"));
        File.CreateSymbolicLink(Path.Combine(build, "nextjs", "package-lock.json"), secret);

        BuildSandbox.CopyBack(build, project);

        File.ReadAllText(Path.Combine(project, "nextjs", "package-lock.json")).Should().Be("old");
    }

    [Fact]
    public void Archive_NeverPacksASymlink()
    {
        if (OperatingSystem.IsWindows()) return; // guarded, like the toolchain tests: symlinks here need Unix
        var project = Path.Combine(_scratch, "src");
        Write(project, "dotnet/Program.cs");
        var secret = Write(_scratch, "outside/secret.txt", "secret");
        File.CreateSymbolicLink(Path.Combine(project, "dotnet", "leak.txt"), secret);
        Directory.CreateSymbolicLink(Path.Combine(project, "dotnet", "outside"), Path.GetDirectoryName(secret)!);

        ProjectArchiver.EnumerateArchiveEntries(project).Should().Equal("dotnet/Program.cs");
    }

    // ── Prepare and Finish ───────────────────────────────────────────────────

    private sealed class ToolLog
    {
        public List<string> Calls { get; } = [];

        public Task<int> Run(string tool, IReadOnlyList<string> args, CancellationToken ct)
        {
            Calls.Add(tool + " " + string.Join(' ', args));
            if (tool == "rm")
            {
                var path = args[^1];
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else if (File.Exists(path)) File.Delete(path);
            }
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task PrepareAndFinish_BuildInACopy_HandTheBuildUserItsTrees_AndKillWhatIsLeft()
    {
        if (OperatingSystem.IsWindows()) return; // guarded: the sandbox runs on Linux only
        var tools = new ToolLog();
        var root = Path.Combine(_scratch, "build");
        var sandbox = new BuildSandbox(new BuildSandboxSettings(10001, 10001, root, "/wrapper"), NullLogger<BuildSandbox>.Instance)
        {
            RunTool = tools.Run,
        };
        var project = Path.Combine(_scratch, "tmp", "gen-2");
        Write(project, "nextjs/package.json", "{}");
        Write(root, "gen-1/stale.txt");
        Directory.CreateDirectory(Path.Combine(root, "gen-1.home"));

        var buildDir = await sandbox.PrepareAsync(project, CancellationToken.None);

        buildDir.Should().Be(Path.Combine(root, "gen-2"));
        File.Exists(Path.Combine(buildDir, "nextjs", "package.json")).Should().BeTrue();
        Directory.Exists(Path.Combine(root, "gen-2.home", "tmp")).Should().BeTrue();
        Directory.Exists(Path.Combine(root, "gen-1")).Should().BeFalse("another job's tree is removed");
        Directory.Exists(Path.Combine(root, "gen-1.home")).Should().BeFalse("another job's HOME, and its package cache, is removed");
        tools.Calls.Should().Contain($"chown -hR 10001:10001 -- {buildDir} {buildDir}.home");

        Write(buildDir, "nextjs/package-lock.json", "new");
        await sandbox.FinishAsync(project, buildDir, CancellationToken.None);

        tools.Calls.Should().Contain("/wrapper kill -KILL -- -1");
        tools.Calls.IndexOf("/wrapper kill -KILL -- -1").Should().BeLessThan(tools.Calls.FindLastIndex(c => c.StartsWith("rm ", StringComparison.Ordinal)),
            "the build user's processes die before its tree is read or deleted");
        File.ReadAllText(Path.Combine(project, "nextjs", "package-lock.json")).Should().Be("new");
        Directory.Exists(buildDir).Should().BeFalse();
        Directory.Exists(buildDir + ".home").Should().BeTrue("the HOME stays warm for the job's next attempt");
    }

    [Fact]
    public async Task CompileService_WithTheSandbox_BuildsTheCopy_NotTheEnginesTree()
    {
        if (OperatingSystem.IsWindows()) return; // guarded: the sandbox runs on Linux only
        var tools = new ToolLog();
        var root = Path.Combine(_scratch, "build");
        var sandbox = new BuildSandbox(new BuildSandboxSettings(10001, 10001, root, "/wrapper"), NullLogger<BuildSandbox>.Instance)
        {
            RunTool = tools.Run,
        };
        var project = Path.Combine(_scratch, "tmp", "gen-3");
        Write(project, "dotnet/App.csproj", "<Project />");
        var strategy = new RecordingStrategy();
        var service = new CompileService([strategy], NullLogger<CompileService>.Instance, sandbox);

        var result = await service.ExecuteBuildAsync(project, ProjectType.DotNetNextJs);

        result.IsSuccess.Should().BeTrue();
        strategy.BuiltIn.Should().Be(Path.Combine(root, "gen-3"));
        strategy.SawProjectFile.Should().BeTrue();
        Directory.Exists(Path.Combine(root, "gen-3")).Should().BeFalse("the copy is discarded after the build");
    }

    private sealed class RecordingStrategy : IBuildStrategy
    {
        public string? BuiltIn { get; private set; }
        public bool SawProjectFile { get; private set; }
        public ProjectType SupportedProjectType => ProjectType.DotNetNextJs;

        public Task<BuildResult> ExecuteBuildAsync(string projectDirectory, CancellationToken ct = default)
        {
            BuiltIn = projectDirectory;
            SawProjectFile = File.Exists(Path.Combine(projectDirectory, "dotnet", "App.csproj"));
            return Task.FromResult(new BuildResult { ExitCode = 0, StandardOutput = "", ErrorOutput = "" });
        }

        public List<string> ExtractBuildErrors(string buildOutput) => [];
    }
}
