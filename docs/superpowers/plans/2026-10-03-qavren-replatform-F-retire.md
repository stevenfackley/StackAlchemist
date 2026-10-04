# Qavren Re-platform — Phase F: Retire Supabase

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Respect the gates: Task 1 ships now; Tasks 2–6 may be built and reviewed early but **merge only when G2 is green**; Task 7 is owner-run.

**Goal:** StackAlchemist has one data store (qavren-db, schema `stackalchemist`) and one identity provider (Qavren Auth, realm `stackalchemist`). The Supabase mode (code, config, CI and deploy steps, docs, secrets and the two Supabase projects) is gone.

**Architecture:** Phases B and C built two strangler seams: `usesPostgresStore()` / `usesQavrenAuth()` in the web (presence of `DATABASE_URL` / `QAVREN_AUTH_URL`) and `DATABASE_URL` presence in the Engine and Worker. Phase E flipped prod to the Qavren side. Phase F collapses both seams. The Qavren branch becomes the only branch and the Supabase implementations are deleted. A missing variable stops meaning "Supabase mode". It means demo mode (non-production, web) or a no-op store (non-production, Engine), and a boot failure in production.

**Out of scope:** the *generated* product's Supabase wiring (`src/StackAlchemist.Templates/**`, and marketing copy that describes the output, such as `pricing`, `about`, `layout` keywords, `faq-manifest`, `compare-manifest` and `llms.txt`). Generated apps still ship the Supabase client. Leave all of it alone.

Parent plan: `2026-09-28-qavren-replatform.md` (phase F section). Cutover record: `docs/runbooks/qavren-cutover-phase-e.md` (§6 lists the F inputs). Tracking issue: #431.

