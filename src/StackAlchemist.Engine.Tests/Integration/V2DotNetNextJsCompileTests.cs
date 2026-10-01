using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The compile gate for <c>V2-DotNet-NextJs</c> — the template set
/// <c>GenerationOrchestrator</c> loads for a .NET + Next.js generation when
/// <c>Generation:UseSwissCheese</c> is on.
///
/// That flag is off in production today, which is exactly why this set had no gate: nothing
/// rendered it for real, nothing built it, and its <c>nextjs/package.json</c> took Dependabot
/// bumps on semver trust alone (StackAlchemist#313). The day the flag flips, this tree becomes
/// the Tier-2 deliverable; the gate makes sure it compiles before that day rather than on a
/// paying customer's generation. Its first run proved the point: <c>next build</c> failed
/// prerendering every per-entity page, because each one fetches the generated API at build
/// time, when nothing is listening (fixed in the template with <c>dynamic = "force-dynamic"</c>).
///
/// Rendered through <see cref="SwissCheeseTemplateHarness"/> (the real V2 path: load → render
/// per entity → fill every zone through the real <see cref="InjectionEngine"/>), then built by
/// the real <see cref="DotNetBuildStrategy"/> — the same strategy, and the same assertions on
/// its recorded steps, as <see cref="V1TemplateCompileTests.RenderedTemplate_BuildsBothHalves"/>.
///
/// The set ships a committed <c>nextjs/package-lock.json</c>. The strategy installs with
/// <c>npm ci</c> first and only falls back to <c>npm install</c> when that fails, keeping the
/// failed step as superseded evidence; the gate refuses any failed step, so a manifest/lock
/// desync — the class V1's gate caught on 2026-08-19 (#312) — fails here instead of being
/// quietly re-resolved.
///
/// Toolchain-guarded via <see cref="IntegrationToolchain"/>: skips locally when dotnet or npm is
/// missing, hard-fails on CI so the gate cannot go quiet.
/// </summary>
public sealed class V2DotNetNextJsCompileTests : IDisposable
{
    private const string TemplateSetName = "V2-DotNet-NextJs";

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "sa-v2-dotnet-gate-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, recursive: true); }
        catch { /* best effort: node_modules on Windows resists deletion */ }
    }

    private Task<SwissCheeseRender> RenderAsync() =>
        SwissCheeseTemplateHarness.RenderToAsync(TemplateSetName, ProjectType.DotNetNextJs, _outputDir);

    [Fact]
    public async Task Render_FillsEveryZoneAndLeavesNoScaffolding()
    {
        var render = await RenderAsync();
        var files = render.Files;

        render.ZonesFilled.Should().BeGreaterThan(0,
            "the gate must exercise the InjectionEngine, not a tree with no zones in it");

        files.Should().Contain("dotnet/InvoiceHub.csproj",
            "the .csproj filename token must be substituted, not left as {{ProjectName}}");
        files.Should().Contain(
            ["dotnet/Repositories/CustomerRepository.cs", "dotnet/Repositories/InvoiceRepository.cs",
             "dotnet/Repositories/LineItemRepository.cs"],
            "per-entity templates render once per schema entity");
        files.Should().Contain(
            ["nextjs/src/app/customers/page.tsx", "nextjs/src/app/invoices/page.tsx",
             "nextjs/src/app/lineitems/page.tsx"]);
        files.Should().Contain("nextjs/package-lock.json",
            "the build installs with `npm ci`, which requires a lockfile");

        files.Should().NotContain(p => p.Contains("/obj/", StringComparison.Ordinal)
                                    || p.Contains("/bin/", StringComparison.Ordinal)
                                    || p.Contains("node_modules", StringComparison.Ordinal),
            "build residue in the template tree must never reach a customer archive");

        foreach (var relativePath in files)
        {
            var content = File.ReadAllText(Path.Combine(_outputDir, relativePath));

            content.Should().NotContain("[[LLM_INJECTION_",
                $"{relativePath} would ship pipeline scaffolding to the customer");
            content.Should().NotContain("{{",
                $"{relativePath} has an unsubstituted Handlebars token");
        }
    }

    [Fact]
    public async Task RenderedTemplate_BuildsBothHalves()
    {
        if (!IntegrationToolchain.Available("dotnet", "--version", "the .NET SDK", requiredOnCi: true))
            return;

        if (!IntegrationToolchain.Available(ProcessCommandResolver.Npm, "--version", "npm", requiredOnCi: true))
            return;

        await RenderAsync();

        var strategy = new DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        var result = await strategy.ExecuteBuildAsync(_outputDir, cts.Token);

        var transcript = $"{result.StandardOutput}\n{result.ErrorOutput}";

        result.IsSuccess.Should().BeTrue(
            because: "the V2 template is the baseline every Swiss-Cheese generation fills in; "
                     + $"if it does not compile as authored, no fill can rescue it.\n\n{transcript}");

        result.Steps.Where(step => step.Half == BuildHalf.DotNet && !step.Skipped)
            .Select(step => step.Command)
            .Should().Contain(["dotnet restore", "dotnet build --no-restore"],
                $"the .NET half must actually build.\n\n{transcript}");

        result.Steps.Where(step => step.Half == BuildHalf.NextJs && !step.Skipped)
            .Select(step => step.Command)
            .Should().Contain(["npm ci", "npm run build"],
                $"the Next.js half must install from the committed lockfile and build.\n\n{transcript}");

        // A failed `npm ci` that the strategy recovered from with `npm install` still counts:
        // that is the manifest/lock desync this gate exists to refuse.
        result.Steps.Should().OnlyContain(step => step.Skipped || step.ExitCode == 0,
            $"every step must pass — a superseded `npm ci` means package.json and "
            + $"package-lock.json disagree.\n\n{transcript}");

        // BuildNextJsAsync reports success when nextjs/package.json is absent; BUILD_ID is only
        // written by a `next build` that ran to completion.
        File.Exists(Path.Combine(_outputDir, "nextjs", ".next", "BUILD_ID"))
            .Should().BeTrue("`next build` must have produced output, not been skipped");

        TailwindStylesheet.AssertCompiled(
            Path.Combine(_outputDir, "nextjs", ".next", "static"), ".min-h-screen", ".text-blue-600");

        // This was `next lint`, which Next 16 removed: the script died before linting anything (#291).
        var (lintExit, lintLog) = await IntegrationToolchain.RunAsync(
            ProcessCommandResolver.Npm, "run lint", Path.Combine(_outputDir, "nextjs"), TimeSpan.FromMinutes(5));

        lintExit.Should().Be(0,
            "the archive ships `npm run lint`; it must run, and pass, on the template as rendered."
            + $"\n\n{IntegrationToolchain.Tail(lintLog)}");
    }
}
