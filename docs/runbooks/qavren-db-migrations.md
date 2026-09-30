# qavren-db Migrations Runbook

StackAlchemist is moving its data from Supabase to qavren-db (the shared
Postgres). Schema changes are written with Drizzle and applied by the web app's
own migrator. This document covers where things live, the two connection URLs,
the CI drift guard, the prod deploy step and the traps. The Supabase side is
still documented in `ci-supabase-migrations.md` and stays authoritative until
phase E flips prod to `DATABASE_URL`.

## Where the schema lives

- Database: qavren-db, schema `stackalchemist`.
- Login role: `stackalchemist` (same name as the schema). It owns the schema and
  everything in it but has **no CREATE on the database**. That is why the stock
  `drizzle-kit migrate` / `push` fail with `42501` (see Traps).
- Source of truth: `src/StackAlchemist.Web/src/db/schema.ts`. Generated SQL and
  the journal live in `src/StackAlchemist.Web/drizzle/`. Hand-written pieces
  (triggers, functions such as `process_checkout_completed`) live in
  `drizzle/0001_functions.sql`.
- Migration ledger: `stackalchemist.__drizzle_migrations`.

## The two URLs

| Secret (prod environment) | Port / mode | Used for |
|---|---|---|
| `DATABASE_URL` | `:6543`, Supavisor **transaction** pooler | Runtime: passed to `sa-web` and `sa-engine` through `.env` and `docker-compose.prod.yml`. Empty or unset = Supabase mode. |
| `DATABASE_URL_MIGRATE` | `:5432`, **session** mode | DDL only: read by `npm run db:migrate` in the deploy step. Never given to a container. |

Both are `postgres://...?sslmode=require`. Percent-encode the password: a raw
`#` or `@` makes the URL unparseable, and a raw `$` is interpolated by
`docker compose` when it reads `.env`. Before setting **either** secret,
percent-encode `$` as `%24`, `#` as `%23` and `@` as `%40` in the password. The
migrator prints "not a valid postgres URL (percent-encode the password)" when a
raw one slips through. The same `$` hazard already applies to every other value
`setup-env` writes into `.env` (the Supabase-style lines and the rest); that is
pre-existing.

Transaction pooling rules for the runtime URL:

- Web: `src/db/index.ts` sets `prepare: false` on postgres-js.
- Engine: `PostgresUrl` sets `Max Auto Prepare=0` and `No Reset On Close`. It
  rejects any query parameter other than `sslmode`, `application_name`,
  `connect_timeout`, `options` and `sslrootcert`, so do not append pooler flags
  to the URL.

## Commands

Run from `src/StackAlchemist.Web`:

| Command | What it does |
|---|---|
| `npm run db:generate -- --name <what-changed>` | Diffs `schema.ts` against `drizzle/` and writes the next migration. Needs no database. |
| `npm run db:migrate` | Applies pending migrations (`scripts/migrate.mjs`). Uses `DATABASE_URL_MIGRATE` whenever that variable is defined (even as an empty string, which then fails the run); only an undefined variable falls back to `DATABASE_URL`. |
| `npm run db:check` | `drizzle-kit check`: verifies the migration folder is internally consistent (no colliding snapshots). |

Workflow for a schema change: edit `schema.ts`, run `db:generate`, read the SQL
it wrote, commit `schema.ts` and the new `drizzle/` files together, run
`db:migrate` against a local database, and run the integration test:

```
TEST_DATABASE_URL=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist_test npx vitest run __tests__/data/drizzle-store.integration.test.ts
```

## CI drift guard

`ci.yml`, job `Frontend (Lint, Typecheck, Unit Tests)`, step "Migration drift
guard". It fails the build when `schema.ts` and `drizzle/` disagree, so a schema
edit without a generated migration cannot merge and then surface for the first
time when prod's migrate step runs it.

It runs `db:generate` (throwaway name `ci-drift-check`) and requires **both**:

1. the output contains the literal `No schema changes`, and
2. `git status --porcelain drizzle/` is empty (nothing was written).

Then it runs `db:check`. The literal grep exists because a renamed column or
table makes drizzle-kit ask an interactive question; on a non-TTY it prints
"Interactive prompts require a TTY", writes nothing and **exits 0**. A clean
`git status` alone would pass that case. If `db:generate` itself exits non-zero,
the step prints its output and fails.