> **Progress (2026-10-03):**
> - Task 1 merged as PR #465 (`ece9467d`) and deployed green. The prod bundle no longer opens the dashboard's Realtime channel.
> - Decision 6 is done: PR #468 deleted `deploy-test.yml`, closing #211.
> - The default model moved to Claude Sonnet 5.5 (PR #467). That doesn't affect this plan.
>
> Still gated: Tasks 2–6 (G2: on or after 2026-10-08, plus the §3.4 money path) and Task 7 (owner).
>
> **Executed 2026-10-04** (the owner brought it forward from 2026-10-08):
> - Merged in order: #483 (07d2272c), #482 (d1bd53c3), #480 (a58edc74).
> - The final deploy (run 37243546821) is green.
> - The Prod `SUPABASE_*` secrets are deleted, and #431 and #422 are closed.
> - Remaining: the owner deletes the two Supabase projects (Task 7).
> - The record is in `docs/runbooks/qavren-cutover-phase-e.md` (Execution record).
>
> **Progress (2026-10-04, before the merge):**
> - G2(2), the money path, is met (see Gates).
> - Tasks 2–6 are built, reviewed and green, but not merged:
>   - #483: web, Tasks 2–3;
>   - #482: Engine and Worker, Task 4, plus #426;
>   - #480: CI, deploy and config (Task 5) and docs (Task 6).
> - **Merge order, on or after 2026-10-08: #483 and #482 first, then #480.**
>   #480 removes the `NEXT_PUBLIC_SUPABASE_*` build args. On today's web code
>   those are what keep `/dashboard` dynamic. Without them `next build`
>   prerenders `/dashboard` as a static redirect to `/login`, which signs out
>   every user. #483 makes `/dashboard` `force-dynamic`.
> - Task 7 so far:
>   - The Test environment has no Supabase secrets left.
>   - Prod's three `SUPABASE_*` secrets stay until #480 deploys green.
>   - The credential files are still on disk. The Bitwarden move script is ready but hasn't been run.

---

## State on 2026-10-03

| | |
|---|---|
| Flip | 2026-10-01 ~02:30 UTC, deploy run 36806238503. Prod is in Qavren mode: `/api/auth/session` 200, `/api/auth/providers` lists `keycloak`. |
| Phase E §3 | 3.1 mode, 3.2 owner re-registered (Google), 3.3 Tier 0 end to end, and 3.5 sign-out all passed. **3.4 money path not proven on qavren-db**: a Checkout session was created, but the keys are live and nobody paid. 3.6 quota is covered by the Drizzle integration suite only. |
| Old prod project `ctqhwykryoglhdwatljt` | On the Pro plan, so it **cannot be paused**. Nothing has written to it since the flip. Delete on or after **2026-10-08**. |
| CI project `cdlefpvsvyepofsboepc` | Unused since phase D (#437). Can be deleted at any time. |
| Browser bundle | Still carries `NEXT_PUBLIC_SUPABASE_*`. Scan of prod chunks on 2026-10-03: `ctqhwykryoglhdwatljt.supabase.co` plus the anon key. Status pages and the dashboard open Realtime channels to that project. |
| Test mirror | Dead since 2026-06-01 (#211). `deploy-test.yml` asks for `[self-hosted, Linux, X64]`, and the fleet has no X64 Linux runner (the prod runner and `mini-linux` are both arm64), so every run queues for 24 h and is cancelled. It is also the last consumer of Supabase Auth. |
| Secrets | Prod: `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY`, kept through E and read by nothing in Qavren mode. Test: the same three plus `SUPABASE_DB_PASSWORD`, which nothing references. `CI_SUPABASE_DB_URL` was deleted 2026-09-30. `deploy-*.yml` also falls back to `NEXT_PUBLIC_SUPABASE_URL` / `NEXT_PUBLIC_SUPABASE_ANON_KEY` secret names, so check for those too. |

## Gates

- **G1, Task 1 only: none.** Task 1 fixes a bug, behaves the same in both modes, and is the F end state anyway. Ship it now.
- **G2, merging Tasks 2–6.** All three must hold:
  1. The date is **2026-10-08 or later**: seven clean days since the flip, which closes the rollback window.
  2. The **money path is verified without a paid checkout** (owner decision, 2026-10-03: no real purchase). This needs:
     - the money-path hardening PR (#421, #419, #423, #424, the 500-character metadata clip, delayed payment methods) merged; and
     - Stripe delivery confirmed by one of the checks in `docs/runbooks/stripe-webhooks.md` (the dashboard's recent deliveries showing 200s, or the owner-approved zero-charge probe).

     Rollback to Supabase mode stays possible until G2 holds.

     **Met on 2026-10-04:**
     - #471 merged.
     - The live endpoint was created; there had been none.
     - The zero-charge probe passed (run 37173569528). The event reached `stackalchemist.stripe_events`.
  3. No open rollback-class incident.
- **G3, Task 7:** the merged F deploy is green and its mode check passes in the single remaining mode.

## Decisions

1. **Code first, then projects.** Deleting `ctqhwykryoglhdwatljt` while the bundle still points at it does no harm, because the channel errors and the page falls back to polling. Merging the code first keeps a revertable path until the project is gone, and it turns the deletion into a non-event.
2. **Realtime is removed, not ported. Polling through the `getGeneration` server action becomes the only transport.**
   - Status pages poll every **3 s** while the page is visible and the status is not terminal. They pause while hidden and fetch once when the page becomes visible again.
   - The dashboard calls `router.refresh()` every **10 s** while it is visible and at least one listed row is not terminal.
   - Cost: one owner-scoped primary-key select per open status page every 3 s, which is negligible at today's volume. Revisit with SSE only if load ever proves otherwise.
   - **Why this ships ahead of the gate:** today the hook calls `stopPolling()` on `SUBSCRIBED` and waits for events. In prod the channel joins the old project, where no row ever changes, so after the single catch-up fetch a status page may not update again until reload. This is **unverified**. Checking it needs a browser watching a build that outlasts the WebSocket handshake. Tier 0 finishes fast enough that the catch-up fetch can hide the problem, and phase E only ran Tier 0. **Confirmed later on 2026-10-03:** an anonymous `postgres_changes` join on `ctqhwykryoglhdwatljt` returned `phx_reply` `ok` and "Subscribed to PostgreSQL", so the freeze was real until #465 deployed.
   - `GenerationsLiveRefresher` refreshes only when a channel event arrives, so in Qavren mode it **never** refreshes. The dashboard's live status badges are dead in prod today. That part is certain from the code.
3. **Demo mode becomes explicit-or-local.** Today `_autoDemo` keys off `NEXT_PUBLIC_SUPABASE_URL`, and it has to stay client-visible (phase C decision 3: a server-only variable breaks hydration). The new rule is `_autoDemo = !NEXT_PUBLIC_DEMO_MODE && NODE_ENV !== "production"`. For everyone who runs without `NEXT_PUBLIC_SUPABASE_URL`, which includes every Qavren-mode developer, this is identical, because phase C already requires `NEXT_PUBLIC_DEMO_MODE=false` for local Qavren-mode dev.
4. **Production boot asserts one shape.** `assertAuthModeConsistent()` becomes `assertProductionConfig()`. In production, `DATABASE_URL`, `QAVREN_AUTH_URL` (URL-shaped) and `AUTH_SECRET` are all required. Outside production, `QAVREN_AUTH_URL` without `DATABASE_URL` is still refused. The Engine's production check requires `DATABASE_URL`; the `SUPABASE_SERVICE_ROLE_KEY` alternative goes away.
5. **Engine outside production without `DATABASE_URL`** registers an explicit `NoOpDeliveryService` (logs once at startup). That keeps today's local and unit behaviour, where an unconfigured `SupabaseDeliveryService` silently did nothing. `IBillingStore` stays unregistered there, as it is today when Supabase is unconfigured, and the webhook and refund paths already take it as optional (`GetService`).
6. **Test mirror: retire it (recommended; the owner can veto).** Delete `deploy-test.yml` and `docker/docker-compose.test.yml`, delete the Test environment's Supabase secrets, and close #211. The `develop` branch exists only to trigger the mirror, so the owner decides whether to delete it. **If the owner vetoes,** reviving the mirror is its own plan and is not part of F: a `stackalchemist-test` realm with callback `https://test.stackalchemist.app/api/auth/callback/keycloak`, the qavren-db `test` schema (provisioned 2026-09-29) in the Test environment, and an arm64 runner label.
7. **`supabase/` is deleted, not archived.** Git keeps the history. Drizzle's `0000_init.sql` and `0001_functions.sql` are the schema of record, and the ERD is regenerated from `src/StackAlchemist.Web/src/db/schema.ts`.
8. **Issues closed by this phase:**
   - #431 (Supabase-mode auth gaps): the code is deleted.
   - #422 (`SupabaseBillingStore` mapping): the code is deleted.
   - #426 (the Worker host cannot resolve `IDeliveryService` because `IPendingWriteBuffer` is never registered): fixed in Task 4, since the Postgres implementation becomes the only one.
   - #211, if decision 6 stands.

   Money-path bugs that are not Supabase-specific (#419, #421, #423, #424) stay open; they are not phase F.

---

## Task 1: Polling-only status transport (G1, ship now)

**Files:**
- `src/StackAlchemist.Web/src/lib/hooks/use-generation-realtime.ts`: rename to `use-generation-status.ts` with export `useGenerationStatus`; the transport type becomes `"polling" | "off"`.
- Callers: `app/generate/[id]/GenerateClientPage.tsx`, `app/simple/SimpleModePage.tsx`, `app/advanced/AdvancedModePage.tsx` (hook import only; their `supabase` hard-nav guard is Task 3).
- `app/dashboard/GenerationsLiveRefresher.tsx` and `app/dashboard/page.tsx`: pass `hasActive`, which is true when any listed row is not terminal.
- Tests: `__tests__/lib/use-generation-realtime.test.ts` (rename and rewrite), `__tests__/dashboard/GenerationsLiveRefresher.test.tsx`.
- Delete `e2e/nightly/realtime-fallback.spec.ts`. Since phase D it duplicates the Spark flow.

- [ ] **Step 1: tests first (vitest, fake timers).**
  - On mount: one immediate fetch, then another every `pollMs` (default 3000).
  - `document.visibilityState = "hidden"` stops the timer; `"visible"` fetches once and restarts it.
  - `enabled: false` (terminal status or demo) clears the timer and does no fetch.
  - A fetch error is swallowed and the next tick recovers.
  - Unmount clears everything.
  - Refresher: no interval when `hasActive` is false; `router.refresh()` every 10 s while visible and `hasActive`; none while hidden; one on becoming visible.
- [ ] **Step 2: implement.** No `@/lib/supabase` import in either file.
- [ ] **Step 3: verify.** From `src/StackAlchemist.Web`: `npm run lint`, `npx tsc --noEmit`, `npx vitest run`, `npm run build`.
- [ ] **Step 4:** PR `fix(web): generation status polls instead of waiting on a Realtime channel nothing writes to`. The body states the unverified-freeze caveat (decision 2) and the certain dashboard defect.
- [ ] **Step 5, after the merge deploy:** an owner or Playwright check on prod. Open a paid-tier or slow generation and watch its status page advance without a reload. `/dashboard` badges should flip without a reload too.

## Task 2: Web — delete Supabase Auth (merge at G2)

**Delete:**
- `app/login/LoginPageClient.tsx`
- `app/register/RegisterPageClient.tsx`
- `app/forgot-password/ForgotPasswordClient.tsx`
- `app/auth/reset-password/ResetPasswordClient.tsx`
- `app/auth/callback/route.ts`
- `components/oauth-buttons.tsx`
- `lib/supabase-server.ts`

**Collapse to the Qavren branch:**
- `proxy.ts`: delete `supabaseSessionRefresh` and the `@supabase/ssr` import. The Qavren gate becomes unconditional, except in demo mode and on the Basic Auth path.
- `lib/session.ts`: `getSessionUser()` is the Auth.js read only.
- `app/auth/signout/route.ts`: RP-initiated logout only.
- `app/login/page.tsx`, `app/register/page.tsx`: Qavren pages only. The `force-dynamic` comment about freezing a mode no longer applies; keep `force-dynamic` only if the page still reads request state.
- `app/forgot-password/page.tsx`, `app/auth/reset-password/page.tsx`: plain redirects to `/login` (Keycloak owns reset).
- `app/login/actions.ts`, `app/register/actions.ts`: drop the `if (!usesQavrenAuth()) return;` guards.
- `auth.ts`, `app/api/auth/[...nextauth]/route.ts`: the lazy-import rule existed only to keep next-auth out of Supabase-mode processes. Static imports are now allowed, but keep the vitest `server.deps.inline` entries.

**Tests:**
- Delete the Supabase-mode cases in `__tests__/auth/*` (`callback-route`, `reset-password`, `forgot-password`, `register`, `login-a11y`, the `signout-route` Supabase case) and in `__tests__/proxy/proxy.test.ts`.
- Qavren-mode assertions stay byte-for-byte. Any changed Qavren assertion blocks review.

- [ ] **Step 1:** delete and collapse, then run `npx tsc --noEmit` until clean.
- [ ] **Step 2:** fix the tests, then run `npx vitest run`.
- [ ] **Step 3:** `npm run lint` and `npm run build`. Confirm with `grep -rn "@supabase/ssr" src` that nothing remains.

## Task 3: Web — delete the Supabase data path, client, config and dependencies (merge at G2)

- **Delete:** `lib/data/supabase-store.ts` and `lib/supabase.ts` (the browser and service-role clients).
- **`lib/data/index.ts`:** `getDataStore()` returns `new DrizzleStore()`. Drop `kind` from `DataStore` or narrow it to `"drizzle"`.
- **`lib/runtime-config.ts`:**
  - Delete `hasPublicSupabaseConfig`, `hasServerSupabaseConfig` and `isLikelyValidAnonKey`.
  - `hasDataStoreConfig()` becomes `usesPostgresStore()`.
  - Apply the `_autoDemo` rule and new warning text from decision 3.
  - `assertAuthModeConsistent` becomes `assertProductionConfig` (decision 4). Update its call in `src/instrumentation.ts`.
- **`app/simple/SimpleModePage.tsx`, `app/advanced/AdvancedModePage.tsx`:** `isDemoMode || !supabase` becomes a plain hard navigation (phase C decision 12).
- **`lib/types.ts`:**
  - Delete the supabase-js `Database` generic (lines ~134+).
  - Change the header comment to "mirror the Drizzle schema in `src/db/schema.ts`".
- **`app/privacy/page.tsx`:** keep only the Qavren copy. Auth: Qavren Solutions' Keycloak. Database: Supabase, as qavren-db's host. Cookies: the Auth.js session cookie. This is legal copy, so read the result in full.
- **`next.config.ts` CSP:** drop `https://*.supabase.co wss://*.supabase.co` from `connect-src`. Also check `images.remotePatterns` for Supabase hosts.
- **`package.json`:** remove `@supabase/ssr` and `@supabase/supabase-js`. Run `npm install` to regenerate the lockfile, then `npm run audit:ci`.
- **Comments that name Supabase mode:**
  - `db/schema.ts:29`
  - `lib/data/drizzle-store.ts:8,75`
  - `lib/build-report.ts:14-16` (the `PROD_SUPABASE_DB_URL` gate it cites is gone; the column can now ship as a Drizzle migration)
  - `app/dashboard/page.tsx:249`
- **Tests:**
  - Delete `__tests__/data/supabase-store.test.ts`.
  - Rewrite `__tests__/data/store-selection.test.ts` (one store).
  - Remove the Supabase cases from `__tests__/lib/runtime-config.test.ts`.
  - In `__tests__/lib/actions-*.test.ts` and `actions-test-helpers.ts`, change mocks of `@/lib/supabase` / `hasServerSupabaseConfig` to the Drizzle store mock. That changes the arrangement only; any changed expectation is a review blocker.
  - `__tests__/privacy-page.test.tsx` keeps only the Qavren-mode assertions.

- [ ] **Step 1:** source changes, then `npx tsc --noEmit`.
- [ ] **Step 2:** tests, then `npx vitest run`. The coverage floors in `vitest.config.ts` must still hold; deleting tested code can lower the ratio, so report it and do not lower a floor silently.
- [ ] **Step 3:** run `npm run lint` and `npm run build`. Then `grep -rniE "supabase" src/StackAlchemist.Web/src` must print only generated-product copy (`about`, `pricing`, `layout` keywords, `faq-manifest`, `compare-manifest`, `demo-data`) and the privacy page's "Supabase — database" line.

## Task 4: Engine and Worker — one store (merge at G2)

- **Delete:**
  - `Services/SupabaseDeliveryService.cs` and `Services/SupabaseBillingStore.cs`
  - `Tests/Services/SupabaseDeliveryServiceTests.cs`
- **`Program.cs`:**
  - Drop the `Supabase:Url` / `Supabase:ServiceRoleKey` env mapping (lines ~86–88) and the two `AddHttpClient(...HttpClientName)` registrations.
  - In Production, require `DATABASE_URL` (decision 4).
  - When `DATABASE_URL` is unset outside Production, register `NoOpDeliveryService` (decision 5).
  - Fix the stale comments that mention Supabase Realtime (~457) and "Persist extracted schema to Supabase" (~556).
- **`StackAlchemist.Worker/Program.cs`:** the same selection. Register `IPendingWriteBuffer` next to `PostgresDeliveryService`, as the Engine does (fixes #426). Add a host-build test that resolves `IDeliveryService` from the Worker's container with `DATABASE_URL` set.
- **`IDeliveryService.cs`, `IBillingStore.cs`, `CompileWorkerService.cs`, `PendingWriteBuffer.cs`, `InFlightGenerationRegistry.cs`:** reword the "Supabase" doc comments to describe the store.
- **Tests that construct the Supabase implementations** (`Webhooks/StripeWebhookTests`, `Services/StripeRefundServiceTests`, `Integration/EngineDataSourceTests`): move them to a fake `IBillingStore` or to the existing Postgres integration fixtures. Keep the replayed-event test, which asserts one transaction rather than two, on the Postgres path. Six more test files only mention Supabase in names or comments (`grep -rli supabase src/StackAlchemist.Engine.Tests`); reword them.

- [ ] **Step 1:** source changes, then `dotnet build StackAlchemist.slnx -warnaserror`.
- [ ] **Step 2:** run `dotnet test StackAlchemist.slnx`. The Postgres integration suites need Docker, as CI's backend job provides.
- [ ] **Step 3:** `dotnet list package --vulnerable --include-transitive` stays clean.

## Task 5: CI, deploy, compose and container config (merge at G2)

**`.github/workflows/deploy-prod.yml`:**
- Delete the `Apply Supabase migrations (prod)` step: the `PROD_SUPABASE_DB_URL` gate, the drift guard and the CLI install.
- The preflight accepts **one** shape and errors on anything missing: `DATABASE_URL`, `DATABASE_URL_MIGRATE`, `QAVREN_AUTH_URL` and `AUTH_SECRET`, all four, with the existing URL-shape checks.
- Remove the Supabase inputs from the `setup-env` call.
- The final mode check expects Qavren only.
- The qavren-db migrate step loses its "skip with warning when unset" branch, because an unset value is now a preflight error.

**`.github/actions/setup-env/action.yml`:** delete the three `supabase_*` inputs, their `env:` lines and the `.env` heredoc lines. Reword "Empty = Supabase mode" in the remaining descriptions.

**Other files:**
- `docker-compose.prod.yml`: drop `NEXT_PUBLIC_SUPABASE_*` and `SUPABASE_SERVICE_ROLE_KEY` from the build args and from the `sa-web` and `sa-engine` environment, and reword the phase B/C comments.
- `Dockerfile`: drop `ARG`/`ENV NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_ANON_KEY`.
- `docker/nginx.prod.conf`: the comments about chunked `sb-*-auth-token` cookies (~35) and "inert until phase E" (~72) go. Keep `proxy_buffer_size`/`proxy_buffers`/`large_client_header_buffers 16k` (~43–46): Auth.js JWT session cookies are large too.
- `.github/workflows/ci.yml`: delete the blank `NEXT_PUBLIC_SUPABASE_*` / `SUPABASE_SERVICE_ROLE_KEY` env lines (~502–504, ~527–529) and the comment at ~125.
- `.github/workflows/tracker-guard.yml`: the comment names "the Supabase PKCE auth cookie". It now names the Auth.js session cookie, which is strictly necessary in the same way. The PR's own run of the reusable guard proves it still passes.
- `.env.example`:
  - Drop the Supabase block (~34–36) and the SMTP-script block (~90–110).
  - Add `NEXT_PUBLIC_DEMO_MODE=true` with a one-line note for the no-backend recipe, and document `NEXT_PUBLIC_DEMO_MODE=false` with `DATABASE_URL`/`QAVREN_AUTH_URL` for the real one.
- `scripts/supabase-auth-smtp.mjs` and the root `package.json` `supabase:smtp` script: delete. `scripts/setup-env.mjs:135`: reword the local-defaults text.
- `.gitignore`: drop `supabase/.temp/` and `supabase/functions/.env`.
- **Decision 6, the mirror:** delete `.github/workflows/deploy-test.yml` and `docker/docker-compose.test.yml`. Remove the `test` environment's use from any other workflow first. As of 2026-10-03 nothing else in `.github/` names it.

- [ ] **Step 1:** edits, then `actionlint` (with the two label `-ignore` flags used in phases B–D) and a PyYAML parse of every touched workflow.
- [ ] **Step 2:** `docker compose -f docker-compose.prod.yml config -q` with a dummy `.env`, plus `docker build --target sa-web .` and `--target sa-engine .`.
- [ ] **Step 3:** the PR's CI is green, including `E2E Integration` (Postgres + Keycloak lane, unaffected).

## Task 6: Docs (merge with Tasks 2–5 or right after)

**Delete:**
- `supabase/` (migrations; decision 7)
- `docs/runbooks/ci-supabase-migrations.md`
- `docs/runbooks/supabase-auth-smtp.md`

**Update to the post-F truth** (one store, one IdP, polling, no RLS, isolation by owner scoping proven by the two-user suite, `claude-sonnet-4-6` through `ANTHROPIC_MODEL`, Next 16, no Proxmox):
- `CLAUDE.md`: "Database/Auth: Supabase" becomes "Database: qavren-db (Postgres schema `stackalchemist`, Drizzle migrations); Auth: Qavren Auth (Keycloak realm `stackalchemist`, Auth.js v5)".
- The local, gitignored agent files `AGENTS.md` and `docs/DEV_PROMPT.md`: drop "Supabase is being retired". Both were brought up to date on 2026-10-03.
- `docs/architecture/`:
  - `Software Design Document.md`
  - `Database ERD.md`: regenerate from `src/db/schema.ts` and `drizzle/0001_functions.sql`; the RLS table becomes the owner-scoping rule plus the isolation suite.
  - `Data Flow Document.md`, `Data Flow Diagram.md`, `Sequence Diagram.md`
  - `Generation State Machine.md` (the `CallClaude35Sonnet` node)
  - `Dev Environment Setup.md` (§3 and §6)
  - `Testing Strategy.md` (§5.2 WebSocket row, §7.2 RLS)
  - `CSP Rollout Plan.md` (allowlist)
- `docs/advanced-docs/architecture-overview.md` ("Real-Time Progress Reporting", "Why Supabase?") and `self-hosting.md` (Supabase setup and required env).
- `docs/product/`: the PRD, BRD and PDD lines that name Supabase or Realtime as *the platform's own* stack. Lines about the generated output stay.
- `docs/user/user-guide.md` and `faq.md`: "real-time" stays true (polling is user-invisible). The FAQ's "Claude 3.5 Sonnet" answer is wrong today; fix it here.
- Runbooks:
  - `docs/runbooks/qavren-auth.md`: "Known gaps and phase F" becomes done.
  - `qavren-db-migrations.md`: drop the Supabase-mode wording.
  - `qavren-cutover-phase-e.md`: append "phase F executed" with the PR, SHA and date.
- `docs/DECISIONS.md`: a dated ADR entry. Supabase is retired, polling replaces Realtime (with the cadence numbers), demo mode is explicit-or-local, and production boot asserts one shape.
- Parent plan `2026-09-28-qavren-replatform.md`: phase F results line.

- [ ] Afterwards, `grep -rniE "supabase" docs CLAUDE.md AGENTS.md README.md` may still match only (a) generated-product copy, (b) historical plans and records under `docs/superpowers/plans/` and `CHANGELOG.md`, and (c) "Supabase as qavren-db's host".

## Task 7: Owner operations (G3; the assistant cannot run these)

The `gh secret delete` and `gh workflow run` commands are blocked for the assistant by the guard-writes hook, and Supabase project deletion has no CLI path here.

- [ ] **Secrets, Prod:** `for s in SUPABASE_URL SUPABASE_ANON_KEY SUPABASE_SERVICE_ROLE_KEY NEXT_PUBLIC_SUPABASE_URL NEXT_PUBLIC_SUPABASE_ANON_KEY; do gh secret delete "$s" --repo stevenfackley/StackAlchemist --env Prod; done` (absent names just error).
- [ ] **Secrets, Test:** the same loop plus `SUPABASE_DB_PASSWORD`, with `--env Test`. If decision 6 stands, delete the whole `test` environment once nothing references it.
- [ ] **Verify:** `gh secret list --repo stevenfackley/StackAlchemist --env Prod` lists no `SUPABASE*` names. Then dispatch one `deploy-prod.yml` run and confirm it is green: the preflight passes on the one shape and the mode check passes.
- [ ] **Supabase dashboard:** delete `cdlefpvsvyepofsboepc` (CI). Delete `ctqhwykryoglhdwatljt` (prod) **on or after 2026-10-08**. Optional: export a final `pg_dump` of it into the password-manager vault first.
- [ ] **Bundle check:** scan prod's JS chunks as in runbook §4, which must print **no** `supabase.co` host.
- [ ] **Credential files:** if they still exist, delete `provision-prod.txt` and `provision-test.txt` from `%USERPROFILE%\stackalchemist-credentials-vault-20260929\` (runbook §6). They hold plaintext DB passwords.
- [ ] **qavren-db:** add the cut-over and retired dates to the `notes:` of `apps/stackalchemist.yaml`, on a branch with a PR.
- [ ] **Bookkeeping:**
  - Vault `Projects/stack-alchemist/stack-alchemist.md`: a phase F section.
  - Workspace memory: retire `project_ci_supabase.md` and `project_prod_migrations.md` (both describe Supabase-era state).
  - Close #431, #422, #426 and #211 with the PR reference.

## Exit criteria

- [ ] Task 1 is on `main`. Prod status pages and `/dashboard` update without a reload, checked on a build that outlasts the WebSocket handshake.
- [ ] No production code path, workflow or container references Supabase except generated-product copy and templates. `@supabase/*` is gone from `src/StackAlchemist.Web/package.json`.
- [ ] Production refuses to boot without `DATABASE_URL`, `QAVREN_AUTH_URL` and `AUTH_SECRET`. The web outside production is in demo mode unless `NEXT_PUBLIC_DEMO_MODE=false`. The Engine outside production runs on the no-op store without `DATABASE_URL`.
- [ ] `ctqhwykryoglhdwatljt` and `cdlefpvsvyepofsboepc` are deleted, and neither GitHub environment has a `SUPABASE*` secret.
- [ ] The docs listed in Task 6 describe the post-F system, and `CLAUDE.md` no longer says "Database/Auth: Supabase".
