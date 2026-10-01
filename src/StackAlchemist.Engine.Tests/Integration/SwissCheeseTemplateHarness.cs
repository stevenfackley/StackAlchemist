using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Renders a REAL on-disk V2 ("Swiss Cheese") template set the way
/// <c>GenerationOrchestrator.GenerateFilesAsync</c> does when <c>Generation:UseSwissCheese</c>
/// is on: <see cref="TemplateProvider.LoadTemplate"/> → <see cref="TemplateProvider.Render"/> →
/// <see cref="InjectionEngine.FillZonesAsync"/>, every one of them the shipping class, over the
/// same <see cref="V1TemplateHarness.SampleVariables"/> InvoiceHub schema the V1 gates use.
///
/// The model is the one stand-in, and it cannot simply be left out the way the V1 harness
/// leaves it out. "The LLM contributed nothing" is a state the V1 path really produces (an empty
/// block set through Reconstruct); V2 has no such state — <see cref="InjectionEngine"/> treats
/// an empty fill as a failed attempt and throws <see cref="InjectionFailedException"/> once the
/// retries run out, so every zone that reaches an archive was filled by something.
/// <see cref="TemplateDefaultLlmClient"/> is the deterministic, free "something": it answers
/// each zone with the template's own placeholder body for that zone, indented the way the
/// prompt tells a model to indent it. The output is therefore the template exactly as authored,
/// carried through the real InjectIntoZone / CleanZoneContent / StripInjectionMarkers path —
/// which is the part a renderer that merely stripped markers would not exercise.
/// </summary>
internal static partial class SwissCheeseTemplateHarness
{
    /// <summary>
    /// Loads → renders → fills every zone through the real <see cref="InjectionEngine"/> →
    /// writes to <paramref name="outputDirectory"/>.
    /// </summary>
    public static async Task<SwissCheeseRender> RenderToAsync(
        string templateSetName, ProjectType projectType, string outputDirectory)
    {
        var provider = new TemplateProvider(new FileSystem(), V1TemplateHarness.ResolveTemplatesRoot());
        var variables = V1TemplateHarness.SampleVariables();

        var engine = new InjectionEngine(
            provider,
            new PromptBuilderService(),
            new TemplateDefaultLlmClient(),
            NullLogger<InjectionEngine>.Instance);

        var rendered = provider.Render(provider.LoadTemplate(templateSetName), variables);
        var injection = await engine.FillZonesAsync(
            rendered,
            V1TemplateHarness.SampleSchema(),
            variables,
            projectType,
            personalization: null);

        Directory.CreateDirectory(outputDirectory);
        foreach (var (relativePath, content) in injection.FilledTemplates)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return new SwissCheeseRender(
            [.. injection.FilledTemplates.Keys.OrderBy(p => p, StringComparer.Ordinal)],
            injection.ZonesFilled);
    }

    /// <summary>
    /// Answers every zone prompt with the placeholder body the template ships for that zone.
    ///
    /// It reads the zone back out of the prompt <see cref="PromptBuilderService.BuildInjectionPrompt"/>
    /// actually built — the "Surrounding File" section carries the rendered file, markers and
    /// all — rather than out of a side channel, so a prompt that stopped carrying the zone would
    /// fail here, loudly, instead of being papered over.
    /// </summary>
    private sealed partial class TemplateDefaultLlmClient : ILlmClient
    {
        private const string SurroundingFileHeading = "## Surrounding File";

        public Task<LlmResponse> GenerateAsync(
            string systemPrompt, string userPrompt, LlmCallOptions? options = null, CancellationToken ct = default)
        {
            var zone = ZoneNameRegex().Match(userPrompt);
            var fileSection = userPrompt.IndexOf(SurroundingFileHeading, StringComparison.Ordinal);
            if (!zone.Success || fileSection < 0)
            {
                throw new InvalidOperationException(
                    "The injection prompt no longer names its zone or carries the surrounding file — "
                    + "TemplateDefaultLlmClient cannot recover the template's placeholder body.");
            }

            var zoneName = Regex.Escape(zone.Groups["zone"].Value);
            var body = Regex.Match(
                userPrompt[fileSection..],
                $@"\[\[LLM_INJECTION_START:\s*{zoneName}\s*\]\][^\n]*\n(?<body>.*?)^[ \t]*\[\[LLM_INJECTION_END:\s*{zoneName}\s*\]\]",
                RegexOptions.Singleline | RegexOptions.Multiline);

            if (!body.Success)
            {
                throw new InvalidOperationException(
                    $"Zone '{zone.Groups["zone"].Value}' is not delimited in the prompt's surrounding file.");
            }

            return Task.FromResult(new LlmResponse(
                body.Groups["body"].Value.TrimEnd(),
                InputTokens: 0,
                OutputTokens: 0,
                Model: "template-default"));
        }

        [GeneratedRegex(@"^Zone name: `(?<zone>[^`]+)`", RegexOptions.Multiline)]
        private static partial Regex ZoneNameRegex();
    }
}

/// <summary>What <see cref="SwissCheeseTemplateHarness.RenderToAsync"/> wrote, and how many zones the engine filled.</summary>
internal sealed record SwissCheeseRender(IReadOnlyList<string> Files, int ZonesFilled);
