# Swiss Cheese Rollout Plan

The V2 "Swiss Cheese" generation path (per-zone parallel LLM dispatch) is
gated behind `Generation:UseSwissCheese`. This doc captures the staged
rollout from dev → test → prod and how to roll back.

## Current state (2026-05-06)

| Env | Setting | Source | Effective value |
|---|---|---|---|
| Dev (local + Docker dev) | `Generation:UseSwissCheese` | `src/StackAlchemist.Engine/appsettings.Development.json` | **`true`** (post-PR #82) |
| Test (`https://test.stackalchemist.app`) | not set | — | **`false`** (default) |
| Prod (`https://stackalchemist.app`) | not set | — | **`false`** (default) |

Local dev exercises V2 templates (V2-DotNet-NextJs, V2-Python-React)
end-to-end with the InjectionEngine; test and prod still take the V1
one-shot path.

## Rollout sequence

### Phase 1 — Dev (this PR)

Flip `Generation:UseSwissCheese=true` in `appsettings.Development.json`.
No env-var override needed locally; everything runs against
`MockLlmClient` unless `ANTHROPIC_API_KEY` is set in the local `.env`.

**Verification:**
1. `dotnet run --project src/StackAlchemist.Engine` against the dev config.
2. Submit a generation request via the local Next.js frontend or curl:
   ```bash
   curl -X POST http://localhost:5000/api/generate \
     -H 'Content-Type: application/json' \
     -d '{"generationId":"local-smoke","mode":"advanced","tier":2,
          "projectType":"DotNetNextJs",
          "schema":{"entities":[{"name":"Product","fields":[
            {"name":"Id","type":"uuid","pk":true},
            {"name":"Name","type":"string"}]}]}}'
   ```
3. Inspect `%TEMP%/stackalchemist/local-smoke/` — should contain
   per-entity files (`ProductRepository.cs`, `ProductEndpoints.cs`,
   `nextjs/src/app/products/page.tsx`) and no `[[LLM_INJECTION_*]]`
   markers in any output file.
4. With `ANTHROPIC_API_KEY` set: same test against real Claude. Expect
   `~6 LLM calls` per entity for DotNetNextJs (5 repository zones + 1
   migration FK zone) plus `~1` schema-wide call (TypeRefinements).

### Phase 2 — Test (separate PR after Phase 1 smoke passes)

Set `Generation__UseSwissCheese=true` in the test deployment. Two options:

**Option A — env var on the runner** (preferred, lowest blast radius):
add to `.github/workflows/deploy-test.yml` env block at the job level:
```yaml
env:
  COMPOSE_PROJECT_NAME: stackalchemist-test
  Generation__UseSwissCheese: "true"
```
ASP.NET Core's default env-var binding picks up the double-underscore
form and maps it to `Generation:UseSwissCheese`.

**Option B — appsettings.Staging.json**: add a new file
`src/StackAlchemist.Engine/appsettings.Staging.json` with the flag set.
The test deploy uses `ASPNETCORE_ENVIRONMENT=Staging` so this layers on
top of `appsettings.json`.

Option A wins because the rollback is removing one line; option B
requires a code change to roll back.

**Verification:** monitor `test.stackalchemist.app` for one full
generation cycle. Confirm via Engine logs that the Swiss Cheese path
ran (`Generation {Id} Swiss Cheese: filled {Zones} zones` log line
with EventId 201). Inspect at least one delivered project's R2 bundle
to confirm clean output (no markers, all per-entity files present).

### Phase 3 — Prod (after ≥1 week of test running clean)

Same mechanism as test (env var on prod deploy workflow). Watch for:

- LLM cost increase: V2 makes ≥6× the API calls of V1 per generation.
  Expect Anthropic spend to roughly multiply by that factor on the
  per-zone-prompt path. Update budget alerts before flipping.
- Per-zone failure rate: track `InjectionFailedException` in logs. If
  >2% of generations fail at the injection step (vs the current ≈0% V1
  baseline), pause and dig.
- Latency: V2 generations are slower wall-clock when zones run sequentially
  but faster when parallel headroom (`MaxConcurrency=4`) is available.
  Track p95 generation time before/after.

## Rollback

Each phase rolls back by reverting the single config change:

| Phase | Rollback |
|---|---|
| Dev | revert `appsettings.Development.json` to remove `Generation` block |
| Test | remove `Generation__UseSwissCheese` from deploy-test.yml env |
| Prod | remove `Generation__UseSwissCheese` from deploy-prod.yml env |

No data migration, no template changes, no version bumps. The flag is
a pure runtime branch in `GenerationOrchestrator.EnqueueAsync`.

## After rollout completes

Once prod has been on V2 for a stable period (≥30 days, no incidents):

1. Delete the V1 one-shot path in `GenerationOrchestrator.EnqueueAsync`.
2. Remove `IReconstructionService` and the V1 `BuildGenerationPrompt`.
3. Delete V1 template directories (`V1-DotNet-NextJs`, `V1-Python-React`).
4. Drop the `UseSwissCheese` flag itself.

Track this as one cleanup PR; don't bundle with feature work.

## Related

- [`swiss-cheese-tuning.md`](./swiss-cheese-tuning.md) — concurrency settings,
  call-count math, Anthropic rate-limit collision analysis.
- [`swiss-cheese-method.md`](../advanced-docs/swiss-cheese-method.md) —
  customer-facing explanation of the architecture.
