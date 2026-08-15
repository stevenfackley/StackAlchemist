using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Pins the boundary of the Handlebars exemption. Both directions matter and for opposite
/// reasons: a chart template that is NOT exempted throws and kills the whole Tier-3
/// generation, while a file that is exempted by mistake ships its <c>{{ProjectName}}</c>
/// tokens verbatim to the buyer.
/// </summary>
public sealed class ForeignTemplatePathsTests
{
    [Theory]
    [InlineData("infra/helm/templates/deployment.yaml")]
    [InlineData("infra/helm/templates/_helpers.tpl")]
    [InlineData("infra/helm/templates/NOTES.txt")]
    [InlineData("infra/helm/charts/api/templates/service.yaml")]
    public void HelmChartTemplates_AreExemptFromHandlebars(string path) =>
        ForeignTemplatePaths.IsForeignTemplate(path).Should().BeTrue(
            "Helm chart templates are Go text/template and a Handlebars pass over one throws");

    [Theory]
    // Plain YAML with our tokens and no Go template syntax — these MUST still be rendered.
    [InlineData("infra/helm/values.yaml")]
    [InlineData("infra/helm/Chart.yaml")]
    // Nothing outside a chart is exempt, however suggestive the name.
    [InlineData("infra/cdk/bin/app.ts")]
    [InlineData("infra/terraform/main.tf")]
    [InlineData("DEPLOYMENT.md")]
    [InlineData("nextjs/src/templates/page.tsx")]
    [InlineData("dotnet/Program.cs")]
    public void EverythingElse_StaysOnTheHandlebarsPath(string path) =>
        ForeignTemplatePaths.IsForeignTemplate(path).Should().BeFalse();

    [Fact]
    public void AFileNamedTemplatesInsideHelm_IsNotMistakenForTheDirectory() =>
        ForeignTemplatePaths.IsForeignTemplate("infra/helm/templates").Should().BeFalse(
            "only directory segments are examined; the last segment is the file name");
}
