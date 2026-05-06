using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface IInjectionEngine
{
    /// <summary>
    /// Fill every [[LLM_INJECTION_START/END]] zone in the rendered templates by dispatching
    /// per-zone LLM calls in parallel. Returns an InjectionResult with filled templates
    /// (markers stripped) and aggregated token usage.
    /// </summary>
    Task<InjectionResult> FillZonesAsync(
        Dictionary<string, string> renderedTemplates,
        GenerationSchema schema,
        TemplateVariables templateVariables,
        ProjectType projectType,
        GenerationPersonalization? personalization,
        CancellationToken ct = default);
}

public sealed class InjectionEngineOptions
{
    public int MaxConcurrency { get; init; } = 4;
    public int MaxAttemptsPerZone { get; init; } = 2;
}

public sealed class InjectionFailedException : Exception
{
    public InjectionFailedException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed partial class InjectionEngine(
    ITemplateProvider templateProvider,
    IPromptBuilderService promptBuilder,
    ILlmClient llmClient,
    ILogger<InjectionEngine> logger,
    InjectionEngineOptions? options = null) : IInjectionEngine
{
    private readonly InjectionEngineOptions _options = options ?? new InjectionEngineOptions();

    public async Task<InjectionResult> FillZonesAsync(
        Dictionary<string, string> renderedTemplates,
        GenerationSchema schema,
        TemplateVariables templateVariables,
        ProjectType projectType,
        GenerationPersonalization? personalization,
        CancellationToken ct = default)
    {
        var workItems = new List<(string FilePath, string ZoneName)>();
        foreach (var (filePath, content) in renderedTemplates)
        {
            foreach (var zone in templateProvider.FindInjectionZones(content))
            {
                workItems.Add((filePath, zone));
            }
        }

        if (workItems.Count == 0)
        {
            return new InjectionResult(
                FilledTemplates: new Dictionary<string, string>(renderedTemplates),
                TotalInputTokens: 0,
                TotalOutputTokens: 0,
                Model: "n/a",
                ZonesFilled: 0);
        }

        logger.LogInformation(
            "InjectionEngine: filling {ZoneCount} zones across {FileCount} files (concurrency={Concurrency})",
            workItems.Count,
            workItems.Select(w => w.FilePath).Distinct().Count(),
            _options.MaxConcurrency);

        using var sem = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        var results = new ConcurrentDictionary<(string FilePath, string ZoneName), string>();
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        string? observedModel = null;

        var tasks = workItems.Select(async item =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var fileContent = renderedTemplates[item.FilePath];
                var entity = FindEntityForFile(item.FilePath, templateVariables.Entities);

                var promptCtx = new InjectionPromptContext(
                    FilePath: item.FilePath,
                    ZoneName: item.ZoneName,
                    RenderedFileContent: fileContent,
                    Schema: schema)
                {
                    ProjectType = projectType,
                    Personalization = personalization,
                    Entity = entity,
                };

                var prompt = promptBuilder.BuildInjectionPrompt(promptCtx);
                var (content, response) = await CallLlmWithRetryAsync(prompt, item, ct);
                results[(item.FilePath, item.ZoneName)] = content;
                Interlocked.Add(ref totalInputTokens, response.InputTokens);
                Interlocked.Add(ref totalOutputTokens, response.OutputTokens);
                observedModel ??= response.Model;
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);

        var filled = new Dictionary<string, string>(renderedTemplates);
        foreach (var fileGroup in results.GroupBy(kv => kv.Key.FilePath))
        {
            var content = filled[fileGroup.Key];
            foreach (var ((_, zoneName), zoneContent) in fileGroup)
            {
                content = templateProvider.InjectIntoZone(content, zoneName, zoneContent);
            }
            filled[fileGroup.Key] = templateProvider.StripInjectionMarkers(content);
        }

        return new InjectionResult(
            FilledTemplates: filled,
            TotalInputTokens: totalInputTokens,
            TotalOutputTokens: totalOutputTokens,
            Model: observedModel ?? "unknown",
            ZonesFilled: results.Count);
    }

    private async Task<(string Content, LlmResponse Response)> CallLlmWithRetryAsync(
        string prompt,
        (string FilePath, string ZoneName) item,
        CancellationToken ct)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= _options.MaxAttemptsPerZone; attempt++)
        {
            try
            {
                var response = await llmClient.GenerateAsync(systemPrompt: string.Empty, userPrompt: prompt, ct);
                if (string.IsNullOrWhiteSpace(response.Text))
                {
                    logger.LogWarning(
                        "Empty LLM response for zone {Zone} in {File} (attempt {Attempt}/{Max})",
                        item.ZoneName, item.FilePath, attempt, _options.MaxAttemptsPerZone);
                    continue;
                }

                return (CleanZoneContent(response.Text), response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                logger.LogWarning(
                    ex,
                    "LLM call failed for zone {Zone} in {File} (attempt {Attempt}/{Max})",
                    item.ZoneName, item.FilePath, attempt, _options.MaxAttemptsPerZone);
            }
        }

        throw new InjectionFailedException(
            $"Zone '{item.ZoneName}' in '{item.FilePath}' failed to fill after {_options.MaxAttemptsPerZone} attempts.",
            lastException);
    }

    private static string CleanZoneContent(string raw)
    {
        var trimmed = raw.Trim();
        trimmed = MarkdownFenceRegex().Replace(trimmed, string.Empty);
        trimmed = StrayFileMarkerRegex().Replace(trimmed, string.Empty);
        return trimmed.Trim();
    }

    private static TemplateEntity? FindEntityForFile(string filePath, List<TemplateEntity> entities)
    {
        return entities
            .Where(e => filePath.Contains(e.Name, StringComparison.Ordinal))
            .OrderByDescending(e => e.Name.Length)
            .FirstOrDefault();
    }

    [GeneratedRegex(@"^```\w*\s*\n?|\n?```\s*$", RegexOptions.Multiline)]
    private static partial Regex MarkdownFenceRegex();

    [GeneratedRegex(@"\[\[(FILE:[^\]]*|END_FILE)\]\]\s*\n?")]
    private static partial Regex StrayFileMarkerRegex();
}
