### Software Design Document (SDD): StackAlchemist

> Status (2026-10-03): describes the system as deployed. Prod runs the V1 one-shot generation path on Claude Sonnet 5.5, with data in qavren-db (Postgres) and sign-in through Qavren Auth (Keycloak). Supabase is legacy and rollback-only; phase F deletes the remaining Supabase code (`docs/superpowers/plans/2026-10-03-qavren-replatform-F-retire.md`).

**1. System Architecture**
* **Frontend and API Gateway:** Next.js 16 (App Router, React 19, Tailwind CSS 4, `@xyflow/react`). Server Actions handle intake and checkout and call the Engine with an `X-Engine-Key` header.
* **Identity:** Qavren Auth. Keycloak realm `stackalchemist` at `auth.stackalchemist.app`, reached through Auth.js v5 (`next-auth` beta + `@qavren/auth-next`). Email/password and Google. Keycloak hosts sign-in, registration, email verification and password reset; the app's `/login` is a one-button hand-off. JWT session with a fixed 7-day lifetime and RP-initiated logout.
* **State:** qavren-db, the shared Qavren Postgres (Supabase-hosted, reached through the Supavisor pooler), schema `stackalchemist`, with its own owning role. Tables: `profiles`, `generations`, `transactions`, `stripe_events`. Web uses Drizzle over postgres-js; the Engine uses Npgsql. There is no RLS: every query is owner-scoped in code and proven by a two-user integration suite (see [Database ERD](Database%20ERD.md)).
* **Generation Engine:** .NET 10 Web API. The orchestrator and the in-process Compile Guarantee worker (`CompileWorkerService`, a `BackgroundService` fed by a `Channel`) run in the same process. The separate `StackAlchemist.Worker` project exists for scale-out but is not deployed.
* **LLM:** Claude Sonnet 5.5 (`claude-sonnet-5-5`) by default, set by `ANTHROPIC_MODEL` (GitHub `vars.ANTHROPIC_MODEL` in CI/deploy; code default `AnthropicDefaults.ModelId`). Adaptive thinking with `output_config.effort` = `medium` (`ANTHROPIC_EFFORT`), `max_tokens` 20,000, server-side refusal fallback; refusals surface as a clear failure message. A mock fallback is used when no Anthropic key is configured. BYOK models (dashboard API settings): Claude Sonnet 5.5, Claude Opus 5.5, Claude Haiku 4.5, OpenAI `gpt-6.1-sol`, OpenRouter `anthropic/claude-sonnet-5.5`. Keys are AES-256-GCM encrypted with `BYOK_ENCRYPTION_KEY` in the web app and decrypted only in the Engine; a key is never sent to a different vendor.
* **Storage:** Cloudflare R2 with presigned download URLs (default expiry 168 h, `CloudflareR2:PresignedUrlExpiryHours`). Open issue #444: generated ZIPs are also reachable through a public R2 custom domain.
* **Email:** Resend (receipt, build ready, refund issued). Auth emails come from Keycloak.
* **Live status:** polling, no WebSockets or Realtime. Status pages poll the owner-scoped `getGeneration` server action every 3 s while the tab is visible (paused when hidden); the dashboard refreshes every 10 s while a build is in progress.

**2. Product Tiers**
* **Spark (Tier 0, free):** 5 builds per account per calendar month, enforced by a DB trigger. Output is a runnable Node demo previewed in StackBlitz; no download.
* **Blueprint (Tier 1, $299):** schema and API docs.
* **Boilerplate (Tier 2, $599):** full codebase ZIP.
* **Infrastructure (Tier 3, $999):** codebase plus AWS CDK (TypeScript, compiled with `tsc`), Helm chart, Terraform and a generated `DEPLOYMENT.md`.

Project types: DotNet + Next.js (default) and FastAPI (Python) + React.

