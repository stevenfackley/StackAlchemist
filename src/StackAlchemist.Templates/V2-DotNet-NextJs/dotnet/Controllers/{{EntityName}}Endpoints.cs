using {{ProjectName}}.Models;
using {{ProjectName}}.Repositories;

namespace {{ProjectName}}.Controllers;

public static class {{EntityName}}EndpointsExtensions
{
    public static RouteGroupBuilder Map{{EntityName}}Endpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (I{{EntityName}}Repository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, I{{EntityName}}Repository repo) =>
        {
            var item = await repo.GetByIdAsync(id);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/", async (Create{{EntityName}}Request request, I{{EntityName}}Repository repo) =>
        {
            var created = await repo.CreateAsync(request);
            return Results.Created($"/api/{{EntityNameLower}}s/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, Create{{EntityName}}Request request, I{{EntityName}}Repository repo) =>
        {
            var ok = await repo.UpdateAsync(id, request);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, I{{EntityName}}Repository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }
}