Fix a red guard locally: `npm run db:generate -- --name <what-changed>` and
commit the result.

## Prod deploy step

`deploy-prod.yml` has a `Set up Node.js (qavren-db migrator)` step and an
`Apply qavren-db migrations (prod)` step, placed right after the Supabase
migration step and before the image build (so new code never goes live against
an older schema, and a failed migration aborts the deploy while the old stack is
still serving).

The migrate step is gated on the `DATABASE_URL_MIGRATE` secret the same way the
Supabase step is gated on `PROD_SUPABASE_DB_URL`:

- **Secret absent:** warning, a "NOT applied" line in the job summary, exit 0.
  This is the state until phase E; prod runs in Supabase mode. The Node setup
  step is skipped too (job-level `QAVREN_DB_MIGRATE_ENABLED`, a boolean derived
  from the secret), so a toolcache or download failure cannot block a deploy.
- **Secret present:** `npm ci --omit=dev --ignore-scripts` (the migrator needs
  only `drizzle-orm` and `postgres`), then `npm run db:migrate`. Any failure
  fails the deploy.

The skip has a drift guard since phase E (2026-09-30), mirroring the Supabase
step's: with the secret unset, a `push` whose `git diff --name-only $BEFORE $SHA`
touches `src/StackAlchemist.Web/drizzle/` fails the deploy (`::error` plus a
"Deploy blocked" summary line) instead of shipping code against a schema the
workflow cannot update. Pushes without migration changes still skip with the
warning, so Supabase mode and a rollback to it keep deploying. The same PR added
a first-step **preflight** over the four re-platform secrets (`DATABASE_URL`
without `DATABASE_URL_MIGRATE` is an error; the reverse is a warning) and an
end-of-run **mode check**; both are described in
`docs/runbooks/qavren-cutover-phase-e.md`.

`DATABASE_URL` (runtime) flows through the `setup-env` action's `database_url`
input into `.env`, then into both containers. Nothing reads it until the secret
exists.

## Local database recipe

```
docker run -d --name sa-pg -p 55440:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=stackalchemist_test postgres:17-alpine
DATABASE_URL_MIGRATE=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist_test npm run db:migrate
```

Run the second line from `src/StackAlchemist.Web`. In PowerShell set
`$env:DATABASE_URL_MIGRATE = '...'` first. Re-running is a no-op ("no pending
migrations"). The credentials are throwaway local ones.

## Traps

- **Never run `drizzle-kit migrate` or `drizzle-kit push`** against qavren-db.
  Both open with `CREATE SCHEMA IF NOT EXISTS`, which fails with `42501` for a
  role without CREATE on the database. Only `drizzle-kit generate` (no database)
  and `check` are safe. `npm run db:migrate` is the only applier.
- **Never log a postgres-js error object whole.** The DSN, password included,
  rides on `err.input`. Log named fields (`code`, `message`, `detail`, `hint`)
  as `migrate.mjs` does.
- **The whole migrator run is ONE transaction** (under an advisory lock), and
  every pending migration in a run shares it. `CREATE INDEX CONCURRENTLY` can
  therefore never run through this migrator, and an enum `ADD VALUE` and its
  first use must ship in separate deploys (separate migrator runs), not merely
  in separate files.
- **Never edit an applied migration.** The migrator compares the stored hash of
  every applied file and fails the run on a mismatch. Write a new migration
  instead. A new migration whose timestamp is older than the newest applied one
  is also rejected: renumber it.
- **Statements split on `--> statement-breakpoint`, not `;`.** Hand-written
  files with function bodies must keep those markers between statements.
- **The ledger is `stackalchemist.__drizzle_migrations`,** not drizzle's default
  `drizzle.__drizzle_migrations`. Do not seed or repair it from a tool that
  assumes the default location.

## Known issues

- **#419** - the "generation row missing" branch in `process_checkout_completed`
  is dead code: the foreign key on `transactions.generation_id` raises `23503`
  before the branch can run, so a checkout for a missing generation makes the
  RPC fail and Stripe retries the webhook forever. Tracked in #419; not fixed
  here.