**3. Environments and Delivery**
There is one deployed environment: prod. No staging or test site exists; the test mirror (`test.stackalchemist.app`, `deploy-test.yml`) was retired on 2026-10-03 (#211, PR #468). Local development uses a local Postgres and a local Keycloak realm (see [Dev Environment Setup](Dev%20Environment%20Setup.md)).

* **Host:** one AWS EC2 `t4g` (ARM64) instance running Docker Compose: `reverse-proxy` (nginx), `sa-web`, `sa-engine`, plus a standalone Cloudflare Tunnel container. A self-hosted GitHub Actions runner lives on the box; AWS access uses GitHub OIDC.
* **Containers:** a multi-stage, multi-target `Dockerfile` (`web`, `engine`, `worker` targets). Final stages include `wget` for Compose health checks.

**4. CI/CD Flow**
* **CI (`ci.yml`, on pull requests):** frontend (lint, `tsc`, vitest with coverage floors, dependency audit gate); backend (.NET build and test, template compile gates, Python 3.14); Docker build validation; E2E smoke (Playwright, demo mode). On main and nightly: an E2E integration lane against Postgres 17 and Keycloak containers, and a qavren-db pooler smoke.
* **Prod deploy (`deploy-prod.yml`):** every push to `main` deploys, except paths in the workflow's `paths-ignore` (docs, tests, CI-only workflows). Steps: secrets preflight, qavren-db migrate step (`DATABASE_URL_MIGRATE`), build, swap behind a maintenance page, health checks, auth-mode check.
* **Releases:** `release.yml` (git-cliff) updates `CHANGELOG.md` and creates a GitHub Release. Other workflows: `prod-healthwatch.yml`, `secret-scan.yml`, `tracker-guard.yml`. Dependabot is monthly and grouped.

**5. Core Workflows and Technical Pipelines**

**A. Dual Mode Intake Pipeline**
* **Simple Mode:** the user submits a text prompt. The `extractSchema` server action calls the Engine (`/api/extract-schema`), which asks the LLM for a structured JSON schema. The frontend renders it into an editable node-based UI.
* **Advanced Mode:** the user defines entities, target platform and endpoints directly in the node UI. The validated schema plus `project_type` go into the generation payload.
* **Personalization wizard:** business identity, palette, domain vocabulary and feature toggles feed templates and prompts.
* **Compile orchestration:** `CompileService` dispatches to per-platform build strategies (`IBuildStrategy`) so both project types share one retry loop and state machine.

**B. Generation Paths**
* **V1 one-shot (what prod runs):** one whole-codebase LLM call; the response is split on `[[FILE:...]]` markers and reconstructed into the template directory. `Generation:UseSwissCheese` is false outside Development.
* **V2 "Swiss Cheese" (Development only):** the Engine renders the master template, then injects LLM-generated code into per-zone placeholders in parallel. See `swiss-cheese-rollout.md` and `swiss-cheese-tuning.md`.
* **Templates:** `V0-Spark-NextJs`, `V1-DotNet-NextJs`, `V1-Python-React`, `V2-DotNet-NextJs`, `V2-Python-React`, `Tier3-Infrastructure`. Each template set with dependencies has a CI compile gate.

**C. The Compile Guarantee**
1. The in-process compile worker builds the reconstructed project for real: `dotnet build`; `npm ci`, typecheck, `npm run build`; for the Python half, pip install in a per-build venv, lint, pytest.
2. On success the project is packed and uploaded.
3. On failure the error output goes back to the LLM for a repair. Up to 3 repair attempts.
4. On final failure of a paid tier, the customer gets an automatic full Stripe refund (`StripeRefundService`) and an email.
5. Each archive ships `build-report.json`.

**D. Tier 3 IaC Export**
For Tier 3, Handlebars injects the user's environment variables and project naming into pre-written AWS CDK (TypeScript, compiled with `tsc`), Helm chart and Terraform. A generated `DEPLOYMENT.md` runbook lands in the archive root.

**E. Payments**
Stripe Checkout. The Engine creates the session (`/api/stripe/create-session`); Stripe posts the signature-verified webhook to the Engine (`/api/webhooks/stripe`). `stripe_events` provides idempotency, and `process_checkout_completed` applies the event, tier update and transaction upsert atomically.

**6. Data Schema**
* Tables `profiles`, `generations`, `transactions`, `stripe_events` in schema `stackalchemist`. Two Drizzle migrations in `src/StackAlchemist.Web/drizzle/` (`0000_init`, `0001_functions`), generated by `drizzle-kit generate` and applied by `db:migrate`.
* DB functions: `append_build_log`, `increment_token_usage`, `process_checkout_completed`, and the triggers `enforce_free_generation_quota` and `set_updated_at`.
* `generations` carries `schema_json`, `personalization_json`, `build_log`, `preview_files_json`, `project_type`, `download_url`, `status`, `attempt_count`, `input_tokens`, `output_tokens`, `model_used`, `error_category`, `user_id`.

**7. Security and Operations**
Rate limits on expensive endpoints; CORS locked to the frontend origin; `X-Engine-Key` service auth; prompt sanitization in web and Engine; CSP in Report-Only mode (enforcement pending, see [CSP Rollout Plan](CSP%20Rollout%20Plan.md)); `X-Correlation-Id`; OpenTelemetry generation meters; `/api/healthz` (web) and `/healthz` (Engine).
