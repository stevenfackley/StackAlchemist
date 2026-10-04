# StackAlchemist: Testing Strategy

> Updated 2026-10-03. Every concrete claim below describes what exists in the repo today. Aspirations are labelled as gaps in section 9.

This document defines how the StackAlchemist platform is tested across the Web app, the Engine and the compile worker, and where each layer runs in CI.

---

## 1. Testing Philosophy

StackAlchemist's core value proposition, a **compile-guaranteed generated repository**, demands a strong test suite. An untested generation pipeline is a broken product.

**Principles:**
- **Test the contract, not the implementation.** Mock at service boundaries, not inside services.
- **Canned LLM output over live LLM calls.** Real or realistic LLM responses saved as fixtures catch real parsing bugs without API cost.
- **Compile verification is a first-class test.** Each template set that has dependencies has a CI compile gate; if generated code does not build, the product is broken.
- **Real Postgres for data-access code.** Both the Web `DrizzleStore` and the Engine's Npgsql stores are tested against a real Postgres 17 running the shipped migrations.
- **No flaky tests in CI.** Multi-service E2E flows that flake belong in the non-blocking nightly suite, not in the PR gate.
- **Fail loudly.** A gate that skips because its toolchain is missing is treated as a failure on CI (see `IntegrationToolchain` in the Engine tests).

---

## 2. Test Pyramid Overview

```
         ╱ ╲
        ╱ E2E ╲          Playwright: smoke (PR gate, demo mode), integration (main/nightly), nightly
       ╱───────╲
      ╱ Integr. ╲        Real Postgres + Keycloak; Testcontainers Postgres; template compile gates
     ╱───────────╲
    ╱   Contract   ╲     LLM output fixtures, Stripe webhook handling, API route handlers
   ╱─────────────────╲
  ╱     Unit Tests     ╲  Vitest (Web) + xUnit (Engine, Worker)
 ╱─────────────────────────╲
```

---

## 3. Frontend Testing (Next.js 16 / TypeScript)

### 3.1 Frameworks & Tools

| Tool | Purpose |
|------|---------|
| **Vitest** (`^5`) | Unit and integration test runner |
| **React Testing Library** + **user-event** + **jest-dom** | Component rendering and interaction |
| **MSW** (`^2`) | API mocking at the network level (`__tests__/mocks/`) |
| **Playwright** (`^1.63`) | E2E browser tests |

### 3.2 Layout

Unit and integration tests live in `src/StackAlchemist.Web/__tests__/`, grouped by area:

| Folder | Covers |
|--------|--------|
| `components/` | SimpleMode, AdvancedWizard, PersonalizationModal, UpgradeModal, paid-tier panel, error boundaries, Modal/Alert and similar |
| `lib/` | Server actions (BYOK, checkout, demo mode, generation, retry, `getGeneration`), the status polling hook, build-report parsing, runtime config, content loaders, demo data |
| `auth/` | Auth.js config, callback and sign-out routes, session handling, login, register, forgot/reset password pages |
| `data/` | Data store selection and the `DrizzleStore` integration suite (section 5.2) |
| `db/` | Drizzle schema and client |
| `proxy/` | Route gating in the Next.js proxy |
| `api/` | Route handlers |
| `hooks/` | Dismissable and free-quota hooks |

Config: `vitest.config.ts`, `vitest.setup.ts`. Coverage is collected over `src/app`, `src/components`, `src/lib` and `src/db`, with floors enforced in CI: lines 30, statements 30, branches 26, functions 23. These are regression floors, not targets.

### 3.3 E2E (Playwright)

Layout under `src/StackAlchemist.Web/e2e/` (policy in `e2e/README.md`):

| Suite | Runs | Contents |
|-------|------|----------|
| `e2e/smoke/` | PR gate, demo mode (`npm run e2e:smoke`) | Simple and Advanced mode flows, checkout flow, dashboard, advanced-draft persistence, personalization modal keyboard handling, SEO content routes |
| `e2e/integration/` | main and nightly, non-demo (`npm run e2e:integration`) | `auth.spec.ts` against real Postgres and Keycloak: anonymous gating, sign-in through the realm, dashboard identity, sign-out, registration (helpers in `e2e/helpers/keycloak.ts`) |
| `e2e/nightly/` | nightly (`npm run e2e:nightly`) | Full Simple Mode generation run; needs the Engine and R2, signs in through Keycloak |

Selectors use `data-testid` for flow-critical assertions.

---

## 4. Backend Testing (.NET 10 Engine + Worker)

### 4.1 Frameworks & Tools

