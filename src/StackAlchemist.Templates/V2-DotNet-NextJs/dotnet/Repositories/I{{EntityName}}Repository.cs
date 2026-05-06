using {{ProjectName}}.Models;

namespace {{ProjectName}}.Repositories;

public interface I{{EntityName}}Repository
{
    Task<IEnumerable<{{EntityName}}>> GetAllAsync();
    Task<{{EntityName}}?> GetByIdAsync(Guid id);
    Task<{{EntityName}}> CreateAsync(Create{{EntityName}}Request request);
    Task<bool> UpdateAsync(Guid id, Create{{EntityName}}Request request);
    Task<bool> DeleteAsync(Guid id);
}
