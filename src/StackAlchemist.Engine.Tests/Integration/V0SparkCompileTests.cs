using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The compile gate for the Tier-0 (Spark / free) deliverable — the template every visitor
/// can generate, and until now the only shipping template with no build gate at all.
///
/// Production renders this set in <c>GenerationOrchestrator.RenderTier0Preview</c>:
/// <c>TemplateProvider.LoadTemplate("V0-Spark-NextJs")</c> + <c>Render(variables)</c>, no
/// Reconstruct, no LLM. The rendered files are written inline into the generation row and
/// booted by StackBlitz WebContainers in the visitor's browser. This gate renders through
/// the same two calls and then proves the tree actually installs and builds with the
/// pinned dependency versions — which is exactly the surface Dependabot bumps touch.
///
/// Deliberately NO package-lock.json in this template: WebContainers installs fresh from
/// package.json (exact pins), and a ~300 KB lockfile would ride along inside
/// <c>preview_files_json</c> on every free generation row. Without a lock there is no
/// manifest/lock desync class here — this gate is what stands between a bad version pin
/// and a Spark demo that no longer boots.
///
/// Toolchain-guarded via <see cref="IntegrationToolchain"/>: skips locally when npm is
/// missing, hard-fails on CI so the gate cannot go quiet.
/// </summary>
public sealed class V0SparkCompileTests : IDisposable
{
    private const string TemplateSetName = "V0-Spark-NextJs";

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "sa-v0-spark-gate-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    /// <summary>
    /// Mirrors <c>GenerationOrchestrator.RenderTier0Preview</c> byte-for-byte: load the real
    /// on-disk set, render with real variables, write out. No Reconstruct — the Spark path
    /// never runs it.
    /// </summary>
    private IReadOnlyList<string> RenderTo(string outputDirectory)
    {
        var provider = new TemplateProvider(
            new System.IO.Abstractions.FileSystem(), V1TemplateHarness.ResolveTemplatesRoot());

        var files = provider.Render(
            provider.LoadTemplate(TemplateSetName), V1TemplateHarness.SampleVariables());

        Directory.CreateDirectory(outputDirectory);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return [.. files.Keys.OrderBy(p => p, StringComparer.Ordinal)];
    }

    [Fact]
    public void Render_ProducesABootableTreeWithNoUnresolvedScaffolding()
    {
        var files = RenderTo(_outputDir);

        files.Should().Contain("package.json");
        files.Should().Contain("next.config.mjs");
        files.Should().Contain(p => p.StartsWith("app/", StringComparison.Ordinal),
            "the Spark demo is an App Router app — an empty app/ means a blank preview");

        files.Should().NotContain("package-lock.json",
            "the Spark template deliberately ships no lockfile — it would ride along inside "
            + "preview_files_json on every free generation row (see class doc)");

        foreach (var relativePath in files)
        {
            var content = File.ReadAllText(Path.Combine(_outputDir, relativePath));

            content.Should().NotContain("{{",
                $"{relativePath} has an unsubstituted Handlebars token — WebContainers would "
                + "boot it verbatim in front of the visitor");
            content.Should().NotContain("[[LLM_INJECTION_",
                $"{relativePath} carries pipeline scaffolding the Spark path has no way to strip");
        }
    }

    /// <summary>
    /// Installs and builds the rendered tree with the template's exact version pins — the
    /// property a Dependabot bump can silently break, and the reason V0 bumps were
    /// semver-trust-only before this gate existed (StackAlchemist#313).
    /// <c>npm install</c> rather than <c>npm ci</c> because the template ships no lockfile
    /// by design; the pins are exact, so the install is still deterministic per version.
    /// </summary>
    [Fact]
    public async Task RenderedSparkTemplate_InstallsAndBuilds()
    {
        if (!IntegrationToolchain.Available(ProcessCommandResolver.Npm, "--version", "npm", requiredOnCi: true))
            return;

        RenderTo(_outputDir);

        var (installExit, installLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm,
            "install --no-audit --no-fund",
            _outputDir,
            TimeSpan.FromMinutes(10));

        installExit.Should().Be(0,
            $"the Spark demo's dependency pins must be installable — WebContainers runs this "
            + $"same install in the visitor's browser.\n\n{IntegrationToolchain.Tail(installLog)}");

        var (buildExit, buildLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm,
            "run build",
            _outputDir,
            TimeSpan.FromMinutes(10));

        buildExit.Should().Be(0,
            $"`next build` over the rendered Spark tree is the cheapest proof the free-tier "
            + $"app still compiles with the pinned versions.\n\n{IntegrationToolchain.Tail(buildLog)}");

        Directory.Exists(Path.Combine(_outputDir, ".next"))
            .Should().BeTrue("`next build` must have produced output, not been skipped");
    }
}
