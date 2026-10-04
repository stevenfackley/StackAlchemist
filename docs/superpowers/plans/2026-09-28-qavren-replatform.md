# Re-platform onto qavren-db + Qavren Auth

> **For agentic workers:** this is the phase-level plan (decisions, order, risk). Each phase gets its own bite-sized execution plan in this folder when it starts (`2026-09-28-qavren-replatform-<phase>.md`), written against the code as it stands then. Use superpowers:subagent-driven-development or superpowers:executing-plans on those, not on this file.

**Goal:** Move StackAlchemist off its two dedicated Supabase projects and onto the shared Qavren platform: data in the `stackalchemist` schema of qavren-db, identity in a `stackalchemist` Keycloak realm.

**Architecture:** qavren-db is pure Postgres (one schema + one owning login role per app; no Supabase Auth, no PostgREST, no Realtime, no RLS). So every Supabase feature StackAlchemist uses gets replaced by its plain equivalent: Auth.js v5 via `@qavren/auth-next` for identity, direct SQL for data (Drizzle in the web app, Npgsql in the Engine), polling for live status, and owner-scoping in code for isolation. The pattern is recharacter's Plan 09, which made the same move in August; copy its shapes wherever they fit.

**Tech stack:** Next.js 16 + Auth.js v5 (`next-auth` beta) + `@qavren/auth-next` + Drizzle over `postgres` (postgres-js); .NET 10 Engine + Npgsql; Keycloak 26 (qavren-auth); Supabase Pro as the host behind qavren-db (Supavisor pooler).

---

## Why

- The CI project `cdlefpvsvyepofsboepc` sits on the free org and auto-pauses. It paused again on 2026-09-23 and every main run of `E2E Integration` failed until it was resumed by hand on 2026-09-26. A daily connection from the nightly run does not keep it awake.
- Prod `ctqhwykryoglhdwatljt` is a second standalone project to pay for, back up and secure. qavren-db already provides nightly per-schema backups to R2 and hard role isolation.
- qavren-db's own design doc and playbook list StackAlchemist (row 14, "deep-4") as refactor-first: "web Realtime → existing polling fallback; auth swap. Engine side is trivial."

## Facts measured on 2026-09-28 (prod, read-only)

| | |
|---|---|
| `auth.users` | **1** (the owner; has a password and one social identity) |
| `generations` | 18 |
| `transactions` | **0**: Stripe has never completed a checkout in prod |
| DB size | 13 MB |
| Realtime publication | `generations` only |
| Storage | none. Generated ZIPs already go to R2 (`CloudflareR2UploadService`) and are unaffected |
| Account deletion / export | none exist, so no Keycloak admin service account is needed |

Zero transactions and one user make the auth crosswalk (Supabase UUID → Keycloak `sub`) moot, as it was for recharacter.

## Decisions (made)

1. **Sign-in = email/password + Google, no magic link** (owner, 2026-09-28). Keycloak does password, self-registration, email verification, reset and Google brokering natively. Magic link would need a third-party extension on the Keycloak every realm shares.
2. **Keycloak hosts the sign-in UI.** StackAlchemist's own `/login`, `/register`, `/forgot-password` and `/auth/reset-password` pages and `/auth/callback` are deleted. `/login` becomes a one-button page that calls `signIn('keycloak')`, as in recharacter. The realm gets a StackAlchemist login theme over `qavren-base`.
3. **Cutover = fresh provision, no data copy** (same call as recharacter §F). The one account re-registers; the 18 generations are the owner's test runs. Keep the old project read-only for 7 days per the qavren-db playbook §6 before deleting it. **Owner can veto:** if any of the 18 generations must survive, the playbook §2–§4 copy is about an hour more work, and `generations.user_id` must be remapped to the new Keycloak `sub`.
4. **Realtime → polling.** `use-generation-realtime.ts` already falls back to polling when there is no Supabase client, and `GenerationsLiveRefresher` becomes a timed `router.refresh()`. No new transport.
5. **Migrations move from `supabase/migrations` to Drizzle in the web app** (`drizzle-kit generate` only, applied by a `db:migrate` script as the app role), as recharacter does. Never `drizzle-kit migrate`/`push` against qavren-db: both issue `CREATE SCHEMA IF NOT EXISTS`, which the app role is refused with 42501.
6. **The plpgsql functions stay in the database**, moved to the `stackalchemist` schema, minus `SECURITY DEFINER` (the app role owns everything, so it buys nothing): `append_build_log`, `increment_token_usage`, `process_checkout_completed`, `enforce_free_generation_quota` + its trigger, `set_updated_at` + its trigger. `handle_new_user` (a trigger on `auth.users`) and `rls_auto_enable` are dropped. Profile creation becomes an idempotent upsert on first authenticated request.
7. **Auth hostname `auth.stackalchemist.app`**, via `infra/hostnames.auto.tfvars` in qavren-auth like every other realm.

