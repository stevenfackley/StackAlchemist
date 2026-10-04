# Swiss Cheese: LLM Concurrency & Throughput Tuning

The V2 "Swiss Cheese" generation path dispatches one LLM call per injection
zone, in parallel, throttled by `InjectionEngine`'s semaphore. This doc
captures the math you need to size that semaphore and to reason about
generation latency against your Anthropic account's rate limits.

V2 runs in Development only today; prod takes the V1 one-shot path (see
[`swiss-cheese-rollout.md`](./swiss-cheese-rollout.md)). The default model is
Claude Sonnet 5.5 (`claude-sonnet-5-5`, set by `ANTHROPIC_MODEL`).

## Where the call count comes from

Each generation produces three classes of LLM calls:

1. **Per-entity, per-zone** - for every entity in the schema, every per-entity
   template file (path contains `{{EntityName...}}`) contributes its zones.
2. **Schema-wide zones** - files without `{{EntityName...}}` in path each
   contribute their zone count once, regardless of entity count.
3. **No call** - files with no `[[LLM_INJECTION_START]]` markers (pure
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

**Total per generation:** `5 x N + 1` where `N` = entity count.

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

**Total per generation:** `7 x N + 4` where `N` = entity count.

### Sample call counts

| Entities | DotNetNextJs calls | Python-React calls |
|---|---|---|
| 1 | 6 | 11 |
| 3 | 16 | 25 |
| 5 | 26 | 39 |
| 10 | 51 | 74 |

## Latency model

Total generation time is about `ceil(callCount / maxConcurrency) x latencyPerCall`,
where `latencyPerCall` is the slow end (p95) of per-zone call latency.

Measure `latencyPerCall` for the current model rather than assuming it.
Claude Sonnet 5.5 runs **adaptive thinking at effort `medium`** by default
(`ANTHROPIC_EFFORT`), so each call can spend extra output tokens on thinking
before it emits the zone's code. That adds latency and output-token cost per
call, and it multiplies across the `5 x N + 1` zone calls. Expect per-zone
latency to depend on effort and on how hard the zone is; lowering effort for
small, mechanical zones is a tuning option to test, not something this doc
promises.

The estimates this doc used to carry (p50 about 2 s, p95 about 5 s, p99 about
10 s per call) were **historical Claude 3.5 Sonnet figures** and are not facts
about the current model. As an illustration of the formula only: at
concurrency 4, a 5-entity DotNet generation has 26 calls, so 7 serial windows;
at an assumed 5 s p95 per call that is about 35 s. Substitute a measured value.

## Rate limits

Anthropic rate limits (requests per minute, input and output tokens per
minute) depend on your account's usage tier and on the model, and they change.
Read the current numbers from the Anthropic console or the response headers
instead of relying on a figure written here. The older figures in this doc
(50 RPM, 40k input TPM, 8k output TPM for a free tier on Claude 3.5 Sonnet)
were historical and no longer describe the current model or tier.

What stays true regardless of the numbers:

- One generation is `5 x N + 1` (or `7 x N + 4`) requests, issued in
  windows of `MaxConcurrency`. Several generations at once multiply the
  in-flight requests, and in-flight requests times the call rate must stay
  under your requests-per-minute limit.
- Thinking tokens count as output tokens, so output-tokens-per-minute is the
  limit most likely to bind on Sonnet 5.5, ahead of requests per minute.
- If you run more than one concurrent generation in production, lower
  `MaxConcurrency` per generation to leave headroom.
- Higher purchased tiers raise limits and usually make the latency math the
  bottleneck instead.

BYOK builds use the customer's own key and limits, not the platform's.

## Recommended `MaxConcurrency` settings

| Scenario | Setting | Rationale |
|---|---|---|
| Local dev / single user | 4 (default) | Fastest single-generation latency, no contention. |
| Low account limits, 2 or more concurrent generations | 2 | Leaves headroom under requests-per-minute and output-token limits. |
| High account limits | 4-8 | Latency wins; rate limits are not the bottleneck. |
| Very large schemas (10 or more entities) | 6-8 | Amortizes the slow calls; watch the tail latency. |

These are starting points to confirm against measured latency and your own
limits.

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

Or env var: `Generation__Injection__MaxConcurrency=2`. Defaults in code:
`MaxConcurrency` = 4, `MaxAttemptsPerZone` = 2.

## Retry budget

Each zone gets up to `MaxAttemptsPerZone` attempts before
`InjectionFailedException` is thrown. Default = 2. Transient LLM failures
are rare, so the practical retry rate is low. **Don't raise this above 3**:
at 3 attempts x 26 zones = 78 worst-case calls per generation, which can
exhaust rate limits and token spend even for a single generation.

## Failure modes by zone count

- **N = 0 entities:** 1 schema-wide call (.NET) or 4 (Python). Subsecond to a few seconds.
- **N = 1 entity:** at most 11 calls. Single-window finish at concurrency 4 for DotNet.
- **N = 10 entities:** at most 74 calls. Many windows at concurrency 4. Watch
  for tail-latency stragglers; one slow call holds up the batch.
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
