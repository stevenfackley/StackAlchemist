using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The commands the FastAPI + React gates run over a rendered tree, shared by
/// <see cref="V1PythonReactCompileTests"/> and <see cref="V2PythonReactCompileTests"/>.
///
/// The backend half never touches the machine's own Python: every gate builds a throwaway
/// virtual environment and installs the template's <c>requirements.txt</c> into it. That is
/// what makes the gate safe to run on a developer's box, and it is also the only way it can run
/// on a hosted runner, whose system interpreter refuses <c>pip install</c> outright (PEP 668).
/// </summary>
internal static class PythonReactGate
{
    /// <summary>The interpreter <see cref="PythonReactBuildStrategy"/> invokes, by the same name.</summary>
    public const string Python = "python";

    private static readonly TimeSpan VenvTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Both toolchains, each skipping locally and failing on CI when missing. The backend CI
    /// job installs Python with <c>actions/setup-python</c> precisely so this cannot skip there.
    /// </summary>
    public static bool ToolchainsAvailable() =>
        IntegrationToolchain.Available(Python, "--version", "Python", requiredOnCi: true)
        && IntegrationToolchain.Available(ProcessCommandResolver.Npm, "--version", "npm", requiredOnCi: true);

    /// <summary>Creates a virtual environment and returns the path of its interpreter.</summary>
    public static async Task<string> CreateVenvAsync(string venvDirectory)
    {
        var (exit, log) = await IntegrationToolchain.RunAsync(
            Python, $"-m venv \"{venvDirectory}\"", Path.GetTempPath(), VenvTimeout);

        exit.Should().Be(0, $"the gate needs an isolated interpreter.\n\n{IntegrationToolchain.Tail(log)}");

        var interpreter = OperatingSystem.IsWindows()
            ? Path.Combine(venvDirectory, "Scripts", "python.exe")
            : Path.Combine(venvDirectory, "bin", "python");

        File.Exists(interpreter).Should().BeTrue($"`python -m venv` must have produced {interpreter}");
        return interpreter;
    }

    /// <summary>
    /// <c>pip install -r requirements.txt</c> into the venv — the surface every pip Dependabot
    /// bump touches, and the first line of the customer's <c>Dockerfile.backend</c>.
    /// </summary>
    public static async Task AssertRequirementsInstallAsync(string venvPython, string backendDirectory)
    {
        var (exit, log) = await IntegrationToolchain.RunAsync(
            venvPython, "-m pip install -r requirements.txt --disable-pip-version-check",
            backendDirectory, InstallTimeout);

        exit.Should().Be(0,
            $"the template's pinned requirements must install together.\n\n{IntegrationToolchain.Tail(log)}");
    }

    /// <summary>
    /// Runs the template's own test suite. <c>DATABASE_URL</c> is removed from the child
    /// environment so the suite proves it needs no database at all — and so a developer's own
    /// connection string can never be handed to generated code.
    /// </summary>
    public static async Task AssertBackendTestsPassAsync(string venvPython, string backendDirectory)
    {
        var (exit, log) = await IntegrationToolchain.RunAsync(
            venvPython, "-m pytest -q -p no:cacheprovider", backendDirectory, StepTimeout,
            new Dictionary<string, string?> { ["DATABASE_URL"] = null });

        exit.Should().Be(0,
            $"the archive ships its own tests; they must pass on the template as rendered.\n\n{IntegrationToolchain.Tail(log)}");
    }

    /// <summary>
    /// <c>npm ci</c> against the committed lockfile. Unlike <c>npm install</c> it refuses a
    /// package.json/package-lock.json disagreement instead of silently re-resolving it — the
    /// desync class V1's gate caught on 2026-08-19 (#312).
    /// </summary>
    public static async Task AssertFrontendInstallsFromLockfileAsync(string frontendDirectory)
    {
        File.Exists(Path.Combine(frontendDirectory, "package-lock.json"))
            .Should().BeTrue("the frontend ships a committed lockfile; `npm ci` cannot run without one");

        var (exit, log) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm, "ci --no-audit --no-fund", frontendDirectory, InstallTimeout);

        exit.Should().Be(0,
            $"`npm ci` must install from the committed lockfile — a failure here is a manifest/lock "
            + $"desync or an unresolvable pin.\n\n{IntegrationToolchain.Tail(log)}");
    }

    /// <summary>The two frontend checks <see cref="PythonReactBuildStrategy"/> runs, verbatim.</summary>
    public static async Task AssertFrontendLintsAndTypechecksAsync(string frontendDirectory)
    {
        var (lintExit, lintLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm, "run lint -- --max-warnings=0", frontendDirectory, StepTimeout);

        lintExit.Should().Be(0, $"`npm run lint` is a Compile Guarantee step.\n\n{IntegrationToolchain.Tail(lintLog)}");

        var (tscExit, tscLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npx, "tsc --noEmit", frontendDirectory, StepTimeout);

        tscExit.Should().Be(0, $"`tsc --noEmit` is a Compile Guarantee step.\n\n{IntegrationToolchain.Tail(tscLog)}");
    }

    /// <summary>
    /// <c>npm run build</c> (<c>tsc -b &amp;&amp; vite build</c>) — what the customer's
    /// <c>Dockerfile.frontend</c> runs, and the one frontend command the build strategy does not.
    /// </summary>
    public static async Task AssertFrontendBuildsAsync(string frontendDirectory)
    {
        var (exit, log) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm, "run build", frontendDirectory, StepTimeout);

        exit.Should().Be(0,
            $"`npm run build` is what the shipped Dockerfile.frontend runs.\n\n{IntegrationToolchain.Tail(log)}");

        File.Exists(Path.Combine(frontendDirectory, "dist", "index.html"))
            .Should().BeTrue("`vite build` must have produced the bundle nginx serves, not been skipped");

        TailwindStylesheet.AssertCompiled(
            Path.Combine(frontendDirectory, "dist"), ".min-h-screen", ".bg-gray-50");
    }

    /// <summary>
    /// The production <see cref="PythonReactBuildStrategy"/>, unchanged except that its
    /// <c>python</c> resolves to the gate's virtual environment instead of whatever interpreter
    /// is first on PATH. Uses the <see cref="BuildStrategyBase.RunProcessAsync"/> seam the
    /// strategy keeps open for exactly this.
    /// </summary>
    public sealed class VenvPythonReactBuildStrategy(string venvPython)
        : PythonReactBuildStrategy(NullLogger<PythonReactBuildStrategy>.Instance)
    {
        protected override Task<BuildResult> RunProcessAsync(
            string fileName, string arguments, string workingDirectory, CancellationToken ct) =>
            base.RunProcessAsync(
                fileName == Python ? venvPython : fileName, arguments, workingDirectory, ct);
    }
}
