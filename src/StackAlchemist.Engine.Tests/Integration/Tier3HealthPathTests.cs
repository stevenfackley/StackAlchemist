using FluentAssertions;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// The Infrastructure tier's CDK target group, Terraform default and Helm probes all health-check
/// one path. Every stack's API has to serve it, or a $999 deploy marks a healthy service unhealthy
/// and cycles it. The .NET templates mapped no such route until this test existed.
/// </summary>
public sealed class Tier3HealthPathTests
{
    private const string HealthPath = "/healthz";

    private static string Template(params string[] parts) =>
        Path.Combine([V1TemplateHarness.ResolveTemplatesRoot(), .. parts]);

    [Fact]
    public void Tier3Infrastructure_ChecksTheHealthPath()
    {
        File.ReadAllText(Template("Tier3-Infrastructure", "infra", "helm", "templates", "deployment.yaml"))
            .Should().Contain($"path: {HealthPath}");
        File.ReadAllText(Template("Tier3-Infrastructure", "infra", "terraform", "variables.tf"))
            .Should().Contain($"\"{HealthPath}\"");
        File.ReadAllText(Template("Tier3-Infrastructure", "infra", "cdk", "lib", "{{ProjectNameKebab}}-stack.ts"))
            .Should().Contain($"\"{HealthPath}\"");
    }

    [Theory]
    [InlineData("V1-DotNet-NextJs", "dotnet", "Program.cs")]
    [InlineData("V2-DotNet-NextJs", "dotnet", "Program.cs")]
    [InlineData("V1-Python-React", "backend", "app", "main.py")]
    [InlineData("V2-Python-React", "backend", "app", "main.py")]
    public void EveryStackApi_ServesTheHealthPath(params string[] path) =>
        File.ReadAllText(Template(path)).Should().Contain($"\"{HealthPath}\"",
            "the Infrastructure tier health-checks it on every stack");
}
