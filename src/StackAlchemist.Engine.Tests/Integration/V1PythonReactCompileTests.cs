using System.IO.Abstractions;
using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// THE Compile Guarantee gate for the FastAPI + React/Vite deliverable — <c>V1-Python-React</c>,
/// a live, user-selectable Tier-2 option, and until this gate the only paid template nothing
/// ever rendered and built.
///
/// What that cost was measurable the first time it ran. On the bare template, before any model
/// output, the strategy's verification failed three independent ways:
/// <list type="bullet">
/// <item><c>npm install</c> died on ERESOLVE — <c>typescript</c> had been bumped to 7.x (#274)
/// while <c>typescript-eslint</c> 8.x peers on <c>&lt;6.1.0</c> — and the customer's own
/// <c>docker compose up --build</c> died with it;</item>
/// <item><c>tsc --noEmit</c> failed: there was no <c>src/vite-env.d.ts</c>, so
/// <c>import.meta.env</c> and the side-effect CSS import had no types;</item>
/// <item><c>pytest --collect-only</c> needed a reachable Postgres, because <c>main.py</c> ran
/// <c>Base.metadata.create_all</c> at import time.</item>
/// </list>
///
/// Rendered the way <c>GenerationOrchestrator</c> renders the V1 path (load → render →
/// Reconstruct with an empty block set, i.e. the baseline the one-shot LLM pass adds to), then:
/// <list type="number">
/// <item><c>npm ci</c> against the committed <c>frontend/package-lock.json</c>, so a
/// manifest/lock desync fails;</item>
/// <item>the real <see cref="PythonReactBuildStrategy"/> — pip install, flake8,
/// <c>pytest --collect-only</c>, npm install, eslint, <c>tsc --noEmit</c> — with only its
/// interpreter redirected into a throwaway venv;</item>
/// <item>the two things that strategy does not run but the customer does: <c>npm run build</c>
/// (their <c>Dockerfile.frontend</c>) and the archive's own pytest suite.</item>
/// </list>
///
/// Toolchain-guarded via <see cref="PythonReactGate.ToolchainsAvailable"/>: skips locally when
/// Python or npm is missing, hard-fails on CI so the gate cannot go quiet.
/// </summary>
public sealed class V1PythonReactCompileTests : IDisposable
{
    private const string TemplateSetName = "V1-Python-React";

    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(), "sa-v1-python-gate-" + Guid.NewGuid().ToString("N")[..8]);

    private string OutputDir => Path.Combine(_workDir, "archive");

    private string VenvDir => Path.Combine(_workDir, "venv");

    public void Dispose()
    {
        try { Directory.Delete(_workDir, recursive: true); }
        catch { /* best effort: node_modules on Windows resists deletion */ }
    }

    /// <summary>
    /// Mirrors the V1 branch of <c>GenerationOrchestrator.GenerateFilesAsync</c> with the model
    /// contributing nothing: the real provider renders, the real <see cref="ReconstructionService"/>
    /// resolves and strips the injection scaffolding.
    /// </summary>
    private IReadOnlyList<string> Render(string? llmResponse = null)
    {
        var provider = new TemplateProvider(new FileSystem(), V1TemplateHarness.ResolveTemplatesRoot());
        var reconstruction = new ReconstructionService();
        var rendered = provider.Render(provider.LoadTemplate(TemplateSetName), V1TemplateHarness.SampleVariables());
        var blocks = llmResponse is null ? [] : reconstruction.Parse(llmResponse);
        var files = reconstruction.Reconstruct(rendered, blocks, provider);

        Directory.CreateDirectory(OutputDir);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(OutputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return [.. files.Keys.OrderBy(p => p, StringComparer.Ordinal)];
    }

    [Fact]
    public void Render_ProducesTheExpectedTreeWithNoUnresolvedScaffolding()
    {
        var files = Render();

        files.Should().Contain(["backend/app/main.py", "backend/requirements.txt", "backend/tests/test_health.py"]);
        files.Should().Contain(["frontend/package.json", "frontend/package-lock.json"],
            "the frontend installs with `npm ci`, which requires a lockfile");
        files.Should().Contain(["infra/docker-compose.yml", "infra/Dockerfile.backend", "infra/Dockerfile.frontend"]);

        files.Should().NotContain(p => p.Contains("node_modules", StringComparison.Ordinal)
                                    || p.Contains("__pycache__", StringComparison.Ordinal)
                                    || p.Contains("/dist/", StringComparison.Ordinal),
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
    public async Task RenderedTemplate_PassesTheCompileGuaranteeBuild()
    {
        if (!PythonReactGate.ToolchainsAvailable())
            return;

        Render();
        var backendDir = Path.Combine(OutputDir, "backend");
        var frontendDir = Path.Combine(OutputDir, "frontend");

        await PythonReactGate.AssertFrontendInstallsFromLockfileAsync(frontendDir);

        var venvPython = await PythonReactGate.CreateVenvAsync(VenvDir);
        var strategy = new PythonReactGate.VenvPythonReactBuildStrategy(venvPython);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        var result = await strategy.ExecuteBuildAsync(OutputDir, cts.Token);
        var transcript = $"{result.StandardOutput}\n{result.ErrorOutput}";

        result.IsSuccess.Should().BeTrue(
            because: "V1-Python-React is a live Tier-2 option and this is the exact verification its "
                     + $"Compile Guarantee runs on every paid generation.\n\n{transcript}");

        // A pass with nothing recorded would mean the strategy skipped a half.
        result.Steps.Where(step => step.Half == BuildHalf.Python && !step.Skipped)
            .Select(step => step.Command)
            .Should().Contain(
                ["python -m pip install -r requirements.txt", "python -m flake8 .", "python -m pytest --collect-only"],
                $"the FastAPI half must actually be verified.\n\n{transcript}");

        result.Steps.Where(step => step.Half == BuildHalf.React && !step.Skipped)
            .Select(step => step.Command)
            .Should().Contain(["npm install", "npm run lint", "npx tsc --noEmit"],
                $"the React half must actually be verified.\n\n{transcript}");

        result.Steps.Should().OnlyContain(step => step.Skipped || step.ExitCode == 0);

        await PythonReactGate.AssertFrontendBuildsAsync(frontendDir);
        await PythonReactGate.AssertBackendTestsPassAsync(venvPython, backendDir);
    }

    /// <summary>
    /// The Vite half of <see cref="V1TemplateCompileTests.PersonalizedPalette_ReachesTheBuiltStylesheet"/>.
    /// The same v3-shaped palette config, routed to <c>frontend/tailwind.config.ts</c>, only
    /// reaches the CSS through <c>src/index.css</c>'s <c>@config "../tailwind.config.ts"</c> and
    /// the <c>@tailwindcss/vite</c> plugin. Without that line the build stays green and the
    /// customer's colors vanish.
    /// </summary>
    [Fact]
    public async Task PersonalizedPalette_ReachesTheBuiltStylesheet()
    {
        if (!IntegrationToolchain.Available(ProcessCommandResolver.Npm, "--version", "npm", requiredOnCi: true))
            return;

        Render(PaletteResponse);
        var frontendDir = Path.Combine(OutputDir, "frontend");

        await PythonReactGate.AssertFrontendInstallsFromLockfileAsync(frontendDir);
        await PythonReactGate.AssertFrontendBuildsAsync(frontendDir);

        TailwindStylesheet.AssertPersonalizedPaletteCompiled(Path.Combine(frontendDir, "dist"));
    }

    /// <summary>A model's answer to the Color Theme section: a whole config plus a page using it.</summary>
    private const string PaletteResponse = $$"""
        [[FILE:frontend/tailwind.config.ts]]
        {{TailwindStylesheet.PersonalizedConfig}}
        [[END_FILE]]
        [[FILE:frontend/src/App.tsx]]
        export default function App() {
          return (
            <main className="min-h-screen bg-gray-50 p-8">
              <h1 className="text-3xl font-bold text-accent">InvoiceHub</h1>
              <p className="mt-2 bg-primary text-white">Generated by StackAlchemist.</p>
            </main>
          );
        }
        [[END_FILE]]
        """;
}
