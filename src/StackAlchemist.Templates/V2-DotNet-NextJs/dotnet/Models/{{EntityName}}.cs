namespace {{ProjectName}}.Models;

public record {{EntityName}}
{
    {{#each Fields}}
    public {{Type}} {{Name}} { get; init; } = default!;
    {{/each}}
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record Create{{EntityName}}Request
{
    {{#each Fields}}
    {{#unless IsPrimaryKey}}
    public {{Type}} {{Name}} { get; init; } = default!;
    {{/unless}}
    {{/each}}
}
