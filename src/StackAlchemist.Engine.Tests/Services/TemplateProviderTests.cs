using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class TemplateProviderTests
{
    private readonly MockFileSystem _fs;
    private readonly TemplateProvider _sut;

    public TemplateProviderTests()
    {
        _fs = new MockFileSystem();
        _fs.AddDirectory("/templates/V1-DotNet-NextJs");
        _sut = new TemplateProvider(_fs, "/templates");
    }

    [Fact]
    public void Render_WithAllVariablesProvided_ReplacesAllPlaceholders()
    {
        var templates = new Dictionary<string, string>
        {
            ["dotnet/{{ProjectName}}.csproj"] = "<RootNamespace>{{ProjectName}}</RootNamespace>",
            ["README.md"] = "# {{ProjectName}} ({{ProjectNameKebab}})",
        };
        var vars = new TemplateVariables
        {
            ProjectName = "MyApp",
            ProjectNameKebab = "my-app",
            ProjectNameLower = "myapp",
            DbConnectionString = "Host=localhost",
            FrontendUrl = "http://localhost:3000",
        };

        var result = _sut.Render(templates, vars);

        result.Should().ContainKey("dotnet/MyApp.csproj");
        result["dotnet/MyApp.csproj"].Should().Be("<RootNamespace>MyApp</RootNamespace>");
        result["README.md"].Should().Be("# MyApp (my-app)");
    }

    [Fact]
    public void FindInjectionZones_WithValidTemplate_ReturnsAllZones()
    {
        var template = """
            // Static code above
            {{!-- LLM_INJECTION_START: Controllers --}}
            {{!-- LLM_INJECTION_END: Controllers --}}
            // Static code below
            {{!-- LLM_INJECTION_START: Repositories --}}
            {{!-- LLM_INJECTION_END: Repositories --}}
            """;

        var zones = _sut.FindInjectionZones(template);

        zones.Should().HaveCount(2);
        zones.Should().Contain("Controllers");
        zones.Should().Contain("Repositories");
    }

    [Fact]
    public void InjectIntoZone_WithValidContent_PlacesContentBetweenMarkers()
    {
        var template = """
            // Before
            {{!-- LLM_INJECTION_START: Controllers --}}
            {{!-- LLM_INJECTION_END: Controllers --}}
            // After
            """;
        var content = "public class ProductsController { }";

        var result = _sut.InjectIntoZone(template, "Controllers", content);

        result.Should().Contain("public class ProductsController { }");
        result.Should().Contain("// Before");
        result.Should().Contain("// After");
        result.Should().Contain("LLM_INJECTION_START: Controllers");
        result.Should().Contain("LLM_INJECTION_END: Controllers");
    }

    [Fact]
    public void LoadTemplate_WithNonexistentTemplate_ThrowsTemplateNotFoundException()
    {
        var act = () => _sut.LoadTemplate("V99-NonExistent");

        act.Should().Throw<TemplateNotFoundException>()
           .WithMessage("*V99-NonExistent*");
    }

    [Fact]
    public void LoadTemplate_WithValidTemplate_ReturnsAllFiles()
    {
        _fs.AddFile("/templates/V1-DotNet-NextJs/dotnet/Program.cs", new MockFileData("var app = builder.Build();"));
        _fs.AddFile("/templates/V1-DotNet-NextJs/nextjs/package.json", new MockFileData("{}"));

        var result = _sut.LoadTemplate("V1-DotNet-NextJs");

        result.Should().HaveCount(2);
        result.Should().ContainKey("dotnet/Program.cs");
        result.Should().ContainKey("nextjs/package.json");
    }

    [Fact]
    public void FindInjectionZones_NoZones_ReturnsEmpty()
    {
        var zones = _sut.FindInjectionZones("plain code, no zones");
        zones.Should().BeEmpty();
    }

    [Fact]
    public void Render_WithEntities_IteratesEntityList()
    {
        var templates = new Dictionary<string, string>
        {
            ["test.txt"] = "{{#each Entities}}{{Name}},{{/each}}",
        };
        var vars = new TemplateVariables
        {
            ProjectName = "App",
            ProjectNameKebab = "app",
            ProjectNameLower = "app",
            DbConnectionString = "x",
            FrontendUrl = "x",
            Entities =
            [
                new TemplateEntity { Name = "User", NameLower = "user", TableName = "users" },
                new TemplateEntity { Name = "Post", NameLower = "post", TableName = "posts" },
            ],
        };

        var result = _sut.Render(templates, vars);

        result["test.txt"].Should().Be("User,Post,");
    }
}
