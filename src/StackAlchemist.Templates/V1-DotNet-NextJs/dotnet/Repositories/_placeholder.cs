[[LLM_INJECTION_START: Repositories]]
{{!--
  The LLM will generate one Dapper repository class per entity here.
  Expected output format per entity:

  public class {EntityName}Repository
  {
      private readonly IDbConnectionFactory _db;
      public {EntityName}Repository(IDbConnectionFactory db) => _db = db;

      public async Task<IEnumerable<{EntityName}>> GetAllAsync() { ... }
      public async Task<{EntityName}?> GetByIdAsync(Guid id) { ... }
      public async Task<{EntityName}> CreateAsync({EntityName} entity) { ... }
      public async Task<bool> UpdateAsync({EntityName} entity) { ... }
      public async Task<bool> DeleteAsync(Guid id) { ... }
  }
--}}
[[LLM_INJECTION_END: Repositories]]
