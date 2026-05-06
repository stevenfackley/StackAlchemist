using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class Tier1ArtifactBuilderTests
{
    private static TemplateVariables MakeVariables(params string[] entityNames)
    {
        return new TemplateVariables
        {
            ProjectName = "TestApp",
            ProjectNameKebab = "test-app",
            ProjectNameLower = "testapp",
            DbConnectionString = "Host=localhost;Port=5432;Database=testapp;Username=postgres;Password=postgres",
            FrontendUrl = "http://localhost:3000",
            Entities = entityNames.Select(n => new TemplateEntity
            {
                Name = n,
                NameLower = n.ToLowerInvariant(),
                TableName = n.ToLowerInvariant() + "s",
            }).ToList(),
        };
    }

    [Fact]
    public void Build_AlwaysProducesSchemaJsonAndApiDocsMarkdown()
    {
        var schema = new GenerationSchema { Entities = [new SchemaEntity { Name = "Product", Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }] }] };
        var result = Tier1ArtifactBuilder.Build(schema, MakeVariables("Product"));

        result.Keys.Should().BeEquivalentTo(["schema.json", "api-docs.md"]);
        result["schema.json"].Should().Contain("\"Product\"");
        result["api-docs.md"].Should().StartWith("# TestApp — API Reference");
    }

    [Fact]
    public void Build_WithEmptySchema_StillEmitsApiDocsWithFallbackText()
    {
        var result = Tier1ArtifactBuilder.Build(new GenerationSchema(), MakeVariables());

        result["api-docs.md"].Should().Contain("No entities are defined");
        result["api-docs.md"].Should().NotContain("## "); // no entity headers
    }

    [Fact]
    public void Build_WithNullSchema_TreatsAsEmptySchema()
    {
        var result = Tier1ArtifactBuilder.Build(null, MakeVariables());

        result.Should().ContainKey("schema.json").WhoseValue.Should().Contain("\"Entities\":");
        result.Should().ContainKey("api-docs.md").WhoseValue.Should().Contain("No entities are defined");
    }

    [Fact]
    public void Build_RendersFieldNotesForNullableAndDefault()
    {
        var schema = new GenerationSchema
        {
            Entities =
            [
                new SchemaEntity
                {
                    Name = "Product",
                    Fields =
                    [
                        new SchemaField { Name = "Id", Type = "uuid", Pk = true },
                        new SchemaField { Name = "Description", Type = "string", Nullable = true },
                        new SchemaField { Name = "Status", Type = "string", Default = "active" },
                    ],
                },
            ],
        };

        var docs = Tier1ArtifactBuilder.Build(schema, MakeVariables("Product"))["api-docs.md"];

        docs.Should().Contain("primary key");
        docs.Should().Contain("nullable");
        docs.Should().Contain("default: `active`");
    }

    [Fact]
    public void Build_IncludesAllFiveCrudEndpointsPerEntity()
    {
        var schema = new GenerationSchema { Entities = [new SchemaEntity { Name = "Product", Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }] }] };

        var docs = Tier1ArtifactBuilder.Build(schema, MakeVariables("Product"))["api-docs.md"];

        docs.Should().Contain("GET /api/v1/products");
        docs.Should().Contain("GET /api/v1/products/{id}");
        docs.Should().Contain("POST /api/v1/products");
        docs.Should().Contain("PUT /api/v1/products/{id}");
        docs.Should().Contain("DELETE /api/v1/products/{id}");
    }

    [Fact]
    public void Build_RendersRelationshipsSectionWhenPresent()
    {
        var schema = new GenerationSchema
        {
            Entities = [new SchemaEntity { Name = "Order", Fields = [new SchemaField { Name = "Id", Type = "uuid", Pk = true }] }],
            Relationships = [new SchemaRelationship { From = "Order", Type = "has-many", To = "OrderItem" }],
        };

        var docs = Tier1ArtifactBuilder.Build(schema, MakeVariables("Order"))["api-docs.md"];

        docs.Should().Contain("## Relationships");
        docs.Should().Contain("`Order`").And.Contain("`has-many`").And.Contain("`OrderItem`");
    }
}
