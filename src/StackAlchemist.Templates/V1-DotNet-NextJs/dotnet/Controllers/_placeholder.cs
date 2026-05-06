[[LLM_INJECTION_START: Controllers]]
{{!--
  The LLM will generate one minimal API endpoint group per entity here.
  Expected output format per entity:

  var {entityLower}Group = app.MapGroup("/api/v1/{entityLower}s").WithTags("{EntityName}");

  {entityLower}Group.MapGet("/", async (IDbConnectionFactory db) => {
      using var conn = db.CreateConnection();
      var items = await conn.QueryAsync<{EntityName}>("SELECT * FROM {table_name}");
      return Results.Ok(items);
  });

  {entityLower}Group.MapGet("/{id:guid}", async (Guid id, IDbConnectionFactory db) => {
      using var conn = db.CreateConnection();
      var item = await conn.QuerySingleOrDefaultAsync<{EntityName}>(
          "SELECT * FROM {table_name} WHERE id = @Id", new { Id = id });
      return item is null ? Results.NotFound() : Results.Ok(item);
  });

  ... (POST, PUT, DELETE)
--}}
[[LLM_INJECTION_END: Controllers]]
