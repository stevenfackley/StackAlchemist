using Dapper;
using {{ProjectName}}.Infrastructure;
using {{ProjectName}}.Models;

namespace {{ProjectName}}.Repositories;

public class {{EntityName}}Repository(IDbConnectionFactory db) : I{{EntityName}}Repository
{
    public async Task<IEnumerable<{{EntityName}}>> GetAllAsync()
    {
        using var conn = db.CreateConnection();
        [[LLM_INJECTION_START: GetAllImpl]]
        return await conn.QueryAsync<{{EntityName}}>(
            "SELECT * FROM {{TableName}} ORDER BY created_at DESC");
        [[LLM_INJECTION_END: GetAllImpl]]
    }

    public async Task<{{EntityName}}?> GetByIdAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        [[LLM_INJECTION_START: GetByIdImpl]]
        return await conn.QueryFirstOrDefaultAsync<{{EntityName}}>(
            "SELECT * FROM {{TableName}} WHERE id = @Id",
            new { Id = id });
        [[LLM_INJECTION_END: GetByIdImpl]]
    }

    public async Task<{{EntityName}}> CreateAsync(Create{{EntityName}}Request request)
    {
        using var conn = db.CreateConnection();
        [[LLM_INJECTION_START: CreateImpl]]
        // LLM fills: INSERT INTO {{TableName}} with all non-pk Create{{EntityName}}Request fields,
        // returning the populated entity. Use parameterized SQL — no string interpolation.
        throw new NotImplementedException("Zone CreateImpl not yet generated.");
        [[LLM_INJECTION_END: CreateImpl]]
    }

    public async Task<bool> UpdateAsync(Guid id, Create{{EntityName}}Request request)
    {
        using var conn = db.CreateConnection();
        [[LLM_INJECTION_START: UpdateImpl]]
        // LLM fills: UPDATE {{TableName}} SET <fields> WHERE id = @Id; return affected > 0.
        throw new NotImplementedException("Zone UpdateImpl not yet generated.");
        [[LLM_INJECTION_END: UpdateImpl]]
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        [[LLM_INJECTION_START: DeleteImpl]]
        var rows = await conn.ExecuteAsync(
            "DELETE FROM {{TableName}} WHERE id = @Id",
            new { Id = id });
        return rows > 0;
        [[LLM_INJECTION_END: DeleteImpl]]
    }
}
