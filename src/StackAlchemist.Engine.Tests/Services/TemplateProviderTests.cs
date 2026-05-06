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
            [[LLM_INJECTION_START: Controllers]]
            [[LLM_INJECTION_END: Controllers]]
            // Static code below
            [[LLM_INJECTION_START: Repositories]]
            [[LLM_INJECTION_END: Repositories]]
            """;

        var zones = _sut.FindInjectionZones(template);

        zones.Should().HaveCount(2);
        zones.Should().Contain("Controllers");
        zones.Should().Contain("Repositories");
    }

    [Fact]
    public void StripInjectionMarkers_RemovesAllMarkerLines()
    {
        var content = """
            line A
            [[LLM_INJECTION_START: GetAllImpl]]
            return await conn.QueryAsync<Product>("SELECT * FROM products");
            [[LLM_INJECTION_END: GetAllImpl]]
            line B
            [[LLM_INJECTION_START: CreateImpl]]
            insert sql here
            [[LLM_INJECTION_END: CreateImpl]]
            line C
            """;

        var result = _sut.StripInjectionMarkers(content);

        result.Should().NotContain("LLM_INJECTION_START");
        result.Should().NotContain("LLM_INJECTION_END");
        result.Should().Contain("line A");
        result.Should().Contain("line B");
        result.Should().Contain("line C");
        result.Should().Contain("return await conn.QueryAsync");
        result.Should().Contain("insert sql here");
    }

    [Fact]
    public void StripInjectionMarkers_OnlyTouchesMarkerLines()
    {
        var content = "no markers here\njust regular code";
        _sut.StripInjectionMarkers(content).Should().Be(content);
    }

    [Fact]
    public void InjectIntoZone_WithValidContent_PlacesContentBetweenMarkers()
    {
        var template = """
            // Before
            [[LLM_INJECTION_START: Controllers]]
            [[LLM_INJECTION_END: Controllers]]
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
    public void Render_WithIfBlock_HonorsConditional()
    {
        var templates = new Dictionary<string, string>
        {
            ["test.txt"] = "{{#each Entities}}{{Name}}{{#if Fields}}!{{/if}},{{/each}}",
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
                new TemplateEntity
                {
                    Name = "User",
                    NameLower = "user",
                    TableName = "users",
                    Fields = [new TemplateField { Name = "Id", NameLower = "id", Type = "Guid", SqlType = "UUID", IsPrimaryKey = true }],
                },
                new TemplateEntity { Name = "Post", NameLower = "post", TableName = "posts" },
            ],
        };

        var result = _sut.Render(templates, vars);

        result["test.txt"].Should().Be("User!,Post,");
    }

    [Fact]
    public void Render_DoesNotHtmlEscapeAngleBrackets()
    {
        var templates = new Dictionary<string, string>
        {
            ["repo.cs"] = "Task<IEnumerable<{{ProjectName}}>> GetAll();",
        };
        var vars = new TemplateVariables
        {
            ProjectName = "Product",
            ProjectNameKebab = "product",
            ProjectNameLower = "product",
            DbConnectionString = "x",
            FrontendUrl = "x",
        };

        var result = _sut.Render(templates, vars);

        result["repo.cs"].Should().Be("Task<IEnumerable<Product>> GetAll();");
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

    [Fact]
    public void Render_PerEntityTemplate_ProducesOneFilePerEntity()
    {
        var templates = new Dictionary<string, string>
        {
            ["Models/{{EntityName}}.cs"] = "namespace {{ProjectName}}.Models; public record {{EntityName}}();",
        };
        var vars = TwoEntityVariables();

        var result = _sut.Render(templates, vars);

        result.Should().HaveCount(2);
        result.Should().ContainKey("Models/User.cs");
        result.Should().ContainKey("Models/Post.cs");
        result["Models/User.cs"].Should().Be("namespace App.Models; public record User();");
        result["Models/Post.cs"].Should().Be("namespace App.Models; public record Post();");
    }

    [Fact]
    public void Render_PerEntityTemplate_ExposesEntityFieldsAndTableName()
    {
        var templates = new Dictionary<string, string>
        {
            ["repos/{{EntityName}}.cs"] = "// {{TableName}}\n{{#each Fields}}{{Name}}:{{Type}};{{/each}}",
        };
        var vars = TwoEntityVariables();

        var result = _sut.Render(templates, vars);

        result["repos/User.cs"].Should().Contain("// users");
        result["repos/User.cs"].Should().Contain("Name:string;");
        result["repos/Post.cs"].Should().Contain("// posts");
        result["repos/Post.cs"].Should().Contain("Title:string;");
    }

    [Fact]
    public void Render_PerEntityTemplate_WithNoEntities_ProducesNoOutput()
    {
        var templates = new Dictionary<string, string>
        {
            ["Models/{{EntityName}}.cs"] = "public record {{EntityName}}();",
            ["Program.cs"] = "// schema-wide\n{{ProjectName}}",
        };
        var vars = new TemplateVariables
        {
            ProjectName = "Empty",
            ProjectNameKebab = "empty",
            ProjectNameLower = "empty",
            DbConnectionString = "x",
            FrontendUrl = "x",
            Entities = [],
        };

        var result = _sut.Render(templates, vars);

        result.Should().HaveCount(1);
        result.Should().ContainKey("Program.cs");
        result.Should().NotContainKey("Models/.cs");
    }

    [Fact]
    public void Render_PerEntityTemplate_DetectsEntityNameLowerToken()
    {
        // Python templates use {{EntityNameLower}} in path (e.g. app/models/{{EntityNameLower}}.py).
        // The detector must treat that as a per-entity signal even when {{EntityName}} doesn't appear.
        var templates = new Dictionary<string, string>
        {
            ["app/models/{{EntityNameLower}}.py"] = "class {{EntityName}}: table = '{{TableName}}'",
        };
        var vars = TwoEntityVariables();

        var result = _sut.Render(templates, vars);

        result.Should().HaveCount(2);
        result.Should().ContainKey("app/models/user.py");
        result.Should().ContainKey("app/models/post.py");
        result["app/models/user.py"].Should().Contain("class User: table = 'users'");
    }

    [Fact]
    public void Render_PerEntityTemplate_PreservesInjectionZones()
    {
        var templates = new Dictionary<string, string>
        {
            ["repos/{{EntityName}}Repository.cs"] =
                "class {{EntityName}}Repository {\n[[LLM_INJECTION_START: GetAllImpl]]\nSELECT * FROM {{TableName}};\n[[LLM_INJECTION_END: GetAllImpl]]\n}",
        };
        var vars = TwoEntityVariables();

        var result = _sut.Render(templates, vars);

        result["repos/UserRepository.cs"].Should().Contain("[[LLM_INJECTION_START: GetAllImpl]]");
        result["repos/UserRepository.cs"].Should().Contain("[[LLM_INJECTION_END: GetAllImpl]]");
        result["repos/UserRepository.cs"].Should().Contain("SELECT * FROM users;");
        result["repos/PostRepository.cs"].Should().Contain("SELECT * FROM posts;");

        var zones = _sut.FindInjectionZones(result["repos/UserRepository.cs"]);
        zones.Should().ContainSingle().Which.Should().Be("GetAllImpl");
    }

    private static TemplateVariables TwoEntityVariables() => new()
    {
        ProjectName = "App",
        ProjectNameKebab = "app",
        ProjectNameLower = "app",
        DbConnectionString = "x",
        FrontendUrl = "x",
        Entities =
        [
            new TemplateEntity
            {
                Name = "User",
                NameLower = "user",
                TableName = "users",
                Fields = [new TemplateField { Name = "Name", NameLower = "name", Type = "string", SqlType = "TEXT" }],
            },
            new TemplateEntity
            {
                Name = "Post",
                NameLower = "post",
                TableName = "posts",
                Fields = [new TemplateField { Name = "Title", NameLower = "title", Type = "string", SqlType = "TEXT" }],
            },
        ],
    };
}
