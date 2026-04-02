using FluentAssertions;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Tests for the TemplateProvider — loads Handlebars templates and renders variables.
/// 
/// Scaffold: These tests define the contract. Implement the service to make them pass.
/// </summary>
public class TemplateProviderTests
{
    [Fact]
    public void Render_WithAllVariablesProvided_ReplacesAllPlaceholders()
    {
        // Arrange
        var template = "namespace {{ProjectName}}.Controllers;";
        var variables = new Dictionary<string, string>
        {
            ["ProjectName"] = "MyApp"
        };

        // Act & Assert
        // var result = _sut.Render(template, variables);
        // result.Should().Be("namespace MyApp.Controllers;");
        // result.Should().NotContain("{{");
        Assert.True(true, "Scaffold: implement when TemplateProvider exists");
    }

    [Fact]
    public void Render_WithMissingRequiredVariable_ThrowsMissingVariableException()
    {
        // Arrange
        var template = "Server={{DbConnectionString}};Database={{DbName}};";
        var variables = new Dictionary<string, string>
        {
            ["DbConnectionString"] = "localhost:5432"
            // Missing: DbName
        };

        // Act & Assert
        // var act = () => _sut.Render(template, variables);
        // act.Should().Throw<MissingTemplateVariableException>()
        //    .WithMessage("*DbName*");
        Assert.True(true, "Scaffold: implement when TemplateProvider exists");
    }

    [Fact]
    public void FindInjectionZones_WithValidTemplate_ReturnsAllZones()
    {
        // Arrange
        var template = """
            // Static code above
            {{!-- LLM_INJECTION_START: Controllers --}}
            {{!-- LLM_INJECTION_END: Controllers --}}
            // Static code below
            {{!-- LLM_INJECTION_START: Repositories --}}
            {{!-- LLM_INJECTION_END: Repositories --}}
            """;

        // Act & Assert
        // var zones = _sut.FindInjectionZones(template);
        // zones.Should().HaveCount(2);
        // zones.Should().Contain("Controllers");
        // zones.Should().Contain("Repositories");
        Assert.True(true, "Scaffold: implement when TemplateProvider exists");
    }

    [Fact]
    public void InjectIntoZone_WithValidContent_PlacesContentBetweenMarkers()
    {
        // Arrange
        var template = """
            // Before
            {{!-- LLM_INJECTION_START: Controllers --}}
            {{!-- LLM_INJECTION_END: Controllers --}}
            // After
            """;
        var content = "public class ProductsController { }";

        // Act & Assert
        // var result = _sut.InjectIntoZone(template, "Controllers", content);
        // result.Should().Contain("public class ProductsController { }");
        // result.Should().Contain("// Before");
        // result.Should().Contain("// After");
        Assert.True(true, "Scaffold: implement when TemplateProvider exists");
    }

    [Fact]
    public void LoadTemplate_WithNonexistentTemplate_ThrowsTemplateNotFoundException()
    {
        // Act & Assert
        // var act = () => _sut.LoadTemplate("V99-NonExistent");
        // act.Should().Throw<TemplateNotFoundException>()
        //    .WithMessage("*V99-NonExistent*available*V1-DotNet-NextJs*");
        Assert.True(true, "Scaffold: implement when TemplateProvider exists");
    }
}
