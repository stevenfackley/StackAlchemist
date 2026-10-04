using FluentAssertions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The compile gate for <c>V2-Python-React</c> — the set <c>GenerationOrchestrator</c> loads for
/// a FastAPI + React generation when <c>Generation:UseSwissCheese</c> is on (off in production
/// today, so this tree reaches no customer yet; StackAlchemist#313).
///
/// Rendered through <see cref="SwissCheeseTemplateHarness"/> — the real V2 path, every zone
/// filled by the real <see cref="Services.InjectionEngine"/> — then gated per half:
/// <list type="bullet">
/// <item>frontend: <c>npm ci</c> against the committed lockfile, the build strategy's eslint and
/// <c>tsc --noEmit</c>, and <c>npm run build</c>;</item>
/// <item>backend: the pinned <c>requirements.txt</c> installs into a throwaway venv — the
/// surface pip Dependabot bumps touch;</item>
/// <item>backend: flake8 (the build strategy's compile check) and the archive's own pytest
/// suite, see <see cref="RenderedBackend_CompilesAndPassesItsOwnTests"/>. Every Python zone sits
/// inside a <c>def</c>/<c>class</c>, so this is also the end-to-end proof that a zone fill keeps
/// its indentation (StackAlchemist#450).</item>
/// </list>
///
/// Toolchain-guarded via <see cref="PythonReactGate.ToolchainsAvailable"/>: skips locally when
/// Python or npm is missing, hard-fails on CI so the gate cannot go quiet.
/// </summary>
public sealed class V2PythonReactCompileTests : IDisposable
{
    private const string TemplateSetName = "V2-Python-React";

    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(), "sa-v2-python-gate-" + Guid.NewGuid().ToString("N")[..8]);

    private string OutputDir => Path.Combine(_workDir, "archive");

    private string VenvDir => Path.Combine(_workDir, "venv");

    public void Dispose()
    {
        try { Directory.Delete(_workDir, recursive: true); }
        catch { /* best effort: node_modules on Windows resists deletion */ }
    }

    private Task<SwissCheeseRender> RenderAsync() =>
        SwissCheeseTemplateHarness.RenderToAsync(TemplateSetName, ProjectType.PythonReact, OutputDir);

    [Fact]
    public async Task Render_FillsEveryZoneAndLeavesNoScaffolding()
    {
        var render = await RenderAsync();
        var files = render.Files;

        render.ZonesFilled.Should().BeGreaterThan(0,
            "the gate must exercise the InjectionEngine, not a tree with no zones in it");

        files.Should().Contain(
            ["backend/app/repositories/customer.py", "backend/app/models/invoice.py",
             "backend/app/routers/lineitem.py", "backend/app/schemas/customer.py"],
            "per-entity templates render once per schema entity");
        files.Should().Contain(["frontend/src/pages/customers.tsx", "frontend/src/pages/lineitems.tsx"]);
        files.Should().Contain("frontend/package-lock.json",
            "the frontend installs with `npm ci`, which requires a lockfile");

        files.Should().NotContain(p => p.Contains("node_modules", StringComparison.Ordinal)
                                    || p.Contains("__pycache__", StringComparison.Ordinal),
            "build residue in the template tree must never reach a customer archive");

        foreach (var relativePath in files)
        {
            var content = File.ReadAllText(Path.Combine(OutputDir, relativePath));

            content.Should().NotContain("[[LLM_INJECTION_",
                $"{relativePath} would ship pipeline scaffolding to the customer");
            content.Should().NotContain("{{",
                $"{relativePath} has an unsubstituted Handlebars token");
        }
    }

    [Fact]
    public async Task RenderedFrontend_InstallsFromItsLockfileAndBuilds()
    {
        if (!PythonReactGate.ToolchainsAvailable())
            return;

        await RenderAsync();
        var frontendDir = Path.Combine(OutputDir, "frontend");

        await PythonReactGate.AssertFrontendInstallsFromLockfileAsync(frontendDir);
        await PythonReactGate.AssertFrontendLintsAndTypechecksAsync(frontendDir);
        await PythonReactGate.AssertFrontendBuildsAsync(frontendDir);
    }

    [Fact]
    public async Task RenderedBackend_InstallsItsPinnedRequirements()
    {
        if (!PythonReactGate.ToolchainsAvailable())
            return;

        await RenderAsync();

        var venvPython = await PythonReactGate.CreateVenvAsync(VenvDir);
        await PythonReactGate.AssertRequirementsInstallAsync(venvPython, Path.Combine(OutputDir, "backend"));
    }

    /// <summary>
    /// The production verification for this half (<c>PythonReactBuildStrategy</c>'s flake8 +
    /// pytest collection), plus the archive's own pytest suite, over the Swiss-Cheese output.
    /// </summary>
    [Fact]
    public async Task RenderedBackend_CompilesAndPassesItsOwnTests()
    {
        if (!PythonReactGate.ToolchainsAvailable())
            return;

        await RenderAsync();
        var backendDir = Path.Combine(OutputDir, "backend");

        var venvPython = await PythonReactGate.CreateVenvAsync(VenvDir);
        await PythonReactGate.AssertRequirementsInstallAsync(venvPython, backendDir);

        var (flake8Exit, flake8Log) = await IntegrationToolchain.RunAsync(
            venvPython, "-m flake8 .", backendDir, TimeSpan.FromMinutes(5));
        flake8Exit.Should().Be(0,
            $"flake8 is the build strategy's compile check for the FastAPI half.\n\n{IntegrationToolchain.Tail(flake8Log)}");

        await PythonReactGate.AssertBackendTestsPassAsync(venvPython, backendDir);
    }
}