## Current coupling (sweep of `origin/main`, 2026-09-28)

**Auth (Supabase GoTrue via `@supabase/ssr`):**
- `src/StackAlchemist.Web/src/middleware.ts` (session refresh)
- `src/lib/supabase.ts`, `src/lib/supabase-server.ts` (`getServerUser`)
- `src/app/auth/callback/route.ts`, `src/app/auth/signout/route.ts`, `src/app/auth/reset-password/page.tsx`
- `src/app/login/LoginPageClient.tsx` (`signInWithOtp`, `signInWithPassword`), `src/app/register/RegisterPageClient.tsx` (`signUp`, `resend`), `src/app/forgot-password/ForgotPasswordClient.tsx` (`resetPasswordForEmail`), `src/components/oauth-buttons.tsx` (`signInWithOAuth`)
- `src/lib/runtime-config.ts` (`hasPublicSupabaseConfig`, `hasServerSupabaseConfig`, demo-mode auto-enable keyed on `NEXT_PUBLIC_SUPABASE_URL`)

**Data, web (supabase-js, service role + RLS):** `src/lib/actions.ts` (profile settings, submit simple/advanced generation, `getGeneration`, `retryGeneration`, `createPendingGeneration`, `getMyGenerations`, `getGenerationStats`); `src/lib/types.ts` mirrors the `public` schema.

**Data, Engine (raw PostgREST over HTTP with the service-role key):**
- `Services/SupabaseDeliveryService.cs` implements `IDeliveryService` (PATCH `generations`, RPCs `increment_token_usage` / `append_build_log`, owner email and credential lookups via `profiles` embeds, stale-row CAS for reconciliation)
- `Services/StripeWebhookHandler.cs` (`stripe_events` insert/delete, RPC `process_checkout_completed`, `transactions` and `generations` PATCH)
- `Services/StripeRefundService.cs` (`transactions` state transitions)
- `Program.cs` maps `Supabase:Url` / `Supabase:ServiceRoleKey`

**Realtime:** `src/lib/hooks/use-generation-realtime.ts`, `src/app/dashboard/GenerationsLiveRefresher.tsx`.

**Isolation (RLS, replaced by code):** own-row SELECT on `profiles`, `generations`, `transactions`; own-row INSERT on `generations`; own-row UPDATE on `profiles`; service-role-only writes elsewhere.

**Ops:** `.github/workflows/ci.yml` E2E job (`CI_SUPABASE_DB_URL`, `supabase db push` against the CI project); `.github/workflows/deploy-prod.yml` (Supabase URL/keys into the box env, `PROD_SUPABASE_DB_URL` migration apply with a drift guard); `next.config.ts` CSP allows `*.supabase.co`.

**User-facing copy that names Supabase as StackAlchemist's own provider:** `src/app/privacy/page.tsx` (auth + database processor; this is legal copy and must change in the same release as the cutover). Copy describing the *generated* product's stack (`pricing`, `faq-section`, `hero-section`, `faq-manifest`, `compare-manifest`) stays as is: the generated code still ships Supabase wiring.

