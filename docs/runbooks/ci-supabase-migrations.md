# CI Supabase Migrations Runbook

The E2E Integration job (`.github/workflows/ci.yml`, job `e2e-integration`)
applies `supabase/migrations/*.sql` to the CI-only Supabase project on every
run before the engine starts. This document covers the secret it depends on
and the failure modes you'll see.

## Secret: `CI_SUPABASE_DB_URL`

A Postgres connection string for the CI-only Supabase project (ref:
`cdlefpvsvyepofsboepc`). Stored in the **Test** GitHub environment so the
`e2e-integration` job (`environment: test`) sees it but other jobs do not.

### How to get the value

1. Open the Supabase dashboard for project `cdlefpvsvyepofsboepc`.
2. **Project Settings → Database → Connection string → URI tab**.
3. Copy the **Transaction Pooler** URL (port 6543) for the CLI — it survives
   IP rotation and Supabase restarts. The Direct Connection URL works too
   but is less robust on shared runners.
4. Replace `[YOUR-PASSWORD]` with the database password set when the project
   was created.
5. Final value looks like:
   `postgres://postgres.cdlefpvsvyepofsboepc:<PASSWORD>@aws-0-us-east-1.pooler.supabase.com:6543/postgres`

### How to set it

1. **GitHub repo → Settings → Environments → test → Environment secrets**.
2. **Add secret** `CI_SUPABASE_DB_URL`, paste the value, save.
3. Trigger the `CI` workflow via `workflow_dispatch` to verify the
   "Apply Supabase migrations to CI project" step succeeds.

### Rotation

If the DB password is rotated, update this secret to match. The CI job
fails closed with `::error::CI_SUPABASE_DB_URL secret is not set` if the
secret is missing.

## Failure modes

| Symptom in `Apply Supabase migrations to CI project` step | Likely cause |
|---|---|
| `password authentication failed for user "postgres..."` | DB password rotated; update the secret |
| `connection refused` / `timeout` | Project paused (free tier auto-pause); open the dashboard to wake it |
| `relation "supabase_migrations.schema_migrations" already exists` (warning, not error) | Normal — re-run is idempotent |
| `migration X.sql already applied` (info) | Normal |
| `must be owner of table` | Using anon/service-role JWT instead of postgres password — switch to the URI from Database settings, not the API tab |
| `migration X.sql failed: <SQL error>` | The migration file has a SQL error or conflicts with state already applied on the CI project. Reproduce locally with `supabase db push --db-url $CI_SUPABASE_DB_URL` to see the full error. Fix the migration and push again. If the CI project's state has drifted irrecoverably, the project is CI-only with no production data — delete and recreate it via the Supabase dashboard, then re-run CI to apply all migrations from scratch. |

## Why a brand-new project per environment

We have three Supabase projects: `dev`, `prod`, and this CI-only one. CI runs
write generated rows, schema_json, and build logs we don't want polluting
prod data and don't want hitting dev rate limits during nightly E2E.

## Related

- Issue #92 — original error context
- PR that wired this — `ci(supabase): apply migrations to CI project before E2E`
