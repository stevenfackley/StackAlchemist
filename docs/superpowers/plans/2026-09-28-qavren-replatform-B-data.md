# Re-platform phase B: data → qavren-db (dual-mode)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Parent plan: `2026-09-28-qavren-replatform.md`; phase A record: `2026-09-28-qavren-replatform-A-platform.md`.

**Goal:** Every read and write StackAlchemist makes against its database can run over plain Postgres on qavren-db (schema `stackalchemist`), selected by the presence of `DATABASE_URL`, with the Supabase path untouched and still the default until cutover.

**Architecture:** Dual-mode behind two seams. Web: a `DataStore` interface in `src/lib/data/` with a `SupabaseStore` (today's supabase-js calls, moved verbatim) and a `DrizzleStore` (Drizzle over postgres-js); `actions.ts` calls the store, not supabase-js. Engine: `IDeliveryService` already exists, so `PostgresDeliveryService` sits beside `SupabaseDeliveryService`; the Stripe webhook and refund paths get a new `IBillingStore` seam with the same two implementations. DI/`getDataStore()` pick Postgres when `DATABASE_URL` is set. RLS is replaced by owner scoping in code, proven by two-user integration suites on real Postgres. Nothing in prod changes when this merges: prod has no `DATABASE_URL` until phase E.

**Tech stack:** drizzle-orm 0.45 + drizzle-kit 0.31 + postgres (postgres-js) 3.4 in the web app; Npgsql 9 in the Engine; Testcontainers.PostgreSql 4.x for Engine tests; a `postgres:17-alpine` service container for the web integration suites in CI.

---

## Decisions specific to this phase

1. **Dual-mode, not a cutover.** Merging to `main` deploys prod. Prod's schema isn't provisioned yet (owner step, phase A task 4) and the owner account hasn't re-registered (phase E). So the new path only activates when `DATABASE_URL` is present; everything else keeps calling Supabase exactly as today. Phase E flips the env; phase F deletes the Supabase implementations.
2. **Realtime → polling moves to phase C** (amends the parent plan's phase B). The browser Supabase client survives until phase C anyway (auth needs it), and ripping Realtime out now would slow prod's status updates to the 30 s poll before there's any benefit. Phase B is server-side data only.
3. **`getGenerationById` stays unscoped.** Today `getGeneration()` reads with the service role and `/generate/[id]/page.tsx` never checks ownership: a result page is reachable by anyone holding the UUID (the retry action, by contrast, is owner-checked). That is current product behaviour, so the store preserves it. Whether result pages should become owner-only is a product call for Steve, filed as a question, not decided here. Everything else (list, stats, quota count, retry, profile) is scoped by `user_id` in the SQL itself. **Decided 2026-09-30 by Steve: owner-only.** `getGenerationById` was replaced by `getGenerationForUser(id, userId)`, scoped in the SQL in both stores; `getGeneration` (the result page and the status watcher's polling), `retryGeneration` and `createCheckoutSession` all read through it with the session user. There is no unscoped by-id read left in the web data layer.
4. **The migration files are the single schema source.** `src/StackAlchemist.Web/drizzle/*.sql` (drizzle-kit output plus one hand-written functions/triggers migration). The Engine's Postgres tests apply those same files into a Testcontainers Postgres, so the two halves can never drift from each other.
5. **Drizzle: `generate` only, custom migrator.** Copied from recharacter: drizzle's stock `migrate()` opens with `CREATE SCHEMA IF NOT EXISTS`, which Postgres refuses (42501) for a role without CREATE on the database even when the schema exists and the role owns it. `scripts/migrate.mjs` drives the loop itself under an advisory lock, and the first migration's schema creation is a `DO $$ ... pg_namespace ... $$` guard.
6. **plpgsql stays plpgsql.** `set_updated_at`, `enforce_free_generation_quota` (+ triggers), `append_build_log`, `increment_token_usage`, `process_checkout_completed` are ported into schema `stackalchemist`, fully qualified, **without** `SECURITY DEFINER` (the app role owns everything; definer rights buy nothing). `handle_new_user` (trigger on `auth.users`) and `rls_auto_enable` are dropped; profiles are upserted from the app (already the case for `saveProfileSettings`; phase C adds the sign-in upsert).
7. **Pooler rules.** Runtime uses the Supavisor transaction pooler (`:6543`): postgres-js `prepare: false`, Npgsql `Max Auto Prepare=0; No Reset On Close=true` (gavel-suite ADR-0017). Migrations use the session URL (`:5432`). No `SET`, no session state, no advisory locks across statements at runtime (the migrator's advisory lock lives inside one transaction, which is fine).
8. **TLS.** URLs carry `?sslmode=require` (phase A). postgres-js honours it from the URL. Npgsql does not parse libpq URIs, so `PostgresUrl.ToNpgsqlConnectionString()` converts. **Corrected 2026-09-30 (Task 4 review, verified against Npgsql 9.0.5 by reflection): `SslMode.Require` encrypts WITHOUT validating the server certificate, exactly like libpq's `require`; `TrustServerCertificate` is obsolete and does nothing.** So both sides are encrypt-only today, the same posture the fleet runs (recharacter). The converter maps all six libpq `sslmode` values strictly and throws on anything else (an unrecognised value used to degrade silently to `Prefer`), and maps `sslrootcert` → `Root Certificate` so a move to `verify-full` is a URL change plus shipping Supabase's CA file to the box. That move is a fleet-wide follow-up, not this phase.
9. **JSON parity.** The Engine serializes `schema_json` / `personalization_json` with `JsonNamingPolicy.SnakeCaseLower` today (PostgREST payloads). `PostgresDeliveryService` must use the same options when writing jsonb, and the same case-insensitive options when reading, or the web reads a differently-shaped schema.
10. **Task 1 review amendments (2026-09-29).** The Opus review of Task 1 changed four details every later task relies on: (a) `getDb(url)` keys its pool cache on the URL (a `Map` on `globalThis`), so tests may pass `TEST_DATABASE_URL` while `DATABASE_URL` is also set; (b) `vitest.config.ts` aliases `server-only` to an empty module (`__tests__/mocks/server-only.ts`), otherwise nothing importing `@/db` or `@/lib/data` can run under vitest; (c) timestamps come back as ISO strings (`customType` over `timestamp with time zone` that returns `new Date(v).toISOString()`), because Drizzle's postgres-js driver otherwise returns Postgres text (`2026-09-29 12:34:56+00`), which Safari's `new Date()` rejects while today's PostgREST rows are ISO; (d) the migrator verifies the ledger (an applied migration whose hash changed, or an unapplied one older than the newest applied, fails the run) and never lets a malformed DSN reach a log line. Task 3's suite constructs the store inside `beforeAll` (vitest evaluates a skipped `describe` body) and asserts the ISO shape of `created_at`.
11. **Task 2 review amendments (2026-09-30).** (a) `retryGeneration` HONOURS `resetForRetry`'s boolean: the Engine's `/api/generate` enqueues unconditionally, so two concurrent retries that both pass the read checks would both fire the Engine (double LLM spend); a `false` from the atomic scoped UPDATE returns the existing "Only failed generations can be retried." (b) `runtime-config.ts` does NOT read `DATABASE_URL` for demo auto-detection: that module also runs in the browser, where Next never exposes non-`NEXT_PUBLIC_` vars, so the server and client would disagree about demo mode in dev. Phase C keys auto-demo on a `NEXT_PUBLIC_` value. `hasDataStoreConfig()` and a `usesPostgresStore()` helper (used by `getDataStore()` too) live in runtime-config. (c) `SupabaseStore.getGenerationById` uses `maybeSingle()` plus a UUID guard so both stores return null for unknown/malformed ids. (d) The interface type is `ProfileSettingsRow` (not `ProfileRow`, which `src/db/schema.ts` already exports). (e) A recording-stub unit test covers `SupabaseStore`'s `user_id` filters: the service-role client bypasses RLS, so those filters are the only tenant isolation on the prod path until phase E.
12. **Task 3 review amendments (2026-09-30).** (a) drizzle-orm 0.45 wraps driver failures in `DrizzleQueryError` whose message embeds the bound parameters; `DrizzleStore` unwraps to the Postgres error (`driverError()`), fails closed when there is no cause, and takes the quota trigger's text only from a real `PostgresError`. `PostgresError.detail` can still carry "Failing row contains (...)", same as PostgREST's `details` today. (b) The CI drift guard must also require drizzle-kit's literal "No schema changes" output: on a non-TTY its rename-or-create prompt exits 0 having written nothing, so a rename would otherwise pass. (c) `enforce_free_generation_quota` uses `date_trunc('month', now(), 'UTC')` so the trigger and the store's UTC month start agree whatever the session TimeZone; changed while no ledger-tracked database had applied `0001` (the migrator's hash check freezes it after Task 8). (d) `listMyGenerations` orders by `created_at desc, id desc` in both stores (paging over equal timestamps). (e) `insertGeneration` names its seven columns explicitly. (f) UUID guards match across both stores. (g) `getMyGenerations` clamps `pageSize` to 1..100.
13. **Task 4 notes (2026-09-30).** (a) Register the data source with `Npgsql.DependencyInjection`'s `AddNpgsqlDataSource(cs, dsb => dsb.ConnectionStringBuilder.MaxPoolSize = 10)` in both `Program.cs` files: factory-built (disposed at shutdown) and wired to the host's `ILoggerFactory`; pool size lives here, not in the URL converter. The Engine computes the connection string once from `DATABASE_URL` and uses that local for the config entry, the production either/or check and the registration, so a stray `ConnectionStrings__Db` can't bypass the pooler flags. (b) The committed `packages.lock.json` files were already stale (dependents pinned old ranges of the Engine's own packages) and CI restores WITHOUT `--locked-mode`, so "locked-mode succeeds" is a local check only; Task 4 regenerated all four locks, which also moved floating minors. (c) `V1TemplateCompileTests.*BuildsBothHalves` time out (15 min each) on this Windows box regardless of these changes; run local Engine tests with `--filter "FullyQualifiedName!~BuildsBothHalves&FullyQualifiedName!~DockerTargetBuilds"` and let CI's ubuntu runner prove those gates. (d) The repo's `npm postinstall` bootstrap writes a stub `.env` at the worktree root; DotNetEnv `TraversePath()` loads it in tests. Its values are placeholders (verified), so it is harmless, but a real `.env` there would be loaded by `EngineHostTests`.
14. **Task 5 notes (2026-09-30).** (a) **Prod bug found and fixed on the Supabase path:** `SupabaseDeliveryService.ParseSnapshot` deserialized `schema_json` with default (case-sensitive) options, so `{"entities": ...}` never populated `Entities` and every row the reconciler requeued re-entered the pipeline with an empty schema. Both services now share `Data/SnapshotMapper.cs` (case-insensitive reads); a Supabase regression test pins it. This is the one intentional behaviour change on the Supabase path in this PR; say so in the PR body. (b) `PostgresFixture` splits migration files on `-->\s*statement-breakpoint` wherever it sits (drizzle-kit appends the marker to the end of FK/index lines), builds the container only after the Docker check (Testcontainers' `Build()` throws without an endpoint), applies the files once per container, and backdates `updated_at` by disabling the `generations_updated_at` trigger inside one transaction. (c) Retry rule: non-transient errors break immediately; Npgsql's `IsTransient` (serialization failure, deadlock, 53300) retries like 408/429 did. (d) `PostgresException` logs carry SQLSTATE and constraint name only; messages can quote row values. (e) `TryPatchOnceAsync` returns false for a missing row (PostgREST's `return=minimal` counted that as success): a buffered write for a deleted row re-buffers until the reconciler's 1 h cap instead of vanishing at once; harmless, documented. (f) Pre-existing, out of scope: the Worker host never registers `IPendingWriteBuffer`, `IRefundService` or `IInFlightGenerationRegistry`, so neither delivery service resolves there; the Worker is not in `docker-compose.prod.yml`. File an issue in Task 9.
15. **One branch, one PR.** `feat/qavren-db-data`. Tasks land as commits; the PR is reviewed as a whole (opus review + green CI is the standing merge condition). The deploy this merge triggers should be a no-op behaviourally; Task 9 verifies that on prod.

## Coupling this phase removes (from `origin/main`, 2026-09-29)

| Where | Today | After |
|---|---|---|
| `src/lib/actions.ts` (11 supabase-js query chains) | `createServerClient()` inline | `getDataStore()` → `DataStore` methods |
| `src/lib/runtime-config.ts` | `hasServerSupabaseConfig()` gates data access; demo auto-enables when `NEXT_PUBLIC_SUPABASE_URL` is unset | `hasDataStoreConfig()` = `DATABASE_URL` or the Supabase pair; demo auto-enables only when neither is set |
| Engine `SupabaseDeliveryService` (PATCH + 2 RPCs + CAS + joins) | only `IDeliveryService` | + `PostgresDeliveryService`, DI-selected |
| Engine `StripeWebhookHandler`, `StripeRefundService` (raw PostgREST inline) | inline HTTP | `IBillingStore` → `SupabaseBillingStore` (moved code) / `PostgresBillingStore` |
| Worker `Program.cs` | registers `SupabaseDeliveryService` | same DI selection as the Engine |
| `.github/actions/setup-env`, `docker-compose.prod.yml`, `deploy-prod.yml` | Supabase env only | + `DATABASE_URL` pass-through and a gated migrate step |

Untouched in this phase: everything under `src/app/auth/*`, `middleware.ts`, `supabase-server.ts`, the browser client, Realtime hooks, `supabase/migrations/` (still applied to the legacy projects until phase F), the E2E Integration job (still runs against the CI Supabase project in Supabase mode).

## File map

**Web (`src/StackAlchemist.Web/`)**
- Create `drizzle.config.ts`, `src/db/schema.ts`, `src/db/index.ts`, `scripts/migrate.mjs`, `drizzle/0000_init.sql`, `drizzle/0001_functions.sql`, `drizzle/meta/*` (generated)
- Create `src/lib/data/store.ts` (interface + types), `src/lib/data/supabase-store.ts`, `src/lib/data/drizzle-store.ts`, `src/lib/data/index.ts` (`getDataStore`)
- Create `__tests__/data/store-selection.test.ts`, `__tests__/data/drizzle-store.integration.test.ts`
- Modify `src/lib/actions.ts`, `src/lib/runtime-config.ts`, `package.json`, the six `__tests__/lib/actions-*.test.ts` + `__tests__/hooks/use-free-quota.test.ts` + `__tests__/dashboard/ByokSettingsForm.test.tsx` runtime-config mocks, `__tests__/lib/runtime-config.test.ts`

**Engine (`src/StackAlchemist.Engine/`)**
- Create `Data/PostgresUrl.cs`, `Data/DeliveryJson.cs`, `Services/PostgresDeliveryService.cs`, `Services/IBillingStore.cs`, `Services/SupabaseBillingStore.cs`, `Services/PostgresBillingStore.cs`
- Modify `Program.cs`, `Services/StripeWebhookHandler.cs`, `Services/StripeRefundService.cs`, `StackAlchemist.Engine.csproj` (+ `packages.lock.json`), `../StackAlchemist.Worker/Program.cs`

**Engine tests (`src/StackAlchemist.Engine.Tests/`)**
- Create `Data/PostgresUrlTests.cs`, `Integration/PostgresFixture.cs`, `Services/PostgresDeliveryServiceTests.cs`, `Services/PostgresBillingStoreTests.cs`
- Modify `Webhooks/StripeWebhookTests.cs`, `Services/StripeRefundServiceTests.cs` (construction only), `StackAlchemist.Engine.Tests.csproj` (+ lock)

**Ops**
- Modify `.github/workflows/ci.yml`, `.github/workflows/deploy-prod.yml`, `.github/actions/setup-env/action.yml`, `docker-compose.prod.yml`
- Create `docs/runbooks/qavren-db-migrations.md`

## Local Postgres for every task below

```bash
docker run -d --name sa-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=stackalchemist_test -p 55440:5432 postgres:17-alpine
export TEST_DATABASE_URL='postgres://postgres:postgres@127.0.0.1:55440/stackalchemist_test'
```

Port 55440 on purpose: 5432 and 55432/55433 are taken by other Qavren stacks on this machine. `postgres` is a superuser here, so the `DO` block in `0000_init.sql` creates the schema; on qavren-db the schema pre-exists and the block is a no-op.

---

### Task 1: Drizzle schema, migrations, migrator

**Files:**
- Modify: `src/StackAlchemist.Web/package.json`
- Create: `src/StackAlchemist.Web/drizzle.config.ts`
- Create: `src/StackAlchemist.Web/src/db/schema.ts`
- Create: `src/StackAlchemist.Web/src/db/index.ts`
- Create: `src/StackAlchemist.Web/scripts/migrate.mjs`
- Create: `src/StackAlchemist.Web/drizzle/0000_init.sql` (generated, then edited), `drizzle/0001_functions.sql` (custom), `drizzle/meta/_journal.json` + snapshots (generated)

- [ ] **Step 1: Add the dependencies**

```bash
cd src/StackAlchemist.Web
npm install drizzle-orm@^0.45 postgres@^3.4
npm install -D drizzle-kit@^0.31
```

Then add to `package.json` `scripts`:

```json
"db:generate": "drizzle-kit generate",
"db:migrate": "node ./scripts/migrate.mjs",
"db:check": "drizzle-kit check"
```

`npm run audit:ci` must still pass (allowlist is `audit-allowlist.json`); if drizzle-kit drags in an advisory, resolve it before committing, don't allowlist it.

- [ ] **Step 2: `drizzle.config.ts`**

```ts
import { defineConfig } from "drizzle-kit";

/**
 * `drizzle-kit generate` ONLY. Never run `drizzle-kit migrate` or `push`
 * against qavren-db: both open with `CREATE SCHEMA IF NOT EXISTS`, which
 * Postgres refuses with 42501 for a role that lacks CREATE on the database —
 * which the app role does, even though it owns the `stackalchemist` schema.
 * Migrations are applied by `npm run db:migrate` (scripts/migrate.mjs).
 */
export default defineConfig({
  dialect: "postgresql",
  schema: "./src/db/schema.ts",
  out: "./drizzle",
  schemaFilter: ["stackalchemist"],
  migrations: { schema: "stackalchemist", table: "__drizzle_migrations" },
  dbCredentials: { url: process.env.DATABASE_URL_MIGRATE ?? process.env.DATABASE_URL ?? "" },
});
```

- [ ] **Step 3: `src/db/schema.ts`** — the prod schema from the parent plan's appendix, verbatim column names (snake_case property names so rows match `Generation` in `src/lib/types.ts` without a mapping layer; `mode: "string"` on timestamps for the same reason)

```ts
import { sql } from "drizzle-orm";
import {
  check, index, integer, jsonb, pgSchema, text, timestamp, uuid, type AnyPgColumn,
} from "drizzle-orm/pg-core";
import type {
  GenerationErrorCategory, GenerationSchema, GenerationStatus, PersonalizationData, ProjectType, Tier,
} from "@/lib/types";

export const sa = pgSchema("stackalchemist");

const tz = { withTimezone: true, mode: "string" } as const;

export const profiles = sa.table("profiles", {
  // Supabase user id until phase C, then the Keycloak `sub`. Plain uuid, no FK.
  id: uuid("id").primaryKey(),
  email: text("email").notNull(),
  api_key_override: text("api_key_override"),
  preferred_model: text("preferred_model").notNull().default("claude-sonnet-4-6"),
  created_at: timestamp("created_at", tz).notNull().defaultNow(),
});

export const generations = sa.table(
  "generations",
  {
    id: uuid("id").primaryKey().default(sql`gen_random_uuid()`),
    user_id: uuid("user_id").references(() => profiles.id, { onDelete: "set null" }),
    transaction_id: uuid("transaction_id").references((): AnyPgColumn => transactions.id, { onDelete: "set null" }),
    mode: text("mode").$type<"simple" | "advanced">().notNull(),
    tier: integer("tier").$type<Tier>().notNull().default(0),
    prompt: text("prompt"),
    schema_json: jsonb("schema_json").$type<GenerationSchema>(),
    status: text("status").$type<GenerationStatus>().notNull().default("pending"),
    download_url: text("download_url"),
    preview_files_json: jsonb("preview_files_json").$type<Record<string, string>>(),
    build_log: text("build_log"),
    error_message: text("error_message"),
    attempt_count: integer("attempt_count").notNull().default(0),
    created_at: timestamp("created_at", tz).notNull().defaultNow(),
    updated_at: timestamp("updated_at", tz).notNull().defaultNow(),
    completed_at: timestamp("completed_at", tz),
    project_type: text("project_type").$type<ProjectType>().notNull().default("DotNetNextJs"),
    personalization_json: jsonb("personalization_json").$type<PersonalizationData>(),
    input_tokens: integer("input_tokens").notNull().default(0),
    output_tokens: integer("output_tokens").notNull().default(0),
    model_used: text("model_used"),
    error_category: text("error_category").$type<GenerationErrorCategory>(),
  },
  (t) => [
    check("generations_mode_check", sql`${t.mode} in ('simple', 'advanced')`),
    check("generations_tier_check", sql`${t.tier} >= 0 and ${t.tier} <= 3`),
    check("generations_status_check", sql`${t.status} in ('pending', 'extracting_schema', 'generating_code', 'generating', 'building', 'packing', 'uploading', 'success', 'failed')`),
    check("generations_project_type_check", sql`${t.project_type} in ('DotNetNextJs', 'PythonReact')`),
    check("generations_error_category_check", sql`${t.error_category} is null or ${t.error_category} in ('quota', 'schema', 'build', 'rate_limit', 'network', 'internal')`),
    index("idx_generations_user_id").on(t.user_id),
    index("idx_generations_status").on(t.status),
    index("idx_generations_project_type").on(t.project_type),
  ],
);

export const transactions = sa.table(
  "transactions",
  {
    id: uuid("id").primaryKey().default(sql`gen_random_uuid()`),
    user_id: uuid("user_id").references(() => profiles.id, { onDelete: "set null" }),
    stripe_session_id: text("stripe_session_id").unique("transactions_stripe_session_id_key"),
    tier: integer("tier").notNull(),
    amount: integer("amount").notNull().default(0),
    status: text("status").notNull().default("pending"),
    created_at: timestamp("created_at", tz).notNull().defaultNow(),
    generation_id: uuid("generation_id").references((): AnyPgColumn => generations.id, { onDelete: "set null" }),
    stripe_payment_intent: text("stripe_payment_intent"),
    stripe_charge_id: text("stripe_charge_id"),
    last_stripe_event_id: text("last_stripe_event_id"),
    updated_at: timestamp("updated_at", tz).notNull().defaultNow(),
  },
  (t) => [
    check("transactions_status_check", sql`${t.status} in ('pending', 'completed', 'failed', 'refund_pending', 'refunded', 'disputed')`),
    check("transactions_tier_check", sql`${t.tier} >= 0 and ${t.tier} <= 3`),
    index("idx_transactions_user_id").on(t.user_id),
    index("idx_transactions_stripe_session").on(t.stripe_session_id),
    index("idx_transactions_payment_intent").on(t.stripe_payment_intent).where(sql`${t.stripe_payment_intent} is not null`),
    index("idx_transactions_charge").on(t.stripe_charge_id).where(sql`${t.stripe_charge_id} is not null`),
  ],
);

export const stripe_events = sa.table("stripe_events", {
  id: text("id").primaryKey(),
  type: text("type").notNull(),
  processed_at: timestamp("processed_at", tz).notNull().defaultNow(),
});

export type GenerationRow = typeof generations.$inferSelect;
export type NewGenerationRow = typeof generations.$inferInsert;
export type ProfileRow = typeof profiles.$inferSelect;
```

- [ ] **Step 4: Generate the first migration and fix its schema statement**

```bash
npm run db:generate -- --name init
```

Expected: `drizzle/0000_init.sql` + `drizzle/meta/0000_snapshot.json` + `drizzle/meta/_journal.json`. Open `0000_init.sql`; its first statement is `CREATE SCHEMA "stackalchemist";`. Replace that one statement (keep the `--> statement-breakpoint` after it) with:

```sql
-- NOT `CREATE SCHEMA IF NOT EXISTS`: Postgres checks CREATE on the database
-- before it honours IF NOT EXISTS, so that form raises 42501 for the qavren-db
-- role even though the schema is pre-created and owned by it. Guarding on
-- pg_namespace means the statement is never issued when the schema exists.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'stackalchemist') THEN
    EXECUTE 'CREATE SCHEMA "stackalchemist"';
  END IF;
END $$;
```

Do not touch anything else in the generated file: the migrator hashes it, and `db:check` compares it against the snapshot.

- [ ] **Step 5: Custom migration for functions and triggers**

```bash
npm run db:generate -- --custom --name functions
```

Fill `drizzle/0001_functions.sql` with the prod function bodies, schema-qualified, no `SECURITY DEFINER`, one statement per `--> statement-breakpoint`:

```sql
CREATE OR REPLACE FUNCTION stackalchemist.set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
begin
  new.updated_at = now();
  return new;
end;
$$;
--> statement-breakpoint
CREATE TRIGGER generations_updated_at
BEFORE UPDATE ON stackalchemist.generations
FOR EACH ROW EXECUTE FUNCTION stackalchemist.set_updated_at();
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.enforce_free_generation_quota() RETURNS trigger
LANGUAGE plpgsql AS $$
declare
  used int;
begin
  if new.tier <> 0 then
    return new;
  end if;
  if new.user_id is null then
    raise exception 'Free-tier generations require an authenticated account'
      using errcode = 'check_violation';
  end if;
  -- UTC on purpose: the web app's pre-check compares created_at against a UTC
  -- month start, and this keeps the trigger on the same boundary whatever the
  -- session TimeZone is.
  select count(*) into used
    from stackalchemist.generations
   where user_id = new.user_id
     and tier = 0
     and status <> 'failed'
     and created_at >= date_trunc('month', now(), 'UTC');
  if used >= 5 then
    raise exception 'Free generation limit reached: 5 builds per month. Upgrade to download or wait until next month.'
      using errcode = 'check_violation';
  end if;
  return new;
end;
$$;
--> statement-breakpoint
CREATE TRIGGER generations_enforce_free_quota
BEFORE INSERT ON stackalchemist.generations
FOR EACH ROW EXECUTE FUNCTION stackalchemist.enforce_free_generation_quota();
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.append_build_log(gen_id uuid, chunk text) RETURNS void
LANGUAGE plpgsql AS $$
begin
  update stackalchemist.generations
  set build_log = case when build_log is null or build_log = '' then chunk
                       else build_log || E'\n' || chunk end,
      updated_at = now()
  where id = gen_id;
end;
$$;
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.increment_token_usage(gen_id uuid, input_delta integer, output_delta integer, model_name text) RETURNS void
LANGUAGE plpgsql AS $$
begin
  update stackalchemist.generations
  set input_tokens  = coalesce(input_tokens, 0)  + greatest(coalesce(input_delta, 0), 0),
      output_tokens = coalesce(output_tokens, 0) + greatest(coalesce(output_delta, 0), 0),
      model_used    = coalesce(nullif(model_name, ''), model_used),
      updated_at    = now()
  where id = gen_id;
end;
$$;
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.process_checkout_completed(
  p_event_id text, p_event_type text, p_session_id text, p_payment_intent text,
  p_generation_id uuid, p_tier integer, p_amount bigint)
RETURNS TABLE(is_new boolean, mode text, prompt text, project_type text, schema_json jsonb, personalization_json jsonb)
LANGUAGE plpgsql AS $$
declare
  v_new boolean;
begin
  insert into stackalchemist.stripe_events (id, type) values (p_event_id, p_event_type)
  on conflict (id) do nothing;
  v_new := found;
  if not v_new then
    return query select false, null::text, null::text, null::text, null::jsonb, null::jsonb;
    return;
  end if;
  update stackalchemist.generations g set tier = p_tier where g.id = p_generation_id;
  insert into stackalchemist.transactions
    (stripe_session_id, stripe_payment_intent, tier, amount, status, generation_id, last_stripe_event_id, updated_at)
  values
    (p_session_id, p_payment_intent, p_tier, p_amount::int, 'completed', p_generation_id, p_event_id, now())
  on conflict (stripe_session_id) do update set
    stripe_payment_intent = excluded.stripe_payment_intent,
    tier                  = excluded.tier,
    amount                = excluded.amount,
    status                = excluded.status,
    generation_id         = excluded.generation_id,
    last_stripe_event_id  = excluded.last_stripe_event_id,
    updated_at            = now();
  return query
    select true, g.mode, g.prompt, g.project_type, g.schema_json, g.personalization_json
    from stackalchemist.generations g where g.id = p_generation_id;
  if not found then
    return query select true, null::text, null::text, null::text, null::jsonb, null::jsonb;
  end if;
end;
$$;
```

- [ ] **Step 6: `scripts/migrate.mjs`** — recharacter's migrator, ESM, schema renamed

```js
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readMigrationFiles } from "drizzle-orm/migrator";
import postgres from "postgres";

// Session-mode URL (:5432 on qavren-db). The transaction pooler cannot hold
// the advisory lock or the long transaction this needs. Prod URLs MUST carry
// `?sslmode=require`: postgres-js reads sslmode out of the URL.
const url = process.env.DATABASE_URL_MIGRATE ?? process.env.DATABASE_URL;
if (!url) {
  console.error("Set DATABASE_URL_MIGRATE (session URL) or DATABASE_URL");
  process.exit(1);
}

const SCHEMA = "stackalchemist";
const TABLE = "__drizzle_migrations";
const LOCK_KEY = 7364102; // arbitrary, fixed: every migrator run contends on it

// Drizzle's stock migrate() opens with CREATE SCHEMA IF NOT EXISTS, which raises
// 42501 for the qavren-db role (no CREATE on the database), so we drive the same
// loop ourselves: one transaction, advisory lock first, ledger inside the app
// schema, statements split on `--> statement-breakpoint` (not `;`, so plpgsql
// bodies survive). See recharacter web/scripts/migrate.ts for the full history.
const here = path.dirname(fileURLToPath(import.meta.url));
const migrations = readMigrationFiles({
  migrationsFolder: path.resolve(here, "../drizzle"),
  migrationsSchema: SCHEMA,
  migrationsTable: TABLE,
});

const sql = postgres(url, { max: 1, prepare: false, onnotice: () => {} });
let applied = 0;
try {
  await sql.begin(async (tx) => {
    await tx.unsafe(`select pg_advisory_xact_lock(${LOCK_KEY})`);
    await tx.unsafe(`SET LOCAL lock_timeout = '5s'`);
    await tx.unsafe(`SET LOCAL statement_timeout = '5min'`);

    const [existing] = await tx`select 1 from pg_namespace where nspname = ${SCHEMA}`;
    if (!existing) await tx.unsafe(`CREATE SCHEMA "${SCHEMA}"`);

    await tx.unsafe(
      `CREATE TABLE IF NOT EXISTS "${SCHEMA}"."${TABLE}" (id SERIAL PRIMARY KEY, hash text NOT NULL, created_at bigint)`,
    );
    const [last] = await tx.unsafe(
      `select created_at from "${SCHEMA}"."${TABLE}" order by created_at desc limit 1`,
    );
    for (const migration of migrations) {
      if (last && Number(last.created_at) >= migration.folderMillis) continue;
      for (const stmt of migration.sql) await tx.unsafe(stmt);
      await tx.unsafe(
        `insert into "${SCHEMA}"."${TABLE}" ("hash", "created_at") values($1, $2)`,
        [migration.hash, migration.folderMillis],
      );
      applied += 1;
    }
  });
  console.log(applied === 0 ? "migrations applied (already up to date)" : `migrations applied (${applied})`);
} catch (err) {
  // Code and message ONLY: a postgres-js connection error carries the full DSN
  // (password included) on err.input.
  console.error({ code: err?.code, message: err?.message });
  process.exitCode = 1;
} finally {
  await sql.end();
}
```

- [ ] **Step 7: `src/db/index.ts`** — lazy postgres-js + Drizzle singleton

```ts
import "server-only";
import { drizzle, type PostgresJsDatabase } from "drizzle-orm/postgres-js";
import postgres from "postgres";
import * as schema from "./schema";

export type Db = PostgresJsDatabase<typeof schema>;

const g = globalThis as unknown as { __saSql?: postgres.Sql; __saDb?: Db };

/**
 * `prepare: false` is mandatory on the Supavisor transaction pooler (:6543)
 * that DATABASE_URL points at in prod; `max` stays small because the pooler
 * multiplexes. Survives Next dev HMR via globalThis. DATABASE_URL carries
 * `?sslmode=require` in prod (postgres-js reads it from the URL); the explicit
 * `ssl` below is belt and braces for a URL that lost the parameter.
 */
export function getDb(url = process.env.DATABASE_URL): Db {
  if (!url) throw new Error("DATABASE_URL is not set");
  if (g.__saDb) return g.__saDb;
  const sql = postgres(url, {
    prepare: false,
    max: process.env.NODE_ENV === "production" ? 10 : 4,
    connect_timeout: 15,
    idle_timeout: 30,
    ssl: process.env.NODE_ENV === "production" ? "require" : undefined,
  });
  g.__saSql = sql;
  g.__saDb = drizzle(sql, { schema });
  return g.__saDb;
}

export async function closeDb(): Promise<void> {
  await g.__saSql?.end({ timeout: 5 });
  g.__saSql = undefined;
  g.__saDb = undefined;
}
```

- [ ] **Step 8: Apply to the local container, twice**

```bash
DATABASE_URL_MIGRATE="$TEST_DATABASE_URL" npm run db:migrate
DATABASE_URL_MIGRATE="$TEST_DATABASE_URL" npm run db:migrate
npm run db:check
docker exec sa-pg psql -U postgres -d stackalchemist_test -c "\dt stackalchemist.*" -c "\df stackalchemist.*" -c "select tgname from pg_trigger where tgrelid = 'stackalchemist.generations'::regclass"
```

Expected: first run `migrations applied (2)`, second `already up to date`; `db:check` reports no issues; `\dt` lists `__drizzle_migrations, generations, profiles, stripe_events, transactions`; `\df` lists the five functions; two triggers.

- [ ] **Step 9: Lint, typecheck, commit**

```bash
npm run lint && npx tsc --noEmit
git add package.json package-lock.json drizzle.config.ts src/db drizzle scripts/migrate.mjs
git commit -m "feat(db): Drizzle schema and migrations for the qavren-db stackalchemist schema"
```

### Task 2: the `DataStore` seam, Supabase implementation, `actions.ts` on the seam

Behaviour-preserving refactor: every user-facing string in `actions.ts` stays byte-identical, and the existing action tests keep passing with one mechanical change to their `runtime-config` mocks.

**Files:**
- Create: `src/StackAlchemist.Web/src/lib/data/store.ts`
- Create: `src/StackAlchemist.Web/src/lib/data/supabase-store.ts`
- Create: `src/StackAlchemist.Web/src/lib/data/index.ts`
- Create: `src/StackAlchemist.Web/__tests__/data/store-selection.test.ts`
- Modify: `src/StackAlchemist.Web/src/lib/runtime-config.ts`
- Modify: `src/StackAlchemist.Web/src/lib/actions.ts`
- Modify: `__tests__/lib/actions-{byok,checkout,demo-mode,generation,retry}.test.ts`, `__tests__/hooks/use-free-quota.test.ts`, `__tests__/dashboard/ByokSettingsForm.test.tsx` (mock objects only), `__tests__/lib/runtime-config.test.ts`

- [ ] **Step 1: Write the failing selection test**

`__tests__/data/store-selection.test.ts`:

```ts
// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/data/drizzle-store", () => ({ DrizzleStore: class { kind = "drizzle" as const; } }));
vi.mock("@/lib/data/supabase-store", () => ({ SupabaseStore: class { kind = "supabase" as const; } }));

describe("getDataStore", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("picks Drizzle when DATABASE_URL is set", async () => {
    vi.stubEnv("DATABASE_URL", "postgres://u:p@localhost:5432/db");
    const { getDataStore } = await import("@/lib/data");
    expect(getDataStore().kind).toBe("drizzle");
  });

  it("falls back to Supabase when DATABASE_URL is unset", async () => {
    vi.stubEnv("DATABASE_URL", "");
    const { getDataStore } = await import("@/lib/data");
    expect(getDataStore().kind).toBe("supabase");
  });
});
```

Run: `npx vitest run __tests__/data/store-selection.test.ts` → FAIL (module `@/lib/data` not found).

- [ ] **Step 2: `src/lib/data/store.ts`** — the interface. Row shapes are the ones `actions.ts` already consumes.

```ts
import type { Generation, GenerationSchema, PersonalizationData, ProjectType, Tier } from "@/lib/types";

export interface ProfileRow {
  email: string;
  api_key_override: string | null;
  preferred_model: string | null;
}

export interface ProfileUpsert {
  id: string;
  email: string;
  preferred_model: string;
  /** undefined = leave the stored key alone; null = clear it. */
  api_key_override?: string | null;
}

export interface NewGeneration {
  mode: "simple" | "advanced";
  tier: Tier;
  prompt: string | null;
  project_type: ProjectType;
  schema_json: GenerationSchema | null;
  personalization_json: PersonalizationData | null;
  user_id: string;
}

/** Thrown by every store method on a failed query; actions.ts maps it to the same user-facing messages it shows today. */
export class DataStoreError extends Error {
  constructor(message: string, readonly cause?: unknown) {
    super(message);
    this.name = "DataStoreError";
  }
}

export interface DataStore {
  readonly kind: "drizzle" | "supabase";
  getProfile(userId: string): Promise<ProfileRow | null>;
  upsertProfile(profile: ProfileUpsert): Promise<void>;
  /** Same predicate as the enforce_free_generation_quota trigger: tier 0, not failed, created this calendar month (UTC). */
  countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number>;
  insertGeneration(gen: NewGeneration): Promise<Generation>;
  /** By id, deliberately unscoped: parity with today's service-role read (see phase B decision 3). */
  getGenerationById(id: string): Promise<Generation | null>;
  /** Scoped in SQL: only the owner's failed row flips back to pending. Returns whether a row changed. */
  resetForRetry(id: string, userId: string): Promise<boolean>;
  listMyGenerations(userId: string, offset: number, limit: number): Promise<{ generations: Generation[]; total: number }>;
  generationStats(userId: string): Promise<{ total: number; completed: number; inProgress: number }>;
}
```

- [ ] **Step 3: `src/lib/data/supabase-store.ts`** — today's query chains, moved. It imports `createServerClient` from `@/lib/supabase` so the existing `vi.mock("@/lib/supabase")` in the action tests still intercepts it.

```ts
import { createServerClient } from "@/lib/supabase";
import type { Generation } from "@/lib/types";
import { DataStoreError, type DataStore, type NewGeneration, type ProfileRow, type ProfileUpsert } from "./store";

type Client = ReturnType<typeof createServerClient>;

/** Supabase-JS over PostgREST with the service role. Retired in phase F. */
export class SupabaseStore implements DataStore {
  readonly kind = "supabase" as const;
  private db?: Client;

  private client(): Client {
    // Lazy: createServerClient() throws when unconfigured, and actions.ts
    // already turns that into its "configuration incomplete" messages.
    return (this.db ??= createServerClient());
  }

  async getProfile(userId: string): Promise<ProfileRow | null> {
    const { data, error } = await this.client()
      .from("profiles").select("email, api_key_override, preferred_model").eq("id", userId).maybeSingle();
    if (error) throw new DataStoreError("profiles select failed", error);
    return data ?? null;
  }

  async upsertProfile(profile: ProfileUpsert): Promise<void> {
    const { api_key_override, ...rest } = profile;
    const row = { ...rest, ...(api_key_override !== undefined ? { api_key_override } : {}) };
    const { error } = await this.client().from("profiles").upsert(row, { onConflict: "id" });
    if (error) throw new DataStoreError("profiles upsert failed", error);
  }

  async countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number> {
    const { count, error } = await this.client()
      .from("generations").select("id", { count: "exact", head: true })
      .eq("user_id", userId).eq("tier", 0).neq("status", "failed")
      .gte("created_at", monthStartUtc.toISOString());
    if (error) throw new DataStoreError("generations count failed", error);
    return count ?? 0;
  }

  async insertGeneration(gen: NewGeneration): Promise<Generation> {
    const { data, error } = await this.client()
      .from("generations")
      .insert({ ...gen, status: "pending", download_url: null, error_message: null, attempt_count: 0, completed_at: null })
      .select().single();
    if (error || !data) throw new DataStoreError("generations insert failed", error);
    return data as Generation;
  }

  async getGenerationById(id: string): Promise<Generation | null> {
    const { data, error } = await this.client().from("generations").select("*").eq("id", id).single();
    if (error) throw new DataStoreError("generations select failed", error);
    return (data as Generation) ?? null;
  }

  async resetForRetry(id: string, userId: string): Promise<boolean> {
    const { data, error } = await this.client()
      .from("generations").update({ status: "pending", error_message: null })
      .eq("id", id).eq("user_id", userId).eq("status", "failed").select("id");
    if (error) throw new DataStoreError("generations update failed", error);
    return (data?.length ?? 0) === 1;
  }

  async listMyGenerations(userId: string, offset: number, limit: number) {
    const { data, error, count } = await this.client()
      .from("generations").select("*", { count: "exact" })
      .eq("user_id", userId).order("created_at", { ascending: false }).range(offset, offset + limit - 1);
    if (error) throw new DataStoreError("generations list failed", error);
    return { generations: (data ?? []) as Generation[], total: count ?? 0 };
  }

  async generationStats(userId: string) {
    const base = () => this.client().from("generations").select("*", { count: "exact", head: true }).eq("user_id", userId);
    const [t, c, p] = await Promise.all([base(), base().eq("status", "success"), base().not("status", "in", "(success,failed)")]);
    if (t.error || c.error || p.error) throw new DataStoreError("generations stats failed", t.error ?? c.error ?? p.error);
    return { total: t.count ?? 0, completed: c.count ?? 0, inProgress: p.count ?? 0 };
  }
}
```

Note the one deliberate change inside `resetForRetry`: the update is scoped by `user_id` and `status = 'failed'` in the query, and `retryGeneration` honours its boolean (decision 11a). The retry happy-path test's update mock must therefore resolve `{ data: [{ id }], error: null }`, and ids in that suite must be real UUIDs because `getGenerationById` returns null for anything else.

- [ ] **Step 4: `src/lib/data/index.ts`**

```ts
import "server-only";
import { usesPostgresStore } from "@/lib/runtime-config";
import type { DataStore } from "./store";
import { SupabaseStore } from "./supabase-store";
import { DrizzleStore } from "./drizzle-store";

export type { DataStore, NewGeneration, ProfileSettingsRow, ProfileUpsert } from "./store";
export { DataStoreError } from "./store";

/**
 * Postgres (qavren-db) when DATABASE_URL is set, Supabase otherwise. The
 * decision is made per call, not at module load, so tests can flip the env.
 * Phase E sets DATABASE_URL in prod; phase F deletes SupabaseStore.
 */
export function getDataStore(): DataStore {
  return usesPostgresStore() ? new DrizzleStore() : new SupabaseStore();
}
```

Until Task 3 lands, create `drizzle-store.ts` as a stub that throws `new Error("DrizzleStore lands in Task 3")` from every method, with `kind = "drizzle"`. Task 3 replaces it.

- [ ] **Step 5: `runtime-config.ts`**

Leave `_autoDemo` alone (decision 11b: this module runs in the browser too). Add:

```ts
/** The qavren-db path is configured. Server-only: DATABASE_URL never reaches the browser bundle. */
export function usesPostgresStore() {
  return Boolean(process.env.DATABASE_URL);
}

/** A server-side store is reachable: qavren-db (DATABASE_URL) or the Supabase service role pair. */
export function hasDataStoreConfig() {
  return usesPostgresStore() || hasServerSupabaseConfig();
}
```

Keep `hasServerSupabaseConfig()` exported (auth code and phase C still read it). Add a case to `__tests__/lib/runtime-config.test.ts`: `DATABASE_URL` alone → `hasDataStoreConfig() === true`.

- [ ] **Step 6: `actions.ts` on the seam**

Mechanical: `import { getDataStore, DataStoreError } from "./data";` replaces `import { createServerClient } from "./supabase";` and `hasServerSupabaseConfig` → `hasDataStoreConfig` in the import and every call. Then per function:

- `getProfileSettings`: `const p = await getDataStore().getProfile(user.id)` inside try/catch; on catch log `[getProfileSettings] Query error:` and fall through to the defaults, as today.
- `saveProfileSettings`: `await getDataStore().upsertProfile({ id: user.id, email: user.email ?? "", preferred_model: preferredModel, api_key_override: apiKeyOverride })`; catch → the existing `"Failed to save API settings."`. The `createServerClient()` try/catch that returned `"Supabase server configuration is incomplete."` becomes the `hasDataStoreConfig()` check that already precedes it (same string).
- `countFreeGenerationsThisMonth(userId)`: drop the `db` parameter; `try { return await getDataStore().countFreeGenerationsThisMonth(userId, currentMonthStartUtc()) } catch (e) { console.error("[countFreeGenerationsThisMonth] count failed:", e); return 0; }` — fail-open stays.
- `submitSimpleGeneration` / `submitAdvancedGeneration` / `createPendingGeneration`: replace the `createServerClient()` try/catch + `.from("generations").insert(...)` with `let data: Generation; try { data = await getDataStore().insertGeneration({...}) } catch (e) { console.error("[submitSimpleGeneration] Supabase insert error:", e); return { success: false, error: "Failed to create generation record. Please try again." }; }`. Keep each function's existing log prefix and message string exactly.
- `getGeneration`: `try { return await getDataStore().getGenerationById(generationId) } catch (e) { console.error("[getGeneration] Error:", e); return null; }`.
- `retryGeneration`: read via `getGenerationById`, keep the three checks and their messages verbatim, then `const reset = await getDataStore().resetForRetry(generationId, user.id); if (!reset) return { success: false, error: "Only failed generations can be retried." };` (decision 11a: the atomic scoped UPDATE is what stops a concurrent retry from firing the Engine twice), then the Engine call as today.
- `getMyGenerations` / `getGenerationStats` / `getFreeQuotaStatus`: straight delegation with the existing `empty` / `full` fallbacks on catch.

Delete the now-unused `createServerClient` import. Grep the file: `grep -n "supabase\|Supabase" src/lib/actions.ts` should show only log-prefix strings and comments.

- [ ] **Step 7: Tests**

In each test file that mocks `@/lib/runtime-config` wholesale and reaches `@/lib/data` (the five `actions-*.test.ts`; `use-free-quota.test.ts` and `ByokSettingsForm.test.tsx` mock `@/lib/actions` instead and need nothing), add `hasDataStoreConfig: vi.fn(() => true),` and `usesPostgresStore: vi.fn(() => false),` next to `hasServerSupabaseConfig`, and where a test does `vi.mocked(hasServerSupabaseConfig).mockReturnValue(false)` to exercise the unconfigured path, do the same for `hasDataStoreConfig`. Nothing else should need to change: `makeDb` still feeds `createServerClient`.

```bash
npx vitest run
npx tsc --noEmit && npm run lint
```

Expected: all green, including `__tests__/data/store-selection.test.ts`.

- [ ] **Step 8: Commit**

```bash
git add src/lib/data src/lib/actions.ts src/lib/runtime-config.ts __tests__
git commit -m "refactor(web): route server actions through a DataStore seam (Supabase impl unchanged)"
```

### Task 3: `DrizzleStore`, the isolation suite, CI Postgres

**Files:**
- Replace: `src/StackAlchemist.Web/src/lib/data/drizzle-store.ts`
- Create: `src/StackAlchemist.Web/__tests__/data/drizzle-store.integration.test.ts`
- Modify: `.github/workflows/ci.yml` (frontend job)

- [ ] **Step 1: Write the failing integration suite**

`__tests__/data/drizzle-store.integration.test.ts`. It runs only where `TEST_DATABASE_URL` is set (locally: the container from the top of this plan; CI: the service container added in step 4). Skipping locally is fine; **CI always has the URL, so CI always runs it.**

```ts
// @vitest-environment node
import { randomUUID } from "node:crypto";
import postgres from "postgres";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { DrizzleStore } from "@/lib/data/drizzle-store";
import { closeDb } from "@/db";

const url = process.env.TEST_DATABASE_URL;

describe.skipIf(!url)("DrizzleStore against real Postgres", () => {
  // Built in beforeAll, not in the describe body: vitest evaluates a skipped
  // describe's body, and getDb() throws when the URL is missing.
  let store: DrizzleStore;
  let sql: ReturnType<typeof postgres>;
  const alice = randomUUID();
  const bob = randomUUID();

  beforeAll(async () => {
    store = new DrizzleStore(url);
    sql = postgres(url!, { max: 1 });
    await sql`insert into stackalchemist.profiles (id, email) values (${alice}, 'alice@example.test'), (${bob}, 'bob@example.test')`;
  });
  beforeEach(async () => {
    await sql`delete from stackalchemist.generations where user_id in (${alice}, ${bob})`;
  });
  afterAll(async () => {
    await sql`delete from stackalchemist.generations where user_id in (${alice}, ${bob})`;
    await sql`delete from stackalchemist.profiles where id in (${alice}, ${bob})`;
    await sql.end();
    await closeDb();
  });

  const gen = (user_id: string, tier: 0 | 1 = 1) => store.insertGeneration({
    mode: "simple", tier, prompt: "p", project_type: "DotNetNextJs", schema_json: null, personalization_json: null, user_id,
  });

  it("insert returns the row with defaults applied", async () => {
    const g = await gen(alice);
    expect(g.status).toBe("pending");
    expect(g.attempt_count).toBe(0);
    // ISO with a Z, the shape PostgREST rows had; Postgres text form breaks Safari's Date parser.
    expect(g.created_at).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/);
  });

  it("list and stats are scoped to the caller", async () => {
    await gen(alice); await gen(alice); await gen(bob);
    const a = await store.listMyGenerations(alice, 0, 20);
    const b = await store.listMyGenerations(bob, 0, 20);
    expect(a.total).toBe(2);
    expect(b.total).toBe(1);
    expect(a.generations.every((g) => g.user_id === alice)).toBe(true);
    expect((await store.generationStats(bob)).total).toBe(1);
  });

  it("retry cannot flip another user's failed row", async () => {
    const g = await gen(alice);
    await sql`update stackalchemist.generations set status = 'failed' where id = ${g.id}`;
    expect(await store.resetForRetry(g.id, bob)).toBe(false);
    expect(await store.resetForRetry(g.id, alice)).toBe(true);
    expect((await store.getGenerationById(g.id))?.status).toBe("pending");
  });

  it("retry only flips failed rows", async () => {
    const g = await gen(alice);
    expect(await store.resetForRetry(g.id, alice)).toBe(false);
  });

  it("free-tier quota: the trigger rejects the sixth non-failed tier-0 build this month", async () => {
    for (let i = 0; i < 5; i++) await gen(alice, 0);
    expect(await store.countFreeGenerationsThisMonth(alice, monthStart())).toBe(5);
    await expect(gen(alice, 0)).rejects.toThrow(/Free generation limit reached/);
    expect(await store.countFreeGenerationsThisMonth(bob, monthStart())).toBe(0);
  });

  it("profile upsert: undefined leaves the key, null clears it", async () => {
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-sonnet-4-6", api_key_override: "v1:cipher" });
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-3-5-haiku-20241022" });
    expect((await store.getProfile(alice))?.api_key_override).toBe("v1:cipher");
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-sonnet-4-6", api_key_override: null });
    expect((await store.getProfile(alice))?.api_key_override).toBeNull();
  });

  it("getGenerationById returns null for an unknown or malformed id", async () => {
    expect(await store.getGenerationById(randomUUID())).toBeNull();
    expect(await store.getGenerationById("demo-simple-123")).toBeNull();
  });
});

function monthStart() {
  const n = new Date();
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), 1));
}
```

Run: `TEST_DATABASE_URL=$TEST_DATABASE_URL npx vitest run __tests__/data/drizzle-store.integration.test.ts` → FAIL (stub throws).

- [ ] **Step 2: `src/lib/data/drizzle-store.ts`**

```ts
import { and, count, desc, eq, gte, ne, sql } from "drizzle-orm";
import { getDb, type Db } from "@/db";
import { generations, profiles } from "@/db/schema";
import type { Generation } from "@/lib/types";
import { DataStoreError, type DataStore, type NewGeneration, type ProfileSettingsRow, type ProfileUpsert } from "./store";

// Same guard SupabaseStore applies: both stores return null for a malformed id.
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Drizzle over postgres-js against the qavren-db `stackalchemist` schema. */
export class DrizzleStore implements DataStore {
  readonly kind = "drizzle" as const;
  private readonly db: Db;

  constructor(url?: string) {
    this.db = getDb(url);
  }

  async getProfile(userId: string): Promise<ProfileSettingsRow | null> {
    if (!UUID.test(userId)) return null;
    try {
      const [row] = await this.db
        .select({ email: profiles.email, api_key_override: profiles.api_key_override, preferred_model: profiles.preferred_model })
        .from(profiles).where(eq(profiles.id, userId)).limit(1);
      return row ?? null;
    } catch (e) { throw new DataStoreError("profiles select failed", e); }
  }

  async upsertProfile(p: ProfileUpsert): Promise<void> {
    const set: Partial<typeof profiles.$inferInsert> = { email: p.email, preferred_model: p.preferred_model };
    if (p.api_key_override !== undefined) set.api_key_override = p.api_key_override;
    try {
      await this.db.insert(profiles)
        .values({ id: p.id, email: p.email, preferred_model: p.preferred_model, api_key_override: p.api_key_override ?? null })
        .onConflictDoUpdate({ target: profiles.id, set });
    } catch (e) { throw new DataStoreError("profiles upsert failed", e); }
  }

  async countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number> {
    try {
      const [{ n }] = await this.db.select({ n: count() }).from(generations).where(and(
        eq(generations.user_id, userId), eq(generations.tier, 0), ne(generations.status, "failed"),
        gte(generations.created_at, monthStartUtc.toISOString()),
      ));
      return Number(n);
    } catch (e) { throw new DataStoreError("generations count failed", e); }
  }

  async insertGeneration(gen: NewGeneration): Promise<Generation> {
    try {
      const [row] = await this.db.insert(generations).values(gen).returning();
      return row as Generation;
    } catch (e) {
      // The quota trigger raises check_violation (23514) with its own message;
      // surface that text so actions.ts callers (and the test) can see it.
      const msg = e instanceof Error ? e.message : "generations insert failed";
      throw new DataStoreError(msg, e);
    }
  }

  async getGenerationById(id: string): Promise<Generation | null> {
    if (!UUID.test(id)) return null;
    try {
      const [row] = await this.db.select().from(generations).where(eq(generations.id, id)).limit(1);
      return (row as Generation | undefined) ?? null;
    } catch (e) { throw new DataStoreError("generations select failed", e); }
  }

  async resetForRetry(id: string, userId: string): Promise<boolean> {
    if (!UUID.test(id) || !UUID.test(userId)) return false;
    try {
      const rows = await this.db.update(generations)
        .set({ status: "pending", error_message: null })
        .where(and(eq(generations.id, id), eq(generations.user_id, userId), eq(generations.status, "failed")))
        .returning({ id: generations.id });
      return rows.length === 1;
    } catch (e) { throw new DataStoreError("generations update failed", e); }
  }

  async listMyGenerations(userId: string, offset: number, limit: number) {
    try {
      const [rows, [{ n }]] = await Promise.all([
        this.db.select().from(generations).where(eq(generations.user_id, userId))
          .orderBy(desc(generations.created_at)).limit(limit).offset(offset),
        this.db.select({ n: count() }).from(generations).where(eq(generations.user_id, userId)),
      ]);
      return { generations: rows as Generation[], total: Number(n) };
    } catch (e) { throw new DataStoreError("generations list failed", e); }
  }

  async generationStats(userId: string) {
    try {
      const [r] = await this.db.select({
        total: count(),
        completed: sql<number>`count(*) filter (where ${generations.status} = 'success')`,
        inProgress: sql<number>`count(*) filter (where ${generations.status} not in ('success', 'failed'))`,
      }).from(generations).where(eq(generations.user_id, userId));
      return { total: Number(r.total), completed: Number(r.completed), inProgress: Number(r.inProgress) };
    } catch (e) { throw new DataStoreError("generations stats failed", e); }
  }
}
```

Run the suite → PASS (8 tests).

- [ ] **Step 3: The rest of the suite still green**

```bash
npx vitest run --coverage && npx tsc --noEmit && npm run lint
```

Coverage thresholds in `vitest.config.ts` are floors; the new files add covered lines, so the ratchet moves up, not down.

- [ ] **Step 4: CI — Postgres service + migrate + drift guard on the frontend job**

In `.github/workflows/ci.yml`, under `frontend:` add (before `steps:`):

```yaml
    services:
      postgres:
        image: postgres:17-alpine
        env:
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: stackalchemist_test
        ports: ["5432:5432"]
        options: >-
          --health-cmd "pg_isready -h 127.0.0.1 -U postgres -d stackalchemist_test"
          --health-interval 5s --health-timeout 3s --health-retries 10
    env:
      TEST_DATABASE_URL: postgres://postgres:postgres@127.0.0.1:5432/stackalchemist_test
```

and two steps after `Install Dependencies`:

```yaml
      - name: Migration drift guard
        # schema.ts and drizzle/ must agree: a schema edit without a generated
        # migration would only surface when prod's migrate step ran it. Both
        # checks are needed: on a non-TTY drizzle-kit's rename prompt exits 0
        # and writes nothing, so a rename leaves `git status` clean.
        run: |
          out=$(npm run db:generate -- --name ci-drift-check 2>&1); echo "$out"
          if ! grep -q "No schema changes" <<<"$out" || [ -n "$(git status --porcelain drizzle/)" ]; then
            git status --porcelain drizzle/
            echo "::error::src/db/schema.ts and drizzle/ disagree (or drizzle-kit needed an interactive answer). Run 'npm run db:generate -- --name <what-changed>' locally and commit drizzle/."
            exit 1
          fi
          npm run db:check

      - name: Apply migrations to the test database
        run: DATABASE_URL_MIGRATE="$TEST_DATABASE_URL" npm run db:migrate
```

The vitest step needs no change: `TEST_DATABASE_URL` is job-level env, so the integration file stops skipping.

- [ ] **Step 5: Commit**

```bash
git add src/lib/data/drizzle-store.ts __tests__/data ../../.github/workflows/ci.yml
git commit -m "feat(web): DrizzleStore over qavren-db with a two-user isolation suite; Postgres in the frontend CI job"
```

### Task 4: Engine — `PostgresUrl`, `NpgsqlDataSource`, DI selection

**Files:**
- Modify: `src/StackAlchemist.Engine/StackAlchemist.Engine.csproj` (+ `packages.lock.json`)
- Create: `src/StackAlchemist.Engine/Data/PostgresUrl.cs`
- Create: `src/StackAlchemist.Engine.Tests/Data/PostgresUrlTests.cs`
- Modify: `src/StackAlchemist.Engine/Program.cs`, `src/StackAlchemist.Worker/Program.cs`

- [ ] **Step 1: Add Npgsql**

```bash
dotnet add src/StackAlchemist.Engine package Npgsql --version 9.*
dotnet restore StackAlchemist.slnx --force-evaluate    # packages.lock.json is committed; CI restores with the lock
```

Commit the two lock files with the csproj change.

- [ ] **Step 2: Failing tests for the URL conversion**

`src/StackAlchemist.Engine.Tests/Data/PostgresUrlTests.cs`:

```csharp
using FluentAssertions;
using Npgsql;
using StackAlchemist.Engine.Data;

namespace StackAlchemist.Engine.Tests.Data;

public sealed class PostgresUrlTests
{
    [Fact]
    public void Converts_a_pooler_url_with_sslmode_require()
    {
        var cs = PostgresUrl.ToNpgsqlConnectionString(
            "postgres://stackalchemist.kenqz:p%40ss%3Aword@aws-1-us-east-1.pooler.supabase.com:6543/postgres?sslmode=require");

        var b = new NpgsqlConnectionStringBuilder(cs);
        b.Host.Should().Be("aws-1-us-east-1.pooler.supabase.com");
        b.Port.Should().Be(6543);
        b.Database.Should().Be("postgres");
        b.Username.Should().Be("stackalchemist.kenqz");
        b.Password.Should().Be("p@ss:word");            // percent-decoded
        b.SslMode.Should().Be(SslMode.Require);
        b.MaxAutoPrepare.Should().Be(0);                 // transaction pooler: no server-side prepare
        b.NoResetOnClose.Should().BeTrue();              // transaction pooler: no DISCARD ALL
    }

    [Fact]
    public void Defaults_port_and_leaves_ssl_prefer_when_unspecified()
    {
        var b = new NpgsqlConnectionStringBuilder(PostgresUrl.ToNpgsqlConnectionString("postgresql://u:p@localhost/db"));
        b.Port.Should().Be(5432);
        b.SslMode.Should().Be(SslMode.Prefer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_maps_to_null(string? url) =>
        PostgresUrl.ToNpgsqlConnectionString(url).Should().BeNull();

    [Fact]
    public void Rejects_a_non_postgres_scheme() =>
        FluentActions.Invoking(() => PostgresUrl.ToNpgsqlConnectionString("https://example.com/db"))
            .Should().Throw<FormatException>();
}
```

Run: `dotnet test src/StackAlchemist.Engine.Tests --filter PostgresUrlTests` → FAIL (type missing).

- [ ] **Step 3: `Data/PostgresUrl.cs`**

```csharp
using System.Web;
using Npgsql;

namespace StackAlchemist.Engine.Data;

/// <summary>
/// libpq URI (what qavren-db's provisioner prints and what DATABASE_URL carries)
/// to an Npgsql connection string. Npgsql does not parse URIs itself. The pooler
/// options are fixed here because DATABASE_URL always points at the Supavisor
/// transaction pooler in prod: no server-side prepared statements, no session
/// reset on close (gavel-suite ADR-0017).
/// </summary>
public static class PostgresUrl
{
    public static string? ToNpgsqlConnectionString(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var uri = new Uri(url);
        if (uri.Scheme is not ("postgres" or "postgresql"))
            throw new FormatException($"DATABASE_URL must be a postgres:// URI, got scheme '{uri.Scheme}'.");

        var userInfo = uri.UserInfo.Split(':', 2);
        var query = HttpUtility.ParseQueryString(uri.Query);

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            MaxAutoPrepare = 0,
            NoResetOnClose = true,
            MaxPoolSize = 10,
            SslMode = query["sslmode"] switch
            {
                "require" => SslMode.Require,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                "disable" => SslMode.Disable,
                _ => SslMode.Prefer,
            },
        };
        return b.ConnectionString;
    }
}
```

Run the tests → PASS.

- [ ] **Step 4: Engine `Program.cs` — config key, data source, selection, startup rule**

In the `AddInMemoryCollection` block add, next to the Supabase pair:

```csharp
    // qavren-db (phase B of the re-platform). Presence of DATABASE_URL selects the
    // Postgres implementations below; absence keeps every Supabase path exactly as-is.
    ["ConnectionStrings:Db"]          = PostgresUrl.ToNpgsqlConnectionString(Ev("DATABASE_URL")),
```

Replace the production `required` entry `("SUPABASE_SERVICE_ROLE_KEY", ...)` with a combined rule placed right after the loop:

```csharp
    if (string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:Db"]) &&
        string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceRoleKey"]))
        throw new InvalidOperationException(
            "Set DATABASE_URL (qavren-db) or SUPABASE_SERVICE_ROLE_KEY before starting in Production; the Engine has no store otherwise.");
```

Registration (replace `builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();`):

```csharp
var dbConnectionString = builder.Configuration["ConnectionStrings:Db"];
if (dbConnectionString is not null)
{
    // Npgsql.DependencyInjection: factory-built (disposed at shutdown), logs via ILoggerFactory.
    builder.Services.AddNpgsqlDataSource(dbConnectionString, dsb => dsb.ConnectionStringBuilder.MaxPoolSize = 10);
    builder.Services.AddSingleton<IDeliveryService, PostgresDeliveryService>();
    builder.Services.AddSingleton<IBillingStore, PostgresBillingStore>();
}
else
{
    builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();
    if (!string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceRoleKey"]))
        builder.Services.AddSingleton<IBillingStore, SupabaseBillingStore>();
}
```

`PostgresDeliveryService`, `IBillingStore` and the two billing stores arrive in Tasks 5 and 6; until then keep this block referencing only `IDeliveryService` and add the billing lines in Task 6. Add `using Npgsql; using StackAlchemist.Engine.Data;`.

Worker `Program.cs`: same `AddInMemoryCollection`-free host reads env directly; add before the delivery registration:

```csharp
var dbConnectionString = PostgresUrl.ToNpgsqlConnectionString(Environment.GetEnvironmentVariable("DATABASE_URL"));
if (dbConnectionString is not null)
{
    builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(dbConnectionString).Build());
    builder.Services.AddSingleton<IDeliveryService, PostgresDeliveryService>();
}
else
{
    builder.Services.AddSingleton<IDeliveryService, SupabaseDeliveryService>();
}
```

(The Worker project references the Engine project, so the types resolve.) Until Task 5 exists, register `SupabaseDeliveryService` in both branches so the build stays green; flip it in Task 5.

- [ ] **Step 5: Build + commit**

```bash
dotnet build src/StackAlchemist.Engine && dotnet build src/StackAlchemist.Worker && dotnet test src/StackAlchemist.Engine.Tests --filter PostgresUrlTests
git add src/StackAlchemist.Engine src/StackAlchemist.Worker src/StackAlchemist.Engine.Tests
git commit -m "feat(engine): DATABASE_URL to Npgsql conversion and a data source when qavren-db is configured"
```

### Task 5: Engine — `PostgresDeliveryService` + the Testcontainers fixture

**Files:**
- Modify: `src/StackAlchemist.Engine.Tests/StackAlchemist.Engine.Tests.csproj` (+ lock): add `Testcontainers.PostgreSql` 4.*
- Create: `src/StackAlchemist.Engine.Tests/Integration/PostgresFixture.cs`
- Create: `src/StackAlchemist.Engine/Data/DeliveryJson.cs`
- Create: `src/StackAlchemist.Engine/Services/PostgresDeliveryService.cs`
- Create: `src/StackAlchemist.Engine.Tests/Services/PostgresDeliveryServiceTests.cs`
- Modify: `src/StackAlchemist.Engine/Services/SupabaseDeliveryService.cs` (use `DeliveryJson`), both `Program.cs` (flip the registration)

- [ ] **Step 1: The fixture** — one Postgres for the collection, the web's migration files applied verbatim

```csharp
using System.Text.RegularExpressions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace StackAlchemist.Engine.Tests.Integration;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// A real Postgres 17 with the SAME migrations the web app ships
/// (src/StackAlchemist.Web/drizzle/*.sql), so the Engine's SQL is tested against
/// the schema prod will actually have. Skips locally without Docker; on CI the
/// backend job runs on ubuntu-latest, which has Docker, so a skip there means the
/// gate stopped gating and the tests fail instead (IntegrationToolchain rule).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        if (!IntegrationToolchain.Available("docker", "info", "Docker (Testcontainers)", requiredOnCi: true))
            return;

        await _pg.StartAsync();
        DataSource = new NpgsqlDataSourceBuilder(_pg.GetConnectionString()).Build();

        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "StackAlchemist.Web", "drizzle"), "*.sql").Order();
        await using var conn = await DataSource.OpenConnectionAsync();
        foreach (var file in files)
        {
            foreach (var stmt in Regex.Split(await File.ReadAllTextAsync(file), @"^-->\s*statement-breakpoint\s*$", RegexOptions.Multiline))
            {
                if (string.IsNullOrWhiteSpace(stmt)) continue;
                await using var cmd = new NpgsqlCommand(stmt, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        Available = true;
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null) await DataSource.DisposeAsync();
        await _pg.DisposeAsync();
    }

    /// <summary>Walks up from the test binary until StackAlchemist.slnx is found.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StackAlchemist.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("StackAlchemist.slnx not found above the test binary.");
    }

    public async Task<Guid> SeedGenerationAsync(Guid? userId = null, string status = "pending", int tier = 1, int attempts = 0)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        if (userId is { } u)
        {
            await using var p = new NpgsqlCommand("insert into stackalchemist.profiles (id, email) values ($1, $2) on conflict (id) do nothing", conn)
            { Parameters = { new() { Value = u }, new() { Value = $"{u}@example.test" } } };
            await p.ExecuteNonQueryAsync();
        }
        await using var g = new NpgsqlCommand(
            "insert into stackalchemist.generations (user_id, mode, tier, status, attempt_count, project_type) values ($1, 'simple', $2, $3, $4, 'DotNetNextJs') returning id", conn)
        { Parameters = { new() { Value = (object?)userId ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid }, new() { Value = tier }, new() { Value = status }, new() { Value = attempts } } };
        return (Guid)(await g.ExecuteScalarAsync())!;
    }
}
```

Tests guard with `if (!fx.Available) return;` at the top (mirrors the toolchain-skip convention in this test project). The fixture applies the migration files exactly once per container (it is a collection fixture, so that is once per test run); `CREATE TRIGGER` in `0001` is not idempotent and a second application would fail.

- [ ] **Step 2: Failing tests** — `Services/PostgresDeliveryServiceTests.cs`, `[Collection(PostgresCollection.Name)]`

Cover, each as its own `[Fact]`:
1. `UpdateStatusAsync(Success, downloadUrl)` → row `status='success'`, `download_url` set, `completed_at` not null.
2. `UpdateTokenUsageAsync` twice → `input_tokens`/`output_tokens` summed, `model_used` set.
3. `AppendBuildLogAsync` twice → `build_log` is `"a\nb"`.
4. `GetGenerationOwnerEmailAsync` → the seeded profile's email; null for a row with no user.
5. `TryBeginExtractionAsync`: pending → true and `status='extracting_schema'`; second call → false (double-submit guard); a `success` row → false.
6. `TryClaimForRequeueAsync`: seed `status='building', attempt_count=1`, force `updated_at = now() - interval '1 hour'` via SQL; snapshot from `GetStaleNonTerminalAsync(TimeSpan.FromMinutes(30))` contains it; claim → true and `attempt_count=2, status='pending'`; claiming again with the stale snapshot → false.
7. `TryFailStaleRowAsync` on a row that moved (update its status first) → false; on a stale one → true with `error_category` set.
8. Unconfigured behaviour is not this class's concern (DI never constructs it without a data source), so there is no "skips when unconfigured" test here; that stays in `SupabaseDeliveryServiceTests`.
9. Critical write with the data source pointed at a closed port (build a second `NpgsqlDataSource` on `localhost:1`) → `UpdateStatusAsync(Failed)` returns without throwing and `PendingWriteBuffer.Count == 1`. Keep the retry budget short in this test by constructing the service with `criticalMaxAttempts: 2, criticalBaseDelay: TimeSpan.FromMilliseconds(10)`.

Run → FAIL (type missing).

- [ ] **Step 3: `Data/DeliveryJson.cs`** — the two option sets both delivery services share

```csharp
using System.Text.Json;

namespace StackAlchemist.Engine.Data;

/// <summary>JSON shapes are part of the schema: the web reads schema_json / personalization_json as written here.</summary>
public static class DeliveryJson
{
    /// <summary>Writes (payloads, jsonb columns): snake_case, the PostgREST-era convention the web app parses.</summary>
    public static readonly JsonSerializerOptions Write = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    /// <summary>Reads: tolerant of either casing.</summary>
    public static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };
}
```

Point `SupabaseDeliveryService.JsonOpts` / `SnapshotJson` at these (delete the private copies).

- [ ] **Step 4: `Services/PostgresDeliveryService.cs`**

The shape mirrors `SupabaseDeliveryService` method-for-method; only the transport differs. Key rules:

- `generationId` arrives as a string; `Guid.TryParse` fails → return null/false (never throw; demo ids never reach the Engine, but the reconciler passes ids from the database, which are always uuids).
- `PatchGenerationAsync(id, payload, critical)` builds `UPDATE stackalchemist.generations SET c1 = $1, c2 = $2 ... WHERE id = $n` from the payload dictionary against a **column allowlist** (`status, updated_at, completed_at, download_url, error_message, error_category, preview_files_json, schema_json, attempt_count`); an unknown key throws `ArgumentException` (a programming error, not a runtime condition). jsonb columns get `NpgsqlDbType.Jsonb` with `JsonSerializer.Serialize(value, DeliveryJson.Write)`; ISO timestamp strings are passed as `timestamptz` (parse with `DateTimeOffset.Parse`).
- Retry budget identical to Supabase: critical 5 attempts with 1/2/4/8 s, otherwise 2 with 500 ms; **retry only on `NpgsqlException { IsTransient: true }` or `TimeoutException`**; a non-transient `PostgresException` (e.g. 23514 check violation) breaks immediately like a 4xx did, while transient SQLSTATEs (serialization failure, deadlock, 53300) retry. Exhausted critical write → `pendingWrites.Enqueue(...)`, same as today. Expose the two budgets as optional constructor parameters with today's defaults so the failure test can shorten them.
- RPC equivalents: `select stackalchemist.increment_token_usage($1,$2,$3,$4)` and `select stackalchemist.append_build_log($1,$2)`, 2 attempts / 1 s like `InvokeRpcAsync`.
- Owner email / credential: `select p.email, p.api_key_override, p.preferred_model from stackalchemist.generations g join stackalchemist.profiles p on p.id = g.user_id where g.id = $1`.
- Snapshot / stale list: `select id, status, tier, mode, prompt, project_type, schema_json, personalization_json, attempt_count, updated_at from stackalchemist.generations where ...`; stale filter `status in ('pending','extracting_schema','generating_code','generating','building','packing','uploading') and updated_at < now() - $1::interval`. Map rows with the same parsing rules as `ParseSnapshot` (`DeliveryJson.Read` for the two jsonb columns; `ProjectType` via `Enum.TryParse(ignoreCase: true)`).
- CAS (`TryConditionalPatchAsync`): `UPDATE ... WHERE id = $1 AND status = $2 AND attempt_count = $3 AND updated_at < $4` → `rowsAffected == 1`. Note `set_updated_at` fires on every UPDATE, so the loser's `updated_at` guard works exactly as it did under PostgREST.
- `TryBeginExtractionAsync`: `UPDATE ... SET status='extracting_schema', error_message=null, error_category=null, updated_at=now() WHERE id=$1 AND status IN ('pending','failed')` → `rowsAffected == 1`.
- `TryPatchOnceAsync`: one attempt through the same allowlisted builder, returns `rowsAffected >= 1`.
- Logging: reuse the existing `LoggerMessage` event ids 500–513 with "Postgres" wording; don't invent new ids.

Constructor: `(NpgsqlDataSource dataSource, IPendingWriteBuffer pendingWrites, ILogger<PostgresDeliveryService> logger, int criticalMaxAttempts = 5, TimeSpan? criticalBaseDelay = null)`.

- [ ] **Step 5: Flip the registrations** in both `Program.cs` (Task 4 left `SupabaseDeliveryService` in the Postgres branch) and run everything

```bash
dotnet build StackAlchemist.slnx
dotnet test src/StackAlchemist.Engine.Tests
```

Expected: the 9 new tests pass (or skip cleanly on a machine without Docker), every existing test still passes.

- [ ] **Step 6: Commit**

```bash
git add src/StackAlchemist.Engine src/StackAlchemist.Worker src/StackAlchemist.Engine.Tests
git commit -m "feat(engine): PostgresDeliveryService over Npgsql, tested against the web's migrations in Testcontainers"
```

### Task 6: Engine — `IBillingStore` (Stripe webhook + refund paths)

**Files:**
- Create: `src/StackAlchemist.Engine/Services/IBillingStore.cs`
- Create: `src/StackAlchemist.Engine/Services/SupabaseBillingStore.cs`
- Create: `src/StackAlchemist.Engine/Services/PostgresBillingStore.cs`
- Modify: `Services/StripeWebhookHandler.cs`, `Services/StripeRefundService.cs`, `Program.cs`
- Modify: `src/StackAlchemist.Engine.Tests/Webhooks/StripeWebhookTests.cs`, `Services/StripeRefundServiceTests.cs` (construction only)
- Create: `src/StackAlchemist.Engine.Tests/Services/PostgresBillingStoreTests.cs`

- [ ] **Step 1: The interface** — every Supabase helper the two services own today, and nothing else

```csharp
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public enum EventRecord { New, Duplicate, Unavailable }

public enum TransactionKey { StripeSessionId, StripePaymentIntent, StripeChargeId }

public sealed record EligibleTransaction(string Id, string PaymentIntentId);

public sealed record CheckoutOutcome(
    bool IsNew, string? Mode, string? Prompt, ProjectType? ProjectType,
    GenerationSchema? Schema, GenerationPersonalization? Personalization);

/// <summary>
/// The billing tables (stripe_events, transactions, and the tier/cancel writes on
/// generations) behind the Stripe webhook and the compile-guarantee refund.
/// Unregistered when no store is configured; callers treat a null store as
/// "no idempotency log, proceed" exactly as they treated a null SupabaseAdmin.
/// </summary>
public interface IBillingStore
{
    /// <summary>Insert into stripe_events; Duplicate on conflict; Unavailable when the store cannot be reached (never throws).</summary>
    Task<EventRecord> TryRecordEventAsync(string eventId, string eventType, CancellationToken ct);
    /// <summary>Compensation for a failed enqueue after a successful checkout RPC. Swallows and logs failures.</summary>
    Task DeleteEventAsync(string eventId, CancellationToken ct);
    /// <summary>process_checkout_completed: one atomic call. Throws on transport failure so the caller can ask Stripe to retry.</summary>
    Task<CheckoutOutcome> ProcessCheckoutCompletedAsync(
        string eventId, string eventType, string sessionId, string? paymentIntentId,
        string generationId, int tier, long amount, CancellationToken ct);
    /// <summary>Status overwrite on every transaction matching key = value. Returns the first row's generation_id when asked. Swallows and logs failures.</summary>
    Task<string?> UpdateTransactionsAsync(TransactionKey key, string value, string status, string eventId, bool returnGenerationId, CancellationToken ct);
    /// <summary>generations: status='failed' unless already 'success'. Swallows and logs failures.</summary>
    Task CancelUndeliveredGenerationAsync(string generationId, string reason, CancellationToken ct);
    /// <summary>Newest 'completed' transaction for the generation, or null. Throws on transport failure.</summary>
    Task<EligibleTransaction?> FindCompletedTransactionAsync(string generationId, CancellationToken ct);
    /// <summary>CAS completed → refund_pending; false when zero rows matched. Throws on transport failure.</summary>
    Task<bool> TryClaimForRefundAsync(string transactionId, CancellationToken ct);
    /// <summary>refund_pending → completed after a failed Stripe call. Swallows and logs failures.</summary>
    Task RevertRefundClaimAsync(string transactionId, CancellationToken ct);
}
```

- [ ] **Step 2: `SupabaseBillingStore`** — cut-and-paste of `TryRecordEventAsync`, `DeleteStripeEventAsync`, `ProcessCheckoutCompletedRpcAsync`, `UpdateTransactionByFilterAsync`, `CancelUndeliveredGenerationAsync` from the handler and `FindCompletedTransactionAsync`, `TryClaimForRefundAsync`, `RevertClaimAsync` from the refund service, with the same `HttpClientName = "SupabaseAdmin"`, the same headers, the same `LoggerMessage`s (move them too). `TransactionKey` renders to the PostgREST filter it replaces (`stripe_session_id=eq.{value}` etc.). Constructor `(IHttpClientFactory, IConfiguration, ILogger<SupabaseBillingStore>)`; `ResolveSupabase()` still returns null when unconfigured; in that case `TryRecordEventAsync` returns **`New`** (today's "unconfigured = no idempotency log, proceed" semantics) and the write helpers no-op. Task 4's DI never registers this store unconfigured, so this is belt-and-braces. Also delete the handler's private `EventRecord` enum and rename its `CheckoutRpcOutcome` to the shared `CheckoutOutcome`.

- [ ] **Step 3: Refactor the two callers**

`StripeWebhookHandler(IGenerationOrchestrator orchestrator, IBillingStore? billing, IEmailService emailService, ILogger<StripeWebhookHandler> logger)`; every `if (ResolveSupabase() is { } sb)` becomes `if (billing is not null)`, each helper call becomes the interface call. `HandleCheckoutCompletedAsync` keeps its exact control flow (RPC → duplicate short-circuit → enqueue → compensating delete → receipt email). Delete `SupabaseAdmin`, `ResolveSupabase`, `HttpClientName`, the HTTP-only `LoggerMessage`s that moved.

`StripeRefundService(IBillingStore? billing, IConfiguration config, RefundService stripeRefunds, ILogger<StripeRefundService> logger)`; `if (billing is null) { LogSupabaseNotConfigured(...); return RefundOutcome.NotEligible; }` (rename the message to "store not configured").

`Program.cs`: the handler and refund service now take `IBillingStore?`, which .NET DI won't inject as optional. Register them with factories:

```csharp
builder.Services.AddSingleton<IStripeWebhookHandler>(sp => new StripeWebhookHandler(
    sp.GetRequiredService<IGenerationOrchestrator>(), sp.GetService<IBillingStore>(),
    sp.GetRequiredService<IEmailService>(), sp.GetRequiredService<ILogger<StripeWebhookHandler>>()));
builder.Services.AddSingleton<IRefundService>(sp => new StripeRefundService(
    sp.GetService<IBillingStore>(), sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<RefundService>(), sp.GetRequiredService<ILogger<StripeRefundService>>()));
```

and complete the Task 4 selection block with the two `IBillingStore` registrations shown there. Remove `AddHttpClient(StripeWebhookHandler.HttpClientName)` / `AddHttpClient(StripeRefundService.HttpClientName)` in favour of `AddHttpClient(SupabaseBillingStore.HttpClientName)` (same name string, so one registration).

- [ ] **Step 4: Existing tests: construction only**

`StripeWebhookTests.BuildSut`: `factory.CreateClient(SupabaseBillingStore.HttpClientName).Returns(httpClient); var billing = new SupabaseBillingStore(factory, Config(), NullLogger<SupabaseBillingStore>.Instance); var sut = new StripeWebhookHandler(orchestrator, billing, email, NullLogger<StripeWebhookHandler>.Instance);`. Same in `StripeRefundServiceTests.BuildSut`. **Every assertion in both files stays as written**: the HTTP sequence is unchanged, which is the point of moving the code rather than rewriting it.

```bash
dotnet test src/StackAlchemist.Engine.Tests --filter "StripeWebhookTests|StripeRefundServiceTests"
```

Expected: PASS with no assertion edits. If one needs editing, the move changed behaviour; fix the store, not the test.

- [ ] **Step 5: Failing Postgres tests** — `Services/PostgresBillingStoreTests.cs`, `[Collection(PostgresCollection.Name)]`:

1. `TryRecordEventAsync` twice with one id → `New` then `Duplicate`.
2. **Replay:** `ProcessCheckoutCompletedAsync` twice with the same `eventId` for a seeded tier-0 generation → first `IsNew=true` with the row's `mode/prompt/project_type`, second `IsNew=false`; exactly **one** `transactions` row exists, `status='completed'`, `tier` on the generation is now the paid tier.
3. `UpdateTransactionsAsync(StripePaymentIntent, ..., "refunded", returnGenerationId: true)` → returns the linked generation id; `CancelUndeliveredGenerationAsync` flips it to `failed`; a `success` generation is left alone.
4. `FindCompletedTransactionAsync` → newest completed; `TryClaimForRefundAsync` → true once, false the second time; `RevertRefundClaimAsync` → back to `completed` and claimable again.
5. `DeleteEventAsync` after a checkout → the same event replays as `IsNew=true` (the compensation path).

- [ ] **Step 6: `PostgresBillingStore`**

`(NpgsqlDataSource dataSource, ILogger<PostgresBillingStore> logger)`. SQL:

- record: `insert into stackalchemist.stripe_events (id, type) values ($1, $2) on conflict (id) do nothing` → `rowsAffected == 1 ? New : Duplicate`; `NpgsqlException` → `Unavailable` (logged).
- delete: `delete from stackalchemist.stripe_events where id = $1`.
- checkout: `select is_new, mode, prompt, project_type, schema_json, personalization_json from stackalchemist.process_checkout_completed($1,$2,$3,$4,$5::uuid,$6,$7)`; map with `DeliveryJson.Read`. Let exceptions propagate (the handler returns `Retry: true`).
- transactions update: `update stackalchemist.transactions set status = $1, last_stripe_event_id = $2, updated_at = now() where <column> = $3 returning generation_id`, column chosen by a `switch` on `TransactionKey` (never string-interpolated from input).
- cancel: `update stackalchemist.generations set status = 'failed', error_message = $2 where id = $1::uuid and status <> 'success'`.
- find: `select id, stripe_payment_intent from stackalchemist.transactions where generation_id = $1::uuid and status = 'completed' order by created_at desc limit 1` (null when `stripe_payment_intent` is null, as today).
- claim: `update ... set status = 'refund_pending', updated_at = now() where id = $1::uuid and status = 'completed'` → `rowsAffected == 1`; revert is the inverse guarded on `refund_pending`.

Run → PASS. Full suite: `dotnet test src/StackAlchemist.Engine.Tests`.

- [ ] **Step 7: Commit**

```bash
git add src/StackAlchemist.Engine src/StackAlchemist.Engine.Tests
git commit -m "refactor(engine): IBillingStore seam for Stripe paths; Supabase impl moved, Postgres impl added"
```

**Review amendments (2026-09-30, Task 6 spec review + Opus quality review):**

- Register `SupabaseBillingStore` only when BOTH `Supabase:Url` and `Supabase:ServiceRoleKey` are set. A key without a URL would register a store whose checkout RPC always throws, turning every paid checkout into a Stripe retry where today it enqueues directly. Matches the old `ResolveSupabase()` semantics.
- `PostgresBillingStore.ProcessCheckoutCompletedAsync` MUST run the function and map the row inside ONE explicit transaction on ONE connection (`OpenConnectionAsync` → `BeginTransactionAsync` → `new NpgsqlCommand(sql, conn, tx)`, never the data-source `CreateCommand` helper), close the reader before `CommitAsync(CancellationToken.None)`, and let disposal roll back on a mapping failure. As an autocommit statement the server commits at Sync, so a `JsonException` in the mapping left the event recorded and the paid generation was never enqueued (every redelivery saw `is_new = false`). Realistic: `SchemaEntity.Name` / `SchemaField.Type` are `required` and `schema_json` can come from the client. Test: bad `schema_json` → throws, zero `stripe_events` / `transactions` rows, tier unchanged; fix the JSON and replay the SAME event → `IsNew`. The Supabase RPC commits server-side and cannot be fixed this way: issue filed for the legacy path (delete the event after a 2xx whose body fails to map). Never add a generic "delete the event on any checkout exception" in the handler: delivery A's failed RPC deleting delivery B's committed event yields two generations for one payment.
- Postgres throwing methods (checkout, find, claim) rethrow `InvalidOperationException` carrying operation + SQLSTATE + constraint (or the JSON path) and NO `InnerException`: the two catch sites (EventIds 310, 806) log the exception whole and Npgsql messages echo values (22P02 input echo, `Key(...)=(...)` detail). Connection errors and timeouts propagate unchanged. Add a typed `BillingStoreException { SqlState, IsTransient }` only when a caller needs to branch (none does).
- Compensations (`DeleteEventAsync` after a failed enqueue, `RevertRefundClaimAsync` after a failed Stripe call) take `CancellationToken.None`: with an already-cancelled request token they threw immediately and lost the checkout / stranded the row in `refund_pending`. Intentional behaviour change on BOTH paths, listed in the PR.
- Tests in the shared container use unique ids (`evt_<guid>`), tier-0 seeds carry a `user_id` (the free-quota trigger rejects ownerless tier-0 rows), the replay test replays with DIFFERENT tier/amount/payment intent so a tier update that re-ran would be caught, a `Task.WhenAll` of ~5 claims proves exactly one wins, and the handler with `billing: null` gets its own test.
- Accepted divergences to list in the PR: a non-uuid id on Postgres `FindCompletedTransactionAsync` → null (`NotEligible`, 802) where Supabase threw on the 400 (`Failed`, 806); Postgres `TryClaimForRefundAsync` throws on failure (`Failed`, 806) where Supabase returned false on any non-2xx (`NotEligible`, misleading 803); Postgres `UpdateTransactionsAsync` returns the first non-null `generation_id`, Supabase the first row's.
- `SupabaseBillingStore`'s unconfigured guards are unreachable through DI after the both-halves registration; kept as part of the verbatim move, no test file.
- Follow-up issues (filed at Task 9): ambiguous claim commit leaves a row in `refund_pending` with no MANUAL RECOVERY breadcrumb; nothing writes `stripe_charge_id`, so `charge.dispute.created` never matches a row; the handler's `Guid.NewGuid()` fallback for missing metadata hits the `transactions.generation_id` FK (23503) and is retried for 3 days (#419).

### Task 7: Deploy plumbing (inert until the secret exists)

- [ ] **Step 0: three follow-ups from the Task 3 quality review** (web + `ci.yml`, both touched here anyway): (a) `drizzle-store.ts`: detect a Postgres error with `cause instanceof postgres.PostgresError` (exported by postgres-js), not `cause.name === "PostgresError"`: Next bundles `postgres` into the server build and the minifier may rename the class, which would silently drop the quota trigger's text from prod logs. (b) `ci.yml` drift guard: `out=$(npm run db:generate -- --name ci-drift-check 2>&1) || { echo "$out"; exit 1; }` so a non-zero drizzle-kit exit still prints its output under `bash -e`. (c) `drizzle-store.integration.test.ts` quota-predicate test: after asserting the count is 3, insert two more tier-0 rows (both succeed) and assert the third is rejected, so a trigger that ignored one exclusion (count 4) would fail the test.

**Files:**
- Modify: `.github/actions/setup-env/action.yml`, `docker-compose.prod.yml`, `.github/workflows/deploy-prod.yml`
- Create: `docs/runbooks/qavren-db-migrations.md`

- [ ] **Step 1: `setup-env`** — new optional input and `.env` line

```yaml
  database_url:
    description: 'qavren-db pooler URL (postgres://...?sslmode=require). Empty = Supabase mode.'
    required: false
    default: ''
```

env: `DATABASE_URL: ${{ inputs.database_url }}`; heredoc line `DATABASE_URL=${DATABASE_URL}` next to the Supabase lines.

- [ ] **Step 2: `docker-compose.prod.yml`** — pass-through on both services

Under `sa-web.environment` and `sa-engine.environment`:

```yaml
      # qavren-db (phase B). Empty keeps every Supabase path; set in phase E.
      DATABASE_URL: ${DATABASE_URL:-}
```

- [ ] **Step 3: `deploy-prod.yml`**

`Setup .env (prod)` gets `database_url: ${{ secrets.DATABASE_URL }}` (the `Prod` environment secret; empty until phase A task 4 is done). Add a step right after the existing Supabase migration step:

```yaml
      - name: Apply qavren-db migrations (prod)
        # Phase B of the re-platform. Gated on the session-mode secret the same
        # way the Supabase step is gated: absent = skip with a warning. Runs the
        # web app's own migrator, never drizzle-kit migrate/push (42501 on
        # qavren-db). See docs/runbooks/qavren-db-migrations.md.
        if: ${{ secrets.DATABASE_URL_MIGRATE != '' }}
        shell: bash
        working-directory: src/StackAlchemist.Web
        env:
          DATABASE_URL_MIGRATE: ${{ secrets.DATABASE_URL_MIGRATE }}
        run: |
          set -Eeuo pipefail
          echo "::add-mask::${DATABASE_URL_MIGRATE}"
          npm ci --ignore-scripts
          npm run db:migrate
```

An unset GitHub secret expands to the empty string, so the migrator's `DATABASE_URL_MIGRATE ?? DATABASE_URL` fallback never triggers in CI; that is fine here because the step is gated on the secret being non-empty. `if:` cannot read `secrets` directly on a step in every runner version; if the linter (`actionlint` in the `Analyze (actions)` CodeQL job) rejects it, use the env-var pattern the Supabase step uses (`if [ -z "$DATABASE_URL_MIGRATE" ]; then echo "::warning::..."; exit 0; fi`). Needs `actions/setup-node` before it (the deploy runner is the EC2 self-hosted box; check Node is present there, else add the setup step with `node-version: 24`).

- [ ] **Step 4: Runbook** `docs/runbooks/qavren-db-migrations.md`: where the schema lives (qavren-db, schema `stackalchemist`, role of the same name), the two URLs and which is for what, `npm run db:generate` / `db:migrate` / `db:check`, the CI drift guard, the prod step and its gate, the local container recipe, and the traps: never `drizzle-kit migrate`/`push`; never log a postgres-js error object whole (the DSN rides on `err.input`); the whole migrator run is ONE transaction, so a migration can't use `CREATE INDEX CONCURRENTLY` or add an enum value and use it in the same file; never edit an applied migration (the ledger hash check fails the run). Also file a follow-up issue: `process_checkout_completed` has a dead "generation row missing" branch inherited from the legacy function; the FK on `transactions.generation_id` raises 23503 first, rolling back the `stripe_events` insert so Stripe retries forever. Faithful port, out of scope here.

- [ ] **Step 5: Commit**

```bash
git add .github docker-compose.prod.yml docs/runbooks/qavren-db-migrations.md
git commit -m "ci(deploy): DATABASE_URL pass-through and a gated qavren-db migrate step"
```

**Review amendments (2026-09-30, Task 7):**

- Step 0 (c) wording corrected: after the count of 3, insert two more ORDINARY COUNTED tier-0 rows (both must succeed, count 5), then assert the next insert is rejected. Rows that do not count could never reach the limit; the proof is that a trigger ignoring one exclusion (e.g. dropping `status <> 'failed'`) reaches the limit early and fails the test (verified by loading such a trigger into the local container).
- Engine lock-in test for the uncancelled compensation: a pre-cancelled token proves nothing because the fake transport ignores cancellation. The test cancels a `CancellationTokenSource` INSIDE the throwing `EnqueueAsync` mock (so the RPC ran on a live token) and asserts the compensating `DELETE` still goes out; `RecordingHttpHandler.SendAsync` gained `cancellationToken.ThrowIfCancellationRequested()` so the transport honours cancellation. Mutating `DeleteEventAsync(..., ct)` back makes it fail ("expected 2 requests, found 1").
- `deploy-prod.yml`: step-level `if` cannot read `secrets`; the migrate step keeps the bash env-var gate (skip + warning + step summary in Supabase mode). Node is NOT set up in this workflow today, so a `actions/setup-node@v7` step was added for the migrator, gated by a JOB-level boolean `QAVREN_DB_MIGRATE_ENABLED: ${{ secrets.DATABASE_URL_MIGRATE != '' }}` (job `env` may read `secrets`; only the boolean is exposed) so a toolcache/download failure on the ARM64 runner cannot block a Supabase-mode deploy. `npm ci --omit=dev --ignore-scripts` is enough: `drizzle-orm` and `postgres` are runtime deps and the migrator imports nothing else (verified in a scratch copy). Until phase E the skip is advisory; at phase E mirror the Supabase step's hard-fail when the push changes `src/StackAlchemist.Web/drizzle/`.
- Node version: `ci.yml` pins `NODE_VERSION: "24"` while `.nvmrc` says `26.3.0`; the deploy workflow mirrors CI (24). Someone should pick one (out of scope here).
- Runbook: a raw `$` in a DSN is interpolated when Compose reads `.env`; percent-encode `$` `#` `@` (`%24` `%23` `%40`) in both URL secrets. The migrator prints "not a valid postgres URL (percent-encode the password)" when a raw one slips through.
- The follow-up issue the plan asked for already existed as #419; the runbook references it. Phase B follow-ups filed 2026-09-30: #422 (Supabase 2xx body fails to map → paid generation lost), #423 (`stripe_charge_id` never written), #424 (ambiguous refund claim strands `refund_pending`), #425 (unparseable stale row never failed), #426 (Worker host lacks `IPendingWriteBuffer`).

### Task 8: Real-target smoke against `qavren-db-test` (Toby, not a subagent)

The vault file from phase A holds the test URLs. Nothing here prints them.

- [ ] **Step 1: Apply the migrations to qavren-db-test** (session URL)

```bash
V=~/stackalchemist-credentials-vault-20260929/provision-test.txt
export DATABASE_URL_MIGRATE="$(grep -oP '^session_url=\K.*' "$V")?sslmode=require"
npm run db:migrate            # expect: migrations applied (2)
npm run db:migrate            # expect: already up to date
```

- [ ] **Step 2: Run the isolation suite through the transaction pooler** (the `prepare: false` path)

```bash
export TEST_DATABASE_URL="$(grep -oP '^pooler_url=\K.*' "$V")?sslmode=require"
npx vitest run __tests__/data/drizzle-store.integration.test.ts
```

Expected: 8 passed. This is the only place the pooler is exercised before prod.

- [ ] **Step 3: Engine against the pooler, once**

`PostgresDeliveryServiceTests` run on Testcontainers by design. For the pooler, run a one-off: `DATABASE_URL="$(pooler url)" dotnet run --project src/StackAlchemist.Engine` locally, hit `GET /health`, and watch the log for the Npgsql connection. `sslmode=require` cannot fail on certificate validation (decision 8, corrected): what this proves is that the converter's output connects through Supavisor with `MaxAutoPrepare=0` / `NoResetOnClose=true` and that Npgsql's logs reach the host logger (`AddNpgsqlDataSource`).

- [ ] **Step 4: Record results in this file** (a dated line under this task) and `unset` the URLs.

- 2026-09-30 — Steps 1–2 done against `qavren-db-test`: `migrations applied (2)`, re-run `no pending migrations (ledger up to date)`; isolation suite through the transaction pooler (:6543, `sslmode=require`): 14 passed in 5.6 s (the "8 passed" above predates Task 3's final suite); schema check via catalog join (the role's `search_path` is `stackalchemist, extensions`, so `regclass::text` drops the schema prefix — join `pg_namespace` instead): 4 tables empty, 5 functions, 2 triggers, 13 indexes. Step 3 pending: `/health` has NO database check registered (`AddHealthChecks()` bare), so the proof is the reconciler's immediate startup sweep in the Npgsql debug log, not the health endpoint.
- 2026-09-30 — Step 3 done: Engine built from `d78389a2` (Development, `DATABASE_URL` = pooler URL with `sslmode=require`, no Supabase vars) boots in Postgres mode; `GET /health` → 200 Healthy, `GET /healthz` → 200. Npgsql logged the reconciler's startup sweep completing against the pooler (`Command execution completed (duration=38ms): select ... from stackalchemist.generations where status in (...) and updated_at < now() - $1`, zero rows), which is the proof the converter's output connects through Supavisor with `Max Auto Prepare=0`. No errors in 60 log lines. URLs unset afterwards. Task 8 complete.

### Task 9: PR, review, merge, verify the deploy was a no-op

- [ ] **Step 1: Open the PR** from `feat/qavren-db-data` with the task list, the decisions above, and the smoke results. Title: `feat: dual-mode data layer over qavren-db (re-platform phase B)`.
- [ ] **Step 2: Opus-tier review** (superpowers:requesting-code-review). Blocking classes: any user-facing string change in `actions.ts`; any test assertion edit in the two Stripe test files; any SQL built from string interpolation of input; a missing `user_id` predicate on a list/stat/retry query.
- [ ] **Step 3: Merge** (standing StackAlchemist authorization: approve verdict + green CI) via the GitHub MCP merge tool.
- [ ] **Step 4: Watch `deploy-prod`.** Expected: green, and prod still in Supabase mode. Verify: `curl -sf https://stackalchemist.app/api/healthz`; sign in on prod and run one Tier 0 generation end to end (submit → status ticks → preview renders). The Engine log should show `Supabase updated: generation ...` lines, not Postgres ones.
- [ ] **Step 5: Vault + memory:** append the phase B section to `Projects/stack-alchemist/stack-alchemist.md`; update the StackAlchemist memory note.

- 2026-09-30 — Task 9 done. PR #427 opened from `feat/qavren-db-data` (20 commits, 0 behind `origin/main`); CI green on `f505e6a3` and again on `459dc4a4` (final-review follow-ups: unread `ConnectionStrings:Db` entry dropped, whitespace `DATABASE_URL` trimmed, two runbook corrections). Final Opus review: Task 7 APPROVED, whole branch MERGE-READY — blocking classes B1–B5 all PASS, `--locked-mode` restore clean, 121 targeted Engine tests, 360 web tests incl. 14 against real Postgres; it also found the "only two Supabase-path changes" claim false — SEVEN deliberate ones are listed in the PR body (add: stale sweep survives a bad row, `retryGeneration` CAS, pageSize clamp + `id desc` tiebreak, UUID pre-guards, log wording). Squash-merged as `7cdaff6f`. `deploy-prod` run 36689811457: success; `Set up Node.js (qavren-db migrator)` SKIPPED via the job-level boolean, `Apply qavren-db migrations (prod)` took the skip-with-warning path, `/api/healthz` 200 — prod unchanged, Supabase mode (secrets verified absent on repo + `prod` environment). Follow-ups #422–#426 filed, #419 commented. Phase E notes from the review: once `DATABASE_URL_MIGRATE` exists a qavren-db outage blocks every deploy even in Supabase mode (deliberate; say so where the secret is added); check the prod runner's process environment for a stray `DATABASE_URL` (Compose prefers it over `.env`); consider a lean install instead of `npm ci --omit=dev`.

## Exit criteria

- [x] Web and Engine each have a Postgres implementation selected by `DATABASE_URL`, with the Supabase implementation still the default and byte-for-byte unchanged in behaviour
- [x] Two-user isolation suite (web) and the checkout replay + refund CAS suite (Engine) run on real Postgres in CI on every PR
- [x] Migrations applied to `qavren-db-test`; the isolation suite passed through the transaction pooler
- [x] `deploy-prod` after merge: green, prod unchanged (Supabase mode)
- [x] Open question filed for Steve: should `/generate/[id]` become owner-only (decision 3)? Answered 2026-09-30: yes.

Phase C (auth) can start once the first two lines are true.