## Phases

Order: **A (platform) → B (data) → C (auth) → D (CI) → E (cutover) → F (retire)**. Data goes before auth here, the reverse of recharacter, because the Engine and web data swap can land while Supabase Auth still supplies the user id. That keeps each phase shippable on its own.

### A. Platform provisioning (qavren-db + qavren-auth repos; no StackAlchemist code)

- qavren-db: `pwsh tools/provision-app.ps1 -App stackalchemist -Env test -Apply`; commit `apps/stackalchemist.yaml`. **Prod provisioning (`-Env prod`) is staged for the owner:** the auto-mode classifier refuses prod DB provisioning.
- qavren-auth: `realms/apps/stackalchemist.yaml` from `realms/_template.yaml`, with `registrationAllowed: true` + `verifyEmail: true` (the fleet review found realms with registration on and no verification, so don't repeat that), `resetPasswordAllowed: true`, fleet SMTP block, Google IdP on the shared client, public PKCE client `stackalchemist-web`, `frontendUrl: https://auth.stackalchemist.app`, a login theme; plus the `hostnames.auto.tfvars` entry. **Owner actions:** `terraform apply` for the hostname, adding the realm's broker redirect URI to the shared Google OAuth client, and the `-AllRealms` realm apply.
- Store the printed pooler/session URLs in StackAlchemist's `test` and `production` environment secrets as `DATABASE_URL` / `DATABASE_URL_MIGRATE`.

### B. Data → `stackalchemist` schema (StackAlchemist repo; Supabase Auth still in place)

- Drizzle schema for `profiles`, `generations`, `transactions`, `stripe_events`, reproducing the prod columns, CHECKs, FKs and indexes listed in the appendix. `profiles.id` becomes a plain `uuid` with no FK (it will hold the Keycloak `sub` after phase C, and the Supabase user id until then). One hand-written SQL migration adds the five kept functions and two triggers.
- Web: replace `createServerClient()` data calls in `actions.ts` with Drizzle over `DATABASE_URL` (`prepare: false`, `max` 10 in prod). The user id still comes from `getServerUser()` for now. **Every statement scopes by `user_id = <session user>`**; that is the replacement for RLS.
- Engine: `PostgresDeliveryService : IDeliveryService` on Npgsql (`ConnectionStrings:Db`, `Pooling` via Supavisor, no prepared statements: `No Reset On Close=true;Max Auto Prepare=0`). Port `StripeWebhookHandler` and `StripeRefundService` off `rest/v1` onto the same connection. The RPCs become `SELECT stackalchemist.fn(...)`. The CAS patches keep their `WHERE` pins as SQL predicates and check the affected-row count.
- **Gate before any query swap lands:** a two-user integration suite against real Postgres 17 proving user B can't read, retry or see stats for user A's generations or transactions. This is the RLS safety net, the same lesson as recharacter's `*-rls.integration.test.ts` suites.
- Realtime → polling (decision 4). Drop `@supabase/supabase-js` data usage; keep `@supabase/ssr` until phase C.

### C. Auth → Keycloak realm `stackalchemist`

- Add `auth.config.ts` / `auth.ts` / `app/api/auth/[...nextauth]/route.ts` exactly as recharacter does (composed callbacks; `idToken` on the JWT only; `trustHost: true` behind Cloudflare Tunnel; `session.user.id = token.sub`).
- `middleware.ts` → Next 16 `proxy.ts` built on `auth()`, with the protected-prefix list taken from today's middleware. Pages and actions still check the session themselves; the proxy is a redirect convenience, not the boundary.
- `getServerUser()` → `getSessionUser()` returning `{ id: sub, email }`, and an idempotent `profiles` upsert on first sight (replaces `handle_new_user`).
- Delete the login/register/forgot/reset/callback pages and `oauth-buttons.tsx`. `/login` becomes one Server Action button. `/auth/signout` becomes recharacter's RP-initiated logout route.
- `runtime-config.ts`: demo mode keys off `DATABASE_URL`/`QAVREN_AUTH_URL` instead of the Supabase URL; drop the Supabase key validators. CSP: drop `*.supabase.co`, add the auth hostname to `form-action`/`connect-src` as needed.
- Privacy page: auth processor becomes Qavren Solutions' self-hosted Keycloak; database becomes Supabase (still the host, via qavren-db).
- Remove `@supabase/ssr` and `@supabase/supabase-js` from `package.json`.

### D. CI

- E2E job: drop `CI_SUPABASE_DB_URL` and the Supabase CLI. Run against a Postgres 17 service container with `db:migrate`, plus the phase B isolation suite. For signed-in E2E, run a Keycloak container with a fixture realm and a direct-grant test client, as qavren-auth's `sdk-test` realm does. Remember the KC 26 trap: fixture users need `firstName`/`lastName` or direct grant fails with `invalid_grant "Account is not fully set up"`.
- Nightly: optionally run the same suite against `qavren-db-test` (Pro, doesn't pause) for a real-pooler smoke.
- `deploy-prod.yml`: replace the Supabase migration step and its drift guard with a `migrate` job (`DATABASE_URL_MIGRATE`) ahead of `deploy`, and swap the Supabase env for `DATABASE_URL`, `AUTH_SECRET`, `QAVREN_AUTH_URL`, `QAVREN_REALM`.

### E. Cutover (owner-run, from a runbook this phase writes)

> **Runbook written 2026-09-30:** `docs/runbooks/qavren-cutover-phase-e.md` (state table, owner steps §1.1–1.7, flip §2, verification §3, pause §4, rollback §5). Same PR: `deploy-prod.yml` gained the secrets preflight (two legitimate shapes, URL shape checks), a warning when a push changes Drizzle migrations while prod has no qavren-db target, and the end-of-run mode check. Done that day by the assistant: Prod `AUTH_SECRET` minted and set (inert until `QAVREN_AUTH_URL`); qavren-auth edge applied after the owner granted it (`auth.stackalchemist.app` resolves, bridge 308 live); the prod schema provision, the realm apply and the Google console remain owner steps (classifier / SSM / no API). Corrections to the list below as written on 2026-09-28: step 2 is GitHub Prod secrets, not the box `.env` (the deploy regenerates `.env`); the Engine reads `DATABASE_URL`, not `ConnectionStrings__Db` (phase B); the Supabase secrets STAY until phase F (nothing reads them in Qavren mode, `deploy-test.yml` still does); `www.stackalchemist.app` currently serves the app and must 301 to the apex first; the committed qavren-db manifest makes the nightly prod backup red from 2026-10-01 until the prod schema exists.

1. Prod realm and hostname live (A); `stackalchemist` prod schema provisioned; `migrate` job applied.
2. Prod environment secrets: `DATABASE_URL_MIGRATE` and `AUTH_SECRET` may go in early (inert / pre-apply only); `DATABASE_URL` and `QAVREN_AUTH_URL` go in **together at the flip**, one deploy (`QAVREN_REALM` defaults to `stackalchemist` in compose). The deploy's preflight refuses any other shape. Keep the Supabase secrets until F.
3. Deploy; re-register the owner account; run one Tier 0 generation end to end (submit → status → build log → download) and one Stripe checkout against `process_checkout_completed` (test-mode only if the Prod keys are test keys; otherwise buy and refund).
4. Pause the old project; start the 7-day clock.

> **Executed 2026-10-01** (deploy 36806238503; record in the runbook's "Execution record"). §3.1–3.3 and §3.5 passed in a browser. §3.4 (paid checkout + refund on qavren-db) is still open: the keys are live. `ctqhwykryoglhdwatljt` is on the Pro plan, so it cannot be paused; the 7-day clock runs from the flip, which makes the delete date on or after 2026-10-08.

### F. Retire

> **Execution plan written 2026-10-03:** `2026-10-03-qavren-replatform-F-retire.md`. It has gates (Task 1, the polling-only status transport, ships now; the code deletions merge on or after 2026-10-08 and only once §3.4 is proven) and a recommendation to retire the dead test mirror instead of giving it a realm. The list below is the 2026-09-28 sketch; the execution plan supersedes it.

- Day 7+: delete `ctqhwykryoglhdwatljt`. Delete `cdlefpvsvyepofsboepc` once D is green on main (it's needed until then). Delete `supabase/` and `docs/runbooks/ci-supabase-migrations.md`. Remove the Supabase secrets from both GitHub environments. Update the vault project note and the workspace memory that describes the auto-pause.

## Order and risk

- **Biggest risk: the RLS → code-scoping swap (B).** The two-user isolation suite lands and passes against the Drizzle layer before any production query path switches.
- **Stripe webhook (B):** `process_checkout_completed` is the only money path. Port it with its idempotency (`stripe_events` insert-or-skip) intact, and prove it with a replayed-event test that asserts one transaction, not two.
- **Engine reconciliation CAS (B):** `TryClaimForRequeueAsync` / `TryFailStaleRowAsync` depend on PostgREST returning the row count through `Prefer`. The SQL port must check `rows affected == 1` or a restart can double-requeue a job.
- **Session UX parity (C):** Keycloak's own screens replace custom ones; check the email-verification and reset emails actually arrive (fleet SES), since a missing SMTP config fails silently.
- **Pooler (B):** transaction mode means no session state: no `SET`, no advisory locks across statements, no prepared statements (both clients configured for this).

## Appendix: prod schema to reproduce (from `ctqhwykryoglhdwatljt`, 2026-09-28)

- `profiles(id uuid pk, email text not null, api_key_override text, preferred_model text not null default 'claude-sonnet-4-6', created_at timestamptz not null default now())`
- `generations(id uuid pk default gen_random_uuid(), user_id uuid → profiles on delete set null, transaction_id uuid → transactions on delete set null, mode text not null check in (simple, advanced), tier int not null default 0 check 0..3, prompt text, schema_json jsonb, status text not null default 'pending' check in (pending, extracting_schema, generating_code, generating, building, packing, uploading, success, failed), download_url text, preview_files_json jsonb, build_log text, error_message text, attempt_count int not null default 0, created_at/updated_at timestamptz not null default now(), completed_at timestamptz, project_type text not null default 'DotNetNextJs' check in (DotNetNextJs, PythonReact), personalization_json jsonb, input_tokens int not null default 0, output_tokens int not null default 0, model_used text, error_category text check null or in (quota, schema, build, rate_limit, network, internal))`; indexes on `user_id`, `status`, `project_type`
- `transactions(id uuid pk default gen_random_uuid(), user_id uuid → profiles on delete set null, stripe_session_id text unique, tier int not null check 0..3, amount int not null default 0, status text not null default 'pending' check in (pending, completed, failed, refund_pending, refunded, disputed), created_at/updated_at timestamptz not null default now(), generation_id uuid → generations on delete set null, stripe_payment_intent text, stripe_charge_id text, last_stripe_event_id text)`; indexes on `user_id`, `stripe_session_id`, partial on `stripe_payment_intent` and `stripe_charge_id` where not null
- `stripe_events(id text pk, type text not null, processed_at timestamptz not null default now())`
- Triggers: `generations_updated_at` (BEFORE UPDATE → `set_updated_at`), `generations_enforce_free_quota` (BEFORE INSERT → `enforce_free_generation_quota`: tier 0 needs a user and allows 5 non-failed per calendar month)
- Function bodies: take them from the prod dump (`pg_get_functiondef`), change `search_path` from `public` to `stackalchemist`, drop `SECURITY DEFINER`.
