namespace StackAlchemist.Engine.Services;

/// <summary>
/// One definition of "this template file belongs to a DIFFERENT templating engine and must
/// reach the customer byte-for-byte".
///
/// The Tier-3 deliverable ships a Helm chart, and Helm charts are Go <c>text/template</c>
/// files that use the same <c>{{ }}</c> delimiters Handlebars does. They are not merely
/// mangled by a Handlebars pass — they cannot survive one at all:
/// <c>{{- toYaml .Values.resources | nindent 12 }}</c> parses as a Handlebars block-params
/// declaration and <see cref="TemplateProvider.Render"/> throws
/// <c>HandlebarsCompilerException: blockParams definition has incorrect syntax</c>. That
/// exception propagates out of <c>GenerationOrchestrator.AppendTier3InfrastructureFiles</c>
/// into the orchestrator's catch, so before this existed EVERY Tier-3 generation ended
/// <c>Failed</c> with no archive — the tier could not deliver at all.
///
/// The rest of the Tier-3 set is ordinary Handlebars (<c>DEPLOYMENT.md</c> uses a real
/// <c>{{#each Entities}}</c> block), so the exemption has to be per-file, not per-set.
/// Files exempted here therefore carry NO Handlebars tokens of our own: anything we would
/// want substituted inside a chart template is expressed in Go template terms instead
/// (<c>{{ .Chart.Name }}</c>), because a token left in an exempt file ships verbatim.
/// </summary>
internal static class ForeignTemplatePaths
{
    private const string HelmSegment = "helm";
    private const string ChartTemplatesSegment = "templates";

    /// <summary>
    /// True when a relative, '/'-separated file path sits inside a Helm chart's
    /// <c>templates/</c> directory — i.e. under a <c>helm</c> segment followed by a later
    /// <c>templates</c> segment (<c>infra/helm/templates/deployment.yaml</c>, and equally a
    /// subchart at <c>infra/helm/charts/api/templates/…</c>).
    ///
    /// <c>values.yaml</c> and <c>Chart.yaml</c> deliberately do NOT match: they are plain
    /// YAML with no Go template syntax, so they stay on the Handlebars path and keep getting
    /// the project name substituted.
    /// </summary>
    public static bool IsForeignTemplate(string relativeFilePath)
    {
        var segments = relativeFilePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        // The last segment is the file name; only directory segments are examined.
        var helmIndex = Array.FindIndex(
            segments, 0, segments.Length - 1,
            s => string.Equals(s, HelmSegment, StringComparison.OrdinalIgnoreCase));

        if (helmIndex < 0)
            return false;

        return Array.FindIndex(
            segments, helmIndex + 1, segments.Length - helmIndex - 2,
            s => string.Equals(s, ChartTemplatesSegment, StringComparison.OrdinalIgnoreCase)) >= 0;
    }
}
