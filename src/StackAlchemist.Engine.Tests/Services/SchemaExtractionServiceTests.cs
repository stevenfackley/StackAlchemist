using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Tests for SchemaExtractionService — processes natural language into structured JSON schema.
/// Used in Simple Mode: user types "I need an e-commerce app..." → extracted entity/relationship schema.
/// </summary>
public class SchemaExtractionServiceTests
{
    private readonly SchemaExtractionService _sut = new();

    [Fact]
    public void ExtractSchema_WithValidLlmResponse_ReturnsStructuredSchema()
    {
        var mockLlmResponse = """
            {
                "entities": [
                    {
                        "name": "Product",
                        "fields": [
                            { "name": "Id",    "type": "uuid",    "isPrimaryKey": true },
                            { "name": "Name",  "type": "string"                        },
                            { "name": "Price", "type": "decimal"                       }
                        ]
                    }
                ],
                "relationships": []
            }
            """;

        var schema = _sut.ParseExtractionResponse(mockLlmResponse);

        schema.Entities.Should().HaveCount(1);
        schema.Entities[0].Name.Should().Be("Product");
        schema.Entities[0].Fields.Should().HaveCount(3);
        schema.Entities[0].Fields.Single(f => f.Name == "Id").Pk.Should().BeTrue();
    }

    [Fact]
    public void ExtractSchema_WithInvalidJson_ThrowsSchemaExtractionException()
    {
        var malformedResponse = "Here are the entities: { broken json ...";

        var act = () => _sut.ParseExtractionResponse(malformedResponse);

        act.Should().Throw<SchemaExtractionException>();
    }

    [Fact]
    public void ExtractSchema_WithJsonWrappedInMarkdown_ExtractsJsonBlock()
    {
        var wrappedResponse = """
            Here is the extracted schema:
            ```json
            {
                "entities": [{ "name": "Product", "fields": [] }],
                "relationships": []
            }
            ```
            """;

        var schema = _sut.ParseExtractionResponse(wrappedResponse);

        schema.Entities.Should().HaveCount(1);
        schema.Entities[0].Name.Should().Be("Product");
    }

    [Fact]
    public void ExtractSchema_ValidatesRelationshipReferences()
    {
        var responseWithBadRef = """
            {
                "entities": [{ "name": "Product", "fields": [] }],
                "relationships": [
                    { "from": "Product", "to": "NonExistentEntity", "type": "one-to-many" }
                ]
            }
            """;

        var act = () => _sut.ParseExtractionResponse(responseWithBadRef);

        act.Should().Throw<SchemaValidationException>()
           .WithMessage("*NonExistentEntity*");
    }

    [Fact]
    public void ExtractSchema_WithTooManyEntities_ThrowsSchemaValidationException()
    {
        var entities = string.Join(",",
            Enumerable.Range(1, 21).Select(i => $$"""
                {
                  "name": "Entity{{i}}",
                  "fields": [{ "name": "Id", "type": "uuid", "isPrimaryKey": true }]
                }
                """));

        var response = $$"""
            {
              "entities": [{{entities}}],
              "relationships": []
            }
            """;

        var act = () => _sut.ParseExtractionResponse(response);

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*maximum of 20 entities*");
    }

    [Fact]
    public void ExtractSchema_WithTooManyFieldsOnEntity_ThrowsSchemaValidationException()
    {
        var fields = string.Join(",",
            Enumerable.Range(1, 31).Select(i => $$"""
                { "name": "Field{{i}}", "type": "string", "isPrimaryKey": false }
                """));

        var response = $$"""
            {
              "entities": [
                {
                  "name": "Product",
                  "fields": [{{fields}}]
                }
              ],
              "relationships": []
            }
            """;

        var act = () => _sut.ParseExtractionResponse(response);

        act.Should().Throw<SchemaValidationException>()
            .WithMessage("*maximum of 30 fields*");
    }
}