| Tool | Purpose |
|------|---------|
| **xUnit** (2.9) | Test runner |
| **NSubstitute** (6) | Mocking |
| **FluentAssertions** (8) | Assertions |
| **Testcontainers.PostgreSql** (4) | Real Postgres 17 for Engine data-access tests (Engine tests only) |
| **Microsoft.AspNetCore.Mvc.Testing** (10) | Host-level tests (Engine tests only) |
| **coverlet.collector** | Coverage collection in CI |

### 4.2 Projects

`src/StackAlchemist.Engine.Tests/`:
- `Services/`: reconstruction, template provider, injection engine, prompt builder, tier gating, schema extraction, LLM client and routing (including BYOK routing and key protection), response guard and HTTP retry, build strategies (.NET and Python+React), compile service, state machine, orchestrator, reconciliation, R2 upload, Resend email, Stripe refund, Postgres delivery and billing stores, build-report writer, archiver, error categorizer.
- `Webhooks/`: `StripeWebhookTests`.
- `Data/`: Postgres URL parsing.
- `Integration/`: pipeline, input validation, engine host and data source, compile-worker retry/refund/build-report tests, and the template compile gates (section 5.3).
- `Fixtures/LlmResponses/`: canned LLM outputs (section 6).

`src/StackAlchemist.Worker.Tests/`: `CompileWorkerTests`, `RetryLogicTests`, `StateMachineTests`.

### 4.3 ReconstructionService

`ReconstructionService` parses raw LLM text (`[[FILE:path]]` blocks) into files and is the most critical parser in the system. `ReconstructionServiceTests` runs it against the fixtures in section 6: well-formed output, missing delimiters, truncation, markdown wrapping, duplicate blocks, empty blocks. Repair writes go through the same path rules as the first pass; a repair may only land inside the archive's tree.

### 4.4 State machine and compile worker

`GenerationStateMachineTests` (Engine) and `StateMachineTests` (Worker) cover the transition table in `GenerationStateMachine.cs`: the happy path `Pending -> Generating -> Building -> Packing -> Uploading -> Success`, the Tier 1 shortcut `Generating -> Packing`, `BuildFailed` retrying through `Generating` until `RetryCount` reaches 3, terminal states rejecting further events, and undefined pairs throwing. The compile-worker integration tests cover the retry loop, the build report and the Compile Guarantee refund. See [Generation State Machine](Generation%20State%20Machine.md).

### 4.5 Tier gating, Stripe webhook, BYOK

- `TierGatingServiceTests`: which tiers get which pipeline.
- `StripeWebhookTests` and `PostgresBillingStoreTests`: signature-verified webhook handling, `checkout.session.completed` through `process_checkout_completed`, idempotency, failure and refund events, and redelivery when a side effect fails.
- `ByokKeyProtectorTests` and `ByokRoutingTests`: key encryption round trip and routing of a user's own key to the right provider only.

---

## 5. Integration Testing

### 5.1 Test environment

`docker/docker-compose.test.yml`, with `docker/docker-compose.ci.yml` on CI, provides real Postgres 17 and a Keycloak container for the E2E integration lane. The Engine runs as a container in that lane (`sa-engine`) and the Web app is started from the checkout.

### 5.2 Two-user isolation suite

There is no RLS; isolation is owner-scoping in code. `__tests__/data/drizzle-store.integration.test.ts` proves it against real Postgres: it seeds two users and asserts that lists and stats are scoped to the caller, that `getGenerationForUser` returns `null` for another user's row (the same answer as for an unknown id), and that retry cannot flip another user's failed row. The same file covers the free-tier quota trigger (the sixth non-failed Tier 0 build in a month is rejected), profile upserts and `ensureProfile`. It runs when `TEST_DATABASE_URL` is set (the CI `frontend` job provides a Postgres 17 service and applies migrations first) and is skipped otherwise. A nightly job runs it through the qavren-db pooler.

### 5.3 Template compile gates

`Engine.Tests/Integration/` builds real projects from each template set: `V0SparkCompileTests`, `V1TemplateCompileTests`, `V1PythonReactCompileTests`, `V2DotNetNextJsCompileTests`, `V2PythonReactCompileTests`, `Tier3InfrastructureCompileTests`, plus `SwissCheeseEndToEndTests`. Toolchains are required on CI; a missing one fails the run instead of skipping.

### 5.4 Engine data access

`PostgresFixture` starts Postgres 17 via Testcontainers and applies the exact migrations the Web app ships (`src/StackAlchemist.Web/drizzle/*.sql`), so Engine SQL is tested against the schema prod has. It skips locally without Docker.

