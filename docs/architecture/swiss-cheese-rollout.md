# Swiss Cheese Rollout Plan

The V2 "Swiss Cheese" generation path (per-zone parallel LLM dispatch) is
gated behind `Generation:UseSwissCheese`. This doc captures the staged
rollout from dev to prod and how to roll back.

## Current state (2026-10-03)

| Env | Setting | Source | Effective value |
|---|---|---|---|
| Dev (local + Docker dev) | `Generation:UseSwissCheese` | `src/StackAlchemist.Engine/appsettings.Development.json` | **`true`** |
| Prod (`https://stackalchemist.app`) | not set | the code default in `GenerationOrchestrator` is `false` | **`false`** |

There is no Test row: the test environment (`test.stackalchemist.app`,
`deploy-test.yml`) was retired on 2026-10-03 (#211, PR #468), and no staging
environment replaced it. Dev is the only place V2 runs; prod takes the V1
one-shot path.

Local dev exercises the V2 templates (V2-DotNet-NextJs, V2-Python-React)
end-to-end with the InjectionEngine. Each V2 template set has a CI compile
gate in `StackAlchemist.Engine.Tests`.

## Rollout sequence

### Phase 1 - Dev (done)

`Generation:UseSwissCheese=true` is set in `appsettings.Development.json`.
No env-var override is needed locally; everything runs against
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
   If the Engine requires the `X-Engine-Key` header in your local config,
   add it to the request.
3. Inspect `%TEMP%/stackalchemist/local-smoke/` - it should contain
   per-entity files (`ProductRepository.cs`, `ProductEndpoints.cs`,
   `nextjs/src/app/products/page.tsx`) and no `[[LLM_INJECTION_*]]`
   markers in any output file.
4. With `ANTHROPIC_API_KEY` set: same test against the real model. Expect
   `5 x N + 1` LLM calls for DotNetNextJs (5 repository zones per entity plus
   one schema-wide migration FK zone; see the zone inventory in
   [`swiss-cheese-tuning.md`](./swiss-cheese-tuning.md)).

### Phase 2 - Prod (next step)

There is no intermediate environment, so the next step after dev is prod,
behind an environment variable. Prod deploys on every push to `main`
(`deploy-prod.yml`), so treat the flip as a production change with its own
PR and watch the first generations.

Mechanism: ASP.NET Core's default env-var binding maps the double-underscore
form `Generation__UseSwissCheese` to `Generation:UseSwissCheese`. Pass it
through to the `sa-engine` container:

1. In `docker-compose.prod.yml`, add to the `sa-engine` `environment` block
   (not present today):
   ```yaml
   Generation__UseSwissCheese: ${GENERATION_USE_SWISS_CHEESE:-false}
   ```
2. In `deploy-prod.yml`, set `GENERATION_USE_SWISS_CHEESE: "true"` in the env
   that feeds the compose step (or via a repo variable, as is done for
   `ANTHROPIC_MODEL`).

The compose default stays `false`, so the file change alone changes nothing;
rollback is setting the variable back (or removing the line).

**Before flipping:**
- Price the change. V2 makes at least 6x the LLM calls of V1 per generation,
  and Claude Sonnet 5.5 runs adaptive thinking at effort `medium`, which adds
  output tokens to every call. Update spend alerts first.
- Decide the concurrency setting (`Generation__Injection__MaxConcurrency`,
  default 4) against your Anthropic account limits; see the tuning doc.

**After flipping, watch for:**
- Per-zone failure rate: `InjectionFailedException` in the Engine logs. If
  more than 2% of generations fail at the injection step (V1 baseline is
  roughly 0%), roll back and investigate.
- Confirm the V2 path ran: the log line `Generation {Id} Swiss Cheese: filled
  {Zones} zones` (EventId 201). Inspect at least one delivered R2 bundle for
  clean output (no markers, all per-entity files present).
- Latency: V2 is slower wall-clock when zones run sequentially and faster
  when parallel headroom (`MaxConcurrency`, default 4) is available. Compare
  p95 generation time before and after.
- Paid-tier refunds: a V2 failure on a paid tier is a Compile Guarantee
  refund, so a regression costs money directly.

## Rollback

Each phase rolls back by reverting the single config change:

| Phase | Rollback |
|---|---|
| Dev | remove the `Generation` block's `UseSwissCheese` from `appsettings.Development.json` |
| Prod | set `GENERATION_USE_SWISS_CHEESE` back to `false` (or remove `Generation__UseSwissCheese` from `docker-compose.prod.yml`) and let the push to `main` deploy |

No data migration, no template changes, no version bumps. The flag is a pure
runtime branch in `GenerationOrchestrator` (read via
`configuration.GetValue("Generation:UseSwissCheese", false)`).

## After rollout completes

Once prod has been on V2 for a stable period (30 days or more, no incidents):

1. Delete the V1 one-shot path in `GenerationOrchestrator`.
2. Remove `IReconstructionService` usage for first-pass generation and the V1
   `BuildGenerationPrompt`. Check first: the compile worker's repair loop
   also calls `ReconstructionService` (`Parse`, `ResolveRepairWrites`), so the
   service itself stays unless the repair path changes too.
3. Delete V1 template directories (`V1-DotNet-NextJs`, `V1-Python-React`).
4. Drop the `UseSwissCheese` flag itself.

Track this as one cleanup PR; don't bundle with feature work.

## Related

- [`swiss-cheese-tuning.md`](./swiss-cheese-tuning.md) - concurrency settings,
  call-count math, rate-limit considerations.
- [`swiss-cheese-method.md`](../advanced-docs/swiss-cheese-method.md) -
  customer-facing explanation of the architecture.
