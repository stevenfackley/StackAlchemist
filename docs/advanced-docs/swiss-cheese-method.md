# The Swiss Cheese Method

The Swiss Cheese Method is StackAlchemist's core approach to reliable code generation. It solves the fundamental problem with pure LLM code generation: LLMs are excellent at writing business logic, but unreliable at maintaining consistent structure across dozens of files.

---

## The Problem with Pure LLM Generation

When you ask an LLM to generate an entire codebase from scratch, two failure modes emerge:

**Over-hallucination** — The LLM invents import paths, class names, and namespaces that don't exist or don't match the rest of the codebase. The files look plausible individually but fail to compile as a whole.

**Structural drift** — Across a large generation, naming conventions drift. `UserRepository` in one file becomes `UsersRepo` in another. `IUserService` is referenced as `IUserRepo`. The LLM "forgets" its own conventions halfway through.

These aren't bugs that can be prompted away. They're fundamental limitations of autoregressive language models generating long outputs.

---

## The Insight

Consider a wheel of Swiss cheese. The cheese itself — the solid, structural part — is consistent and deterministic. The holes are distributed throughout, each one unique, shaped by local conditions.

Code generation works the same way:

- **The cheese (structure)** is the same on every project. File layout, class signatures, import paths, project references, middleware registration — these follow the same patterns on every .NET/Next.js codebase. There's no reason to involve an LLM here. They should be deterministic.

- **The holes (business logic)** are unique to each project. The actual SQL queries for your specific entities, the validation logic for your domain rules, the implementation of your custom API endpoints — this is where intelligence is needed.

---

## The Architecture

```
User Schema Input
        │
        ▼
┌───────────────────┐
│  Template Engine  │  ← Handlebars templates: deterministic structure
│  (Outer Layer)    │     File layout, class skeletons, import paths,
│                   │     namespace declarations, interface definitions
└────────┬──────────┘
         │ renders "cheese" with pre-punched holes
         ▼
┌───────────────────┐
│   LLM Injection   │  ← Claude 3.5 Sonnet: fills the holes
│   (Inner Layer)   │     SQL query bodies, domain validation logic,
│                   │     custom endpoint implementations
└────────┬──────────┘
         │ complete source files
         ▼
┌───────────────────┐
│  Compile Check    │  ← Actual compiler verification
│                   │     dotnet build + npm run build
└────────┬──────────┘
         │
     passes? ──── yes ──→  Package & Deliver
         │
        no
         │
         ▼
┌───────────────────┐
│  Auto-Correction  │  ← Compiler errors fed back to LLM
│  (Retry Loop)     │     Up to 3 attempts
└───────────────────┘
```

---

## The Template Layer (Deterministic)

Templates are written in [Handlebars](https://handlebarsjs.com/) and live in the `StackAlchemist.Templates` project. Each template covers a specific file type in the generated output.

### What templates handle

- **File structure** — Exactly where each file goes in the output
- **Namespace declarations** — `namespace {{ProjectName}}.Api.Controllers`
- **Class skeletons** — Class names, constructors, injected dependencies
- **Interface definitions** — `IRepository<T>`, `IService<T>` contracts
- **Import paths** — `using {{ProjectName}}.Core.Models;`
- **Method signatures** — Return types, parameter lists, route attributes
- **DI registration** — All services wired in `Program.cs` correctly
- **Migration structure** — Table name, column list, FK constraints

### What templates do NOT handle

- Method bodies (business logic)
- SQL query implementations
- Validation rules
- Custom endpoint logic

These are the "holes" — the parts intentionally left for LLM injection.

### Example: Controller Template

```handlebars
using {{ProjectName}}.Core.Interfaces;
using {{ProjectName}}.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace {{ProjectName}}.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class {{EntityName}}Controller : ControllerBase
{
    private readonly I{{EntityName}}Repository _repository;

    public {{EntityName}}Controller(I{{EntityName}}Repository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        {{!-- LLM_INJECT: GetAll implementation --}}
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        {{!-- LLM_INJECT: GetById implementation --}}
    }

    {{#each Endpoints}}
    {{!-- ENDPOINT: {{this.method}} {{this.path}} - {{this.description}} --}}
    {{!-- LLM_INJECT: {{this.methodName}} implementation --}}
    {{/each}}
}
```

The `{{!-- LLM_INJECT: ... --}}` markers are where the LLM fills in implementations.

---

## The LLM Injection Layer (Intelligent)

After template rendering, the engine identifies every injection point and constructs targeted prompts for each one. The LLM receives:

1. **The rendered context** — The surrounding class structure, so it knows the exact method signature, class name, and dependencies
2. **The entity schema** — All field names, types, relationships
3. **The endpoint description** — The business intent for custom actions
4. **Constraints** — The exact SQL dialect, ORM conventions (Dapper, not EF Core), return type expected

This targeted prompting is what keeps the LLM focused. It's not generating an entire 500-line file from scratch — it's filling in 20 lines of a specific method, with all the context it needs to get it right.

### Example: Injected Repository Method

The template skeleton:
```csharp
public async Task<IEnumerable<Project>> GetByWorkspaceAsync(Guid workspaceId)
{
    // LLM_INJECT: Return all projects for workspaceId, ordered by created_at desc
}
```

After LLM injection:
```csharp
public async Task<IEnumerable<Project>> GetByWorkspaceAsync(Guid workspaceId)
{
    const string sql = """
        SELECT id, name, description, status, workspace_id, owner_id,
               created_at, updated_at
        FROM projects
        WHERE workspace_id = @WorkspaceId
          AND deleted_at IS NULL
        ORDER BY created_at DESC
        """;

    using var connection = _connectionFactory.CreateConnection();
    return await connection.QueryAsync<Project>(sql, new { WorkspaceId = workspaceId });
}
```

The LLM wrote the SQL and the Dapper call. The template provided everything else: the method signature, the correct return type, the correct parameter name, the correct namespace for `IEnumerable<T>`.

---

## Why This Works

### Predictability at scale
Templates guarantee that every generated project has the same file structure, the same project references, the same DI registration pattern. Even if you generate 10 different schemas, the outer structure is identical — only the content varies.

### LLM confidence
By giving the LLM a narrow, well-defined context (fill in this method body, not generate this entire class), the LLM produces higher-quality output with fewer hallucinations. The model isn't guessing at import paths or class names — they're already there.

### Compiler as ground truth
The compile check isn't a "nice to have" — it's the validation gate for the entire system. No matter how confident the LLM is about what it generated, if it doesn't compile, it doesn't ship. The compiler is the arbiter.

### Targeted correction
When a build fails, the compiler output is precise: file, line, error type. The auto-correction loop can feed *exactly* the relevant error back to the LLM along with the specific file that needs fixing — not the entire codebase. This makes corrections efficient and accurate.

---

## Template Library

The template library (`StackAlchemist.Templates`) is the accumulated knowledge of what a correct .NET 10 / Next.js 15 codebase looks like. It evolves with the framework — when .NET 11 ships, the templates update and every future generation uses the new patterns automatically.

---

## Related Docs

- [The Compile Guarantee →](./compile-guarantee)
- [Architecture Overview →](./architecture-overview)
- [Advanced Mode — Entity Wizard →](../user/advanced-mode)
