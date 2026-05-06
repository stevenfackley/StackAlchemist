# Swiss Cheese: LLM Concurrency & Throughput Tuning

The V2 "Swiss Cheese" generation path dispatches one LLM call per injection
zone, in parallel, throttled by `InjectionEngine`'s semaphore. This doc
captures the math you need to size that semaphore and to predict generation
latency against Anthropic's rate limits.

## Where the call count comes from

Each generation produces three classes of LLM calls:

1. **Per-entity, per-zone** — for every entity in the schema, every per-entity
   template file (path contains `{{EntityName...}}`) contributes its zones.
2. **Schema-wide zones** — files without `{{EntityName...}}` in path each
   contribute their zone count once, regardless of entity count.
3. **No call** — files with no `[[LLM_INJECTION_START]]` markers (pure
   Handlebars scaffolds) contribute nothing.

### V2-DotNet-NextJs zone inventory

| File | Per-entity? | Zones |
|---|---|---|
| `dotnet/{{ProjectName}}.csproj` | no | 0 |
| `dotnet/Program.cs` | no | 0 (uses `{{#each Entities}}`) |
| `dotnet/Infrastructure/DbConnectionFactory.cs` | no | 0 |
| `dotnet/appsettings*.json` | no | 0 |
| `dotnet/Models/{{EntityName}}.cs` | yes | 0 |
| `dotnet/Repositories/I{{EntityName}}Repository.cs` | yes | 0 |
| `dotnet/Repositories/{{EntityName}}Repository.cs` | yes | **5** (GetAll, GetById, Create, Update, Delete) |
| `dotnet/Controllers/{{EntityName}}Endpoints.cs` | yes | 0 |
| `dotnet/Migrations/001_initial_schema.sql` | no | **1** (ForeignKeyConstraints) |

**Total per generation:** `5 × N + 1` where `N` = entity count.

### V2-Python-React zone inventory

| File | Per-entity? | Zones |
|---|---|---|
| `backend/app/main.py` | no | 0 (uses `{{#each Entities}}`) |
| `backend/app/database.py` / `config.py` / etc. | no | 0 |
| `backend/app/models/{{EntityNameLower}}.py` | yes | **1** (ColumnDefinitions) |
| `backend/app/schemas/{{EntityNameLower}}.py` | yes | **1** (BaseFields) |
| `backend/app/repositories/{{EntityNameLower}}.py` | yes | **5** (Get/Create/Update/Delete + GetById) |
| `backend/app/routers/{{EntityNameLower}}.py` | yes | 0 |
| `backend/alembic/versions/001_initial_schema.sql` | no | **1** (ForeignKeyConstraints) |
| `frontend/src/App.tsx` | no | **1** (HomePageContent) |
| `frontend/src/lib/api.ts` | no | **1** (ApiRouteHandlers) |
| `frontend/src/types/index.ts` | no | **1** (TypeDefinitions) |

**Total per generation:** `7 × N + 4` where `N` = entity count.

### Sample call counts

| Entities | DotNetNextJs calls | Python-React calls |
|---|---|---|
| 1 | 6 | 11 |
| 3 | 16 | 25 |
| 5 | 26 | 39 |
| 10 | 51 | 74 |

## Latency model

Total generation time ≈ `ceil(callCount / maxConcurrency) × p95LatencyPerCall`.

For Claude 3.5 Sonnet on a ~1k-token per-zone prompt:
- p50 latency: ~2s
- p95 latency: ~5s
- p99 latency: ~10s

Worst-case latency for a 5-entity DotNet generation at concurrency=4 with p95
calls: `ceil(26 / 4) × 5s = 7 × 5s = 35s`. p99 doubles it.

## Anthropic rate limits (Tier 1)

As of 2026, the **Tier 1 (free)** limits for Claude 3.5 Sonnet are:
- **50 RPM** (requests per minute)
- **40k input TPM** (tokens per minute)
- **8k output TPM**

A 5-entity .NET generation = 26 requests. At concurrency=4 they finish in
~30s — well under the 50 RPM window. But two concurrent generations at
concurrency=4 each = 8 in-flight, **52 calls in ~60s** — over the limit.

**Practical implication:** if you scale beyond a single concurrent generation
in production, drop `MaxConcurrency` to 2 per generation to leave headroom.

For higher tiers (purchased), limits are usage-based and rarely the bottleneck;
the latency math dominates instead.

## Recommended `MaxConcurrency` settings

| Scenario | Setting | Rationale |
|---|---|---|
| Local dev / single user | 4 (default) | Fastest single-generation latency, no contention. |
| Tier 1 production, ≤2 concurrent generations | 2 | Stays under 50 RPM with overhead. |
| Tier 2+ production | 4–8 | Latency wins; rate limits are not the bottleneck. |
| Very large schemas (≥10 entities) | 6–8 | Amortizes the slow ones; watch p99. |

Set via config:

```json
{
  "Generation": {
    "Injection": {
      "MaxConcurrency": 2,
      "MaxAttemptsPerZone": 2
    }
  }
}
```

Or env var: `Generation__Injection__MaxConcurrency=2`.

## Retry budget

Each zone has `MaxAttemptsPerZone` retries before `InjectionFailedException`
is thrown. Default = 2. Real LLM transient failures are rare (~1%), so the
practical retry rate is low. **Don't raise this above 3** — at 3 attempts ×
26 zones = 78 worst-case calls per generation, blowing through Tier 1 rate
limits even on a single generation.

## Failure modes by zone count

- **N = 0 entities:** 1 schema-wide call (.NET) or 4 (Python). Subsecond.
- **N = 1 entity:** ≤11 calls. Single-window finish at concurrency 4.
- **N = 10 entities:** ≤74 calls. Two windows at concurrency 4. Watch for
  p99 stragglers — one slow call holds up the whole batch.
- **N > 10:** consider splitting generation into entity-batched LLM calls
  (not currently implemented). Track this in capacity planning before
  customers ship 20-entity schemas.

## Observability hooks already in place

`InjectionEngine.FillZonesAsync` emits a single structured log line at the
start with `ZoneCount`, `FileCount`, `Concurrency`. Each per-zone retry logs
`Empty LLM response` or `LLM call failed` with attempt counter. The
`InjectionResult` returned to `GenerationOrchestrator` carries
`TotalInputTokens`, `TotalOutputTokens`, and `ZonesFilled` for end-to-end
accounting via `IDeliveryService.UpdateTokenUsageAsync`.

For richer observability later: emit per-zone OpenTelemetry spans inside the
`Task.WhenAll` loop. That's a 5-line change to `InjectionEngine` once anyone
needs it.