### 5.5 Mock LLM

The Engine ships `MockLlmClient`, used when no Anthropic key is configured, and `MockLlmClientTests` cover it. Pipeline tests use it or canned fixtures; no test calls a live LLM.

---

## 6. LLM-Specific Testing

Canned LLM responses live in `src/StackAlchemist.Engine.Tests/Fixtures/LlmResponses/`:

```
single-entity-valid.txt
multi-entity-valid.txt
entity-with-relationships.txt
malformed-delimiters.txt
truncated-response.txt
extra-markdown-wrapping.txt
duplicate-file-blocks.txt
empty-file-block.txt
v1-invoicehub-golden.txt
```

`LlmResponseGuardTests` covers the truncation guard (a truncated response is never applied, in the first pass or a repair), and `AnthropicLlmClientTests` and `LlmHttpRetryTests` cover the client and its HTTP retry behavior. Prompts are version-controlled in `src/StackAlchemist.Engine/Prompts/` and built by `PromptBuilder` (`PromptBuilderTests`). When prompts change, re-run the Engine suite.

---

## 7. Database Testing

- **Schema and drift:** `src/db/schema.ts` is the source of truth. The CI `frontend` job runs `drizzle-kit generate` and fails if it produces anything other than "No schema changes", then runs `npm run db:check`.
- **Migrations:** applied to a Postgres 17 service before the Vitest run (`DATABASE_URL_MIGRATE`).
- **Isolation:** two-user suite, section 5.2.
- **Triggers and functions:** the free-tier quota trigger is tested in the same suite; `PostgresDeliveryServiceTests` and `PostgresBillingStoreTests` exercise `append_build_log`, `increment_token_usage` and `process_checkout_completed` from the Engine side.
- **Pooler:** a nightly job (`db-pooler-nightly`) runs the data-store suite through the qavren-db Supavisor pooler.

---

## 8. CI Pipeline and Gates

`.github/workflows/ci.yml` jobs:

| Job | Runs | What it does |
|-----|------|--------------|
| `frontend` | PR, main, nightly | `npm ci`, drizzle drift check, migrations, ESLint (`--max-warnings 0`), dependency audit gate (`npm run audit:ci`), `tsc --noEmit`, `vitest run --coverage` against a Postgres 17 service |
| `backend` | PR, main, nightly | restore, build Engine, Worker and both test projects, `dotnet test` for Engine.Tests and Worker.Tests with coverage |
| `docker` | PR, main, nightly | Docker image build validation |
| `e2e-smoke` | PR, main, nightly | Playwright smoke suite in demo mode |
| `e2e-integration` | main, nightly, manual | Postgres + Keycloak containers, migrations, Engine container, `e2e:integration`, and `e2e:nightly` on the nightly trigger |
| `db-pooler-nightly` | nightly, manual | qavren-db pooler smoke |
| `quality-gate` | all but nightly | aggregates the required jobs |

Other workflows that gate or watch: `secret-scan.yml`, `tracker-guard.yml`, `prod-healthwatch.yml`. There is no staging environment to test against; the deploy to prod is the final step after the PR gates pass.

---

## 9. Known Gaps

- No visual regression suite exists (no screenshot assertions in `e2e/`).
- No load or chaos tests exist beyond the malformed-output fixtures above.
- Frontend coverage floors are low (section 3.2); they stop regressions but do not measure quality.
- No test environment exists beyond local and CI; E2E integration against the real Engine in CI uses a test R2 bucket and test-mode Stripe.

---

## 10. Test Data & Fixtures

| Data Type | Local Dev | CI |
|-----------|-----------|----|
| **Database** | Throwaway Postgres 17 container (see [Dev Environment Setup](Dev%20Environment%20Setup.md)) | Postgres 17 service / containers; Testcontainers for Engine tests |
| **Auth** | Local Keycloak realm `stackalchemist-dev` | Keycloak container |
| **LLM responses** | Fixtures in repo, `MockLlmClient` | Same |
| **Stripe** | Stripe test mode | Stripe test mode |
| **R2** | `stackalchemist-generations-dev` | `stackalchemist-generations-test` (E2E integration lane) |
| **Email (Resend)** | Mocked in unit tests | Mocked |

---

## 11. When to Write Tests

1. **Before a critical-path feature**: write the test first for the reconstruction parser, state machine, tier gating and billing paths.
2. **During feature development**: write tests alongside code for the rest.
3. **After a bug is found**: write a regression test that reproduces it before fixing it.
4. **Before merging**: all required CI gates pass.
