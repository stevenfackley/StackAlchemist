# Qavren Re-platform — Phase D: CI on Postgres + Keycloak (no Supabase in CI)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `E2E Integration (Playwright, Main/Nightly)` lane runs the web app and the Engine in Postgres + Keycloak mode against containers started by CI — a Postgres 17 service migrated by the web's own migrator and a Keycloak 26 with an imported CI realm — with a signed-in Playwright suite (sign-in, dashboard, first-insert profile row, sign-out incl. the in-flight-request race, registration), and CI no longer touches the auto-pausing CI Supabase project. A nightly job smoke-tests the real `qavren-db-test` pooler.

**Architecture:** `docker/docker-compose.test.yml` gains `postgres` and `keycloak` services on the existing `stackalchemist-test` network; the Engine container gets `DATABASE_URL` pointing at `postgres:5432`; the web runs on the runner host through Playwright's `webServer` (`npm run dev`) with the Qavren env in the step's `env:` (process env beats `.env`, so the host-view `localhost:5432` URL wins there). A Keycloak realm JSON checked into the repo (`docker/keycloak/stackalchemist-ci-realm.json`) is imported with `start-dev --import-realm`; it mirrors qavren-auth's `stackalchemist-dev` realm (public PKCE client, registration on) but with `verifyEmail: false` (no SMTP in CI) and a fixture user. **`deploy-prod.yml` is NOT touched** (prod stays in Supabase mode until phase E; the parent plan's phase D deploy bullet moves to E).

**Tech Stack:** GitHub Actions, docker compose, `postgres:17-alpine`, `quay.io/keycloak/keycloak:26.5.5` (`start-dev --import-realm`), Playwright 1.63 (chromium), the phase B migrator (`npm run db:migrate`), phase C's Qavren mode (`QAVREN_AUTH_URL`, `QAVREN_REALM`, `AUTH_SECRET`, `AUTH_URL`).

Parent plan: `2026-09-28-qavren-replatform.md` (phase D). Phase C record: `2026-09-30-qavren-replatform-C-auth.md` (its amendments hold: `@/auth` lazy-only, `force-dynamic` pages, fixed 7-day sessions, sign-out deletes cookies with `Expires`).

---

## Decisions specific to this phase

1. **CI realm is a repo-local Keycloak JSON, not a clone of qavren-auth.** Cloning the platform repo in CI would need a token and couple every StackAlchemist run to another repo's layout. The JSON mirrors `realms/apps/stackalchemist-dev.yaml` where it matters (client `stackalchemist-ci-web`, public + PKCE S256, redirect `http://localhost:3000/api/auth/callback/keycloak`, post-logout `http://localhost:3000/*`, `registrationAllowed: true`) and deliberately differs in `verifyEmail: false`, `sslRequired: none`, no SMTP, no Google IdP, no theme. Drift is acceptable for CI; the runbook says which fields are load-bearing.
2. **Fixture user in the realm JSON** (`e2e@stackalchemist.test` / a public CI constant password, `firstName`/`lastName` set, `emailVerified: true`): Keycloak 26 refuses direct grant and shows "Account is not fully set up" for a user without names — the KC 26 trap from the parent plan. The password is a CI constant, not a secret (same stance as qavren-auth's QA fixtures); it only ever reaches a throwaway container.
3. **Postgres and Keycloak live in `docker-compose.test.yml`**, not in GitHub `services:` (services cannot take a `command`, and the Engine container must reach Postgres by service name on the compose network). Ports are published to the runner host for the web, the migrator and Playwright.
4. **Two views of `DATABASE_URL`.** `.env` (rendered by `setup-env`, consumed by the Engine via `env_file`) carries the container view `postgres://postgres:postgres@postgres:5432/stackalchemist?sslmode=disable`; the Playwright/migrate steps set the host view `…@localhost:5432/…` in step `env:`. Process env overrides `.env` for `next dev`.
5. **Remove the Supabase dependency from CI entirely**: the `Apply Supabase migrations to CI project` step, the Supabase CLI download, `CI_SUPABASE_DB_URL`, and the three Supabase entries in `validate-secrets`. `setup-env`'s Supabase inputs become optional (`required: false`, `default: ''`); prod still passes them. The CI Supabase project (`cdlefpvsvyepofsboepc`) may be paused after this merges; it is retired in phase F.
6. **`AUTH_SECRET` is minted per run** (`openssl rand -base64 32`, masked) — no new repository secret.
7. **The nightly Spark flow keeps needing real R2 + Engine secrets** (Tier 0 makes no LLM call, but the Engine uploads the archive to the test bucket). The integration (main-push) lane needs NO external secret: Keycloak + Postgres are local and the Engine boots in Staging without keys. `validate-secrets` therefore only gates the nightly specs; a missing R2/Stripe secret must not skip the auth suite. `skip_e2e` keeps its meaning ("the nightly real-API specs were skipped") and the Quality Gate's main-branch assertion stays.
8. **Nightly pooler job** (`db-pooler-nightly`): schedule + dispatch only, `environment: test`, runs `npm run db:migrate` with the `Test` environment's `DATABASE_URL_MIGRATE` and the Drizzle isolation suite with `TEST_DATABASE_URL` = the pooler `DATABASE_URL`. Proves prod-shaped connectivity (Supavisor, `prepare: false`) once a day; never blocks the gate.
9. **Existing nightly specs gain a Keycloak sign-in** (`/simple` is proxy-gated in Qavren mode). `realtime-fallback.spec.ts` stays as-is plus the sign-in: aborting `**/realtime/v1/**` matches nothing in Postgres mode, and the polling path is the only path, so the spec is a plain duplicate of the Spark flow — keep it until phase F deletes Realtime.

## File map

| File | Responsibility |
|---|---|
| `docker/keycloak/stackalchemist-ci-realm.json` | CI realm: client, fixture user, flags |
| `docker/docker-compose.test.yml` | + `postgres`, `keycloak` services; Engine `DATABASE_URL` |
| `src/StackAlchemist.Web/e2e/helpers/keycloak.ts` | `signInViaKeycloak`, `adminToken`, `userSessions`, `deleteUserByEmail` |
| `src/StackAlchemist.Web/e2e/integration/auth.spec.ts` (replaces `dashboard.spec.ts`) | anonymous redirects; sign-in; session; sign-out + race; registration |
| `src/StackAlchemist.Web/e2e/nightly/*.spec.ts` | + `signInViaKeycloak` in `beforeEach` |
| `.github/workflows/ci.yml` | `e2e-integration` rewritten; `db-pooler-nightly` added |
| `.github/actions/setup-env/action.yml` | Supabase inputs optional |
| `docs/runbooks/ci-e2e-keycloak-postgres.md` (new), `docs/runbooks/ci-supabase-migrations.md` (CI section marked superseded) | how the lane works, local recipe, traps |

## Environment for every task below

- Worktree: `C:\Users\steve\projects\_wt\StackAlchemist-ci-keycloak`, branch `feat/ci-keycloak-postgres-e2e` off `origin/main`. Web dir `<worktree>\src\StackAlchemist.Web`; run `npm ci` once there; Playwright chromium: `npx playwright install chromium`.
- Docker Desktop is running. Never `cd` in the Bash tool; PowerShell `Push-Location` for npm/vitest/playwright. LF; Conventional Commits; **no trailers**. Push only in Task 5.
- Local stack for Tasks 1–2: `docker compose -f docker/docker-compose.test.yml up -d --wait postgres keycloak` from the worktree root (ports 5432, 8080, 9000 must be free — `sa-pg` on 55440 does not collide). Tear down with `down -v`.

---

### Task 1: CI realm JSON + compose services

**Files:** create `docker/keycloak/stackalchemist-ci-realm.json`; modify `docker/docker-compose.test.yml`.

- [ ] **Step 1: Realm JSON** (Keycloak realm representation; `--import-realm` reads every `*.json` under `/opt/keycloak/data/import`):

```json
{
  "realm": "stackalchemist-ci",
  "enabled": true,
  "displayName": "StackAlchemist (CI)",
  "sslRequired": "none",
  "registrationAllowed": true,
  "registrationEmailAsUsername": true,
  "loginWithEmailAllowed": true,
  "duplicateEmailsAllowed": false,
  "verifyEmail": false,
  "resetPasswordAllowed": true,
  "bruteForceProtected": false,
  "accessTokenLifespan": 300,
  "ssoSessionIdleTimeout": 1800,
  "clients": [
    {
      "clientId": "stackalchemist-ci-web",
      "name": "StackAlchemist web (CI)",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": true,
      "implicitFlowEnabled": false,
      "serviceAccountsEnabled": false,
      "redirectUris": ["http://localhost:3000/api/auth/callback/keycloak"],
      "webOrigins": ["http://localhost:3000"],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "http://localhost:3000/*"
      },
      "defaultClientScopes": ["openid", "profile", "email", "roles", "web-origins"],
      "optionalClientScopes": ["offline_access"]
    }
  ],
  "users": [
    {
      "username": "e2e@stackalchemist.test",
      "email": "e2e@stackalchemist.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "E2E",
      "lastName": "Fixture",
      "credentials": [{ "type": "password", "value": "E2e-Fixture-2026!", "temporary": false }],
      "requiredActions": []
    }
  ]
}
```

Comment in the runbook (JSON has no comments): the password is a public CI constant; `firstName`/`lastName` are load-bearing (KC 26 "Account is not fully set up"); `directAccessGrantsEnabled` exists so a spec can mint a token with the password grant when it needs an API-level check; `verifyEmail: false` because CI has no SMTP; everything else mirrors qavren-auth `realms/apps/stackalchemist-dev.yaml`.

- [ ] **Step 2: compose services** — add to `docker/docker-compose.test.yml` (same network; healthchecks so `--wait` works):

```yaml
  postgres:
    container_name: sa-test-postgres
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: stackalchemist
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d stackalchemist"]
      interval: 5s
      timeout: 5s
      retries: 20
    networks:
      - stackalchemist-test

  keycloak:
    container_name: sa-test-keycloak
    image: quay.io/keycloak/keycloak:26.5.5
    command: ["start-dev", "--import-realm"]
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
      KC_HEALTH_ENABLED: "true"
      KC_HTTP_ENABLED: "true"
    ports:
      - "8080:8080"
      - "9000:9000"
    volumes:
      - ./keycloak:/opt/keycloak/data/import:ro
    healthcheck:
      # The image ships no curl/wget; bash's /dev/tcp against the management port.
      test:
        - CMD-SHELL
        - |
          exec 3<>/dev/tcp/127.0.0.1/9000
          printf 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3
          grep -q '"status": "UP"' <&3
      interval: 5s
      timeout: 5s
      retries: 36
      start_period: 20s
    networks:
      - stackalchemist-test
```

and on `sa-engine`: `depends_on: postgres: { condition: service_healthy }` and, under a new `environment:` block that OVERRIDES the `env_file` value, `DATABASE_URL: postgres://postgres:postgres@postgres:5432/stackalchemist?sslmode=disable` (compose: `environment` wins over `env_file`). Leave `sa-web` untouched (CI runs the web on the host; the built `sa-web` service is for the test mirror).

- [ ] **Step 3: Verify live** from the worktree root: `docker compose -f docker/docker-compose.test.yml config` resolves; `up -d --wait postgres keycloak` → both healthy; `curl -s localhost:8080/realms/stackalchemist-ci/.well-known/openid-configuration | jq .issuer` → `http://localhost:8080/realms/stackalchemist-ci`; admin token (`POST /realms/master/protocol/openid-connect/token`, `admin-cli`, `admin`/`admin`) → `GET /admin/realms/stackalchemist-ci/users?username=e2e@stackalchemist.test&exact=true` shows the user with `firstName`, `emailVerified: true`; direct grant against `stackalchemist-ci-web` with the fixture password returns an `access_token` (proves the KC 26 trap is avoided); `curl -s localhost:5432` refused-vs-open sanity via `pg_isready -h localhost`. Then `down -v`.

- [ ] **Step 4: Commit** `ci(e2e): Keycloak CI realm and Postgres/Keycloak services in the test compose stack`

### Task 2: Playwright helper + signed-in specs (run locally against the stack)

**Files:** create `src/StackAlchemist.Web/e2e/helpers/keycloak.ts`; replace `e2e/integration/dashboard.spec.ts` with `e2e/integration/auth.spec.ts`; modify `e2e/nightly/simple-mode-flow.spec.ts`, `e2e/nightly/realtime-fallback.spec.ts`; modify `e2e/README.md`.

- [ ] **Step 1: Helper** `e2e/helpers/keycloak.ts`:

```ts
import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const KC_URL = process.env.E2E_KEYCLOAK_URL ?? "http://localhost:8080";
export const KC_REALM = process.env.QAVREN_REALM ?? "stackalchemist-ci";
export const FIXTURE_USER = { email: "e2e@stackalchemist.test", password: "E2e-Fixture-2026!" };

/** Drive the real Keycloak login page from an app page that shows the Qavren "Sign in" button. */
export async function signInViaKeycloak(page: Page, user = FIXTURE_USER, startAt = "/dashboard") {
  await page.goto(startAt);
  await page.waitForURL(/\/login/);
  await page.locator("main form button[type=submit]").click();
  await page.waitForURL(new RegExp(`${KC_URL.replace(/[/.:]/g, "\\$&")}/realms/${KC_REALM}/`));
  await page.getByRole("textbox", { name: /username or email/i }).fill(user.email);
  await page.getByRole("textbox", { name: /^password$/i }).fill(user.password);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(/localhost:3000/);
}

export async function adminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${KC_URL}/realms/master/protocol/openid-connect/token`, {
    form: { client_id: "admin-cli", grant_type: "password", username: "admin", password: "admin" },
  });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).access_token as string;
}

export async function findUserId(request: APIRequestContext, email: string): Promise<string | null> {
  const token = await adminToken(request);
  const res = await request.get(`${KC_URL}/admin/realms/${KC_REALM}/users`, {
    params: { username: email, exact: "true" }, headers: { authorization: `Bearer ${token}` },
  });
  const users = (await res.json()) as Array<{ id: string }>;
  return users[0]?.id ?? null;
}

/** Active realm sessions for a user — the honest check that RP-initiated logout worked. */
export async function userSessionCount(request: APIRequestContext, email: string): Promise<number> {
  const token = await adminToken(request);
  const id = await findUserId(request, email);
  if (!id) return 0;
  const res = await request.get(`${KC_URL}/admin/realms/${KC_REALM}/users/${id}/sessions`, {
    headers: { authorization: `Bearer ${token}` },
  });
  return ((await res.json()) as unknown[]).length;
}

export async function deleteUserByEmail(request: APIRequestContext, email: string): Promise<void> {
  const token = await adminToken(request);
  const id = await findUserId(request, email);
  if (id) await request.delete(`${KC_URL}/admin/realms/${KC_REALM}/users/${id}`, { headers: { authorization: `Bearer ${token}` } });
}
```

- [ ] **Step 2: `e2e/integration/auth.spec.ts`** (delete `dashboard.spec.ts`; keep its two anonymous-redirect tests verbatim as the first block):

```ts
import { expect, test } from "@playwright/test";
import { FIXTURE_USER, deleteUserByEmail, signInViaKeycloak, userSessionCount } from "../helpers/keycloak";

test.describe("Integration: anonymous routing", () => {
  test("dashboard redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });
  test("generation route redirects anonymous users to login with returnTo", async ({ page }) => {
    await page.goto("/generate/00000000-0000-0000-0000-000000000000");
    await page.waitForURL(/\/login/);
    await expect(page).toHaveURL(/returnTo/);
  });
  test("Auth.js endpoints answer in Qavren mode", async ({ request }) => {
    const providers = await request.get("/api/auth/providers");
    expect(providers.status()).toBe(200);
    expect(await providers.json()).toHaveProperty("keycloak");
    const session = await request.get("/api/auth/session");
    expect(session.status()).toBe(200);
    expect(await session.json()).toBeNull();
  });
});

test.describe("Integration: Keycloak sign-in, dashboard, sign-out", () => {
  test("signs in through the realm and lands on the dashboard with the identity", async ({ page }) => {
    await signInViaKeycloak(page);
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.getByText(FIXTURE_USER.email).first()).toBeVisible();
    const session = await page.request.get("/api/auth/session");
    expect((await session.json())?.user?.email).toBe(FIXTURE_USER.email);
  });

  test("sign-out ends both the app session and the realm session, even with a request in flight", async ({ page, request }) => {
    await signInViaKeycloak(page);
    expect(await userSessionCount(request, FIXTURE_USER.email)).toBeGreaterThan(0);
    // Provoke the race phase C fixed: a gated request still in flight at sign-out.
    const inflight = page.evaluate(() => fetch("/dashboard", { credentials: "include" }).then((r) => r.status));
    await page.locator('form[action="/auth/signout"] button[type="submit"]').click();
    await page.waitForURL(/localhost:3000\/?$/);
    await inflight;
    const cookies = await page.context().cookies("http://localhost:3000");
    expect(cookies.some((c) => /^(__Secure-)?authjs\.session-token/.test(c.name) && c.value !== "")).toBe(false);
    await page.goto("/dashboard");
    await expect(page).toHaveURL(/\/login\?returnTo=/);
    expect(await userSessionCount(request, FIXTURE_USER.email)).toBe(0);
  });

  test("a protected page reached by a percent-encoded path is still gated", async ({ page }) => {
    await page.goto("/%73imple");
    await page.waitForURL(/\/login/);
  });
});

test.describe("Integration: registration through the realm", () => {
  const email = `e2e-reg-${Date.now()}@stackalchemist.test`;
  test.afterEach(async ({ request }) => { await deleteUserByEmail(request, email); });

  test("Create an account opens Keycloak's registration form and returns signed in", async ({ page }) => {
    await page.goto("/register?returnTo=%2Fdashboard");
    await page.locator("main form button[type=submit]").click();
    await page.waitForURL(/\/realms\/.*\/login-actions\/registration|\/protocol\/openid-connect\/registrations/);
    await page.getByLabel(/first name/i).fill("Reg");
    await page.getByLabel(/last name/i).fill("Ression");
    await page.getByLabel(/^email$/i).fill(email);
    await page.getByLabel(/^password$/i).fill("Reg-Ression-2026!");
    await page.getByLabel(/confirm password/i).fill("Reg-Ression-2026!");
    await page.getByRole("button", { name: /register/i }).click();
    await page.waitForURL(/localhost:3000\/dashboard/);
    await expect(page.getByText(email).first()).toBeVisible();
  });
});
```

(Adjust the Keycloak registration selectors to the realm's stock login theme after seeing it once — the labels above are Keycloak 26's defaults. If `prompt=create` on this Keycloak version renders the registration form at a different URL, loosen the `waitForURL` and say so.)

- [ ] **Step 3: Nightly specs** — add `test.beforeEach(async ({ page }) => { await signInViaKeycloak(page, undefined, "/"); })` to both nightly specs (sign in starting from `/` so the flow then begins on the home page as today). Note in `realtime-fallback.spec.ts` that in Postgres mode polling is the only path (kept until phase F).

- [ ] **Step 4: Run locally** — `up -d --wait postgres keycloak`; `DATABASE_URL_MIGRATE=postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable npm run db:migrate`; then with `QAVREN_AUTH_URL=http://localhost:8080 QAVREN_REALM=stackalchemist-ci AUTH_SECRET=<openssl rand -base64 32> AUTH_URL=http://localhost:3000 NEXT_PUBLIC_APP_URL=http://localhost:3000 DATABASE_URL=postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable NEXT_PUBLIC_SUPABASE_URL= NEXT_PUBLIC_SUPABASE_ANON_KEY= SUPABASE_SERVICE_ROLE_KEY= ENGINE_API_URL=http://localhost:5000` run `npm run e2e:integration` (Playwright starts `next dev`). Expected: all `auth.spec.ts` tests pass (the Engine is not needed for them). Run twice. Record the Keycloak registration selectors that worked.

- [ ] **Step 5: Commit** `test(e2e): Keycloak sign-in helper; signed-in integration suite incl. the sign-out race and registration`

### Task 3: `ci.yml` — the lane on Postgres + Keycloak; nightly pooler job; setup-env inputs optional

**Files:** `.github/workflows/ci.yml`, `.github/actions/setup-env/action.yml`.

- [ ] **Step 1: `setup-env`**: `supabase_url`, `supabase_anon_key`, `supabase_service_role_key` → `required: false`, `default: ''` (descriptions gain "Empty = Postgres/Keycloak mode").

- [ ] **Step 2: `e2e-integration` job** — keep name, `environment: test`, `needs`, `if`, `outputs`, the self-test step, Node setup, `npm ci`, Playwright install, artifacts and teardown. Replace the middle:
  - `Setup .env (test)`: drop the three `supabase_*` lines; add `database_url: postgres://postgres:postgres@postgres:5432/stackalchemist?sslmode=disable` (container view, for the Engine); `app_url: http://localhost:3000`, `is_test_site: "false"` (the runner is not the mirror; Basic Auth off), `engine_api_url: http://localhost:5000`. Keep R2/Stripe/Anthropic/Engine-key inputs (nightly).
  - `Validate Required Integration Secrets`: remove the three Supabase keys from `required_keys` and the three Supabase `assert_pattern`s. Rename the warning text to say the NIGHTLY real-API specs will be skipped. Everything else unchanged.
  - Delete `Apply Supabase migrations to CI project` entirely (with its `CI_SUPABASE_DB_URL` reference).
  - New steps, in this order, none gated on `skip_e2e`:
    ```yaml
      - name: Start Postgres + Keycloak (test compose)
        run: docker compose -f docker/docker-compose.test.yml up -d --wait postgres keycloak

      - name: Mint AUTH_SECRET for this run
        run: |
          set -euo pipefail
          secret="$(openssl rand -base64 32)"
          echo "::add-mask::${secret}"
          echo "AUTH_SECRET=${secret}" >> "$GITHUB_ENV"

      - name: Apply qavren-db migrations to the CI Postgres
        working-directory: src/StackAlchemist.Web
        env:
          DATABASE_URL_MIGRATE: postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable
        run: npm run db:migrate

      - name: Copy .env to Next.js project
        run: cp .env src/StackAlchemist.Web/.env

      - name: Build and Start Engine (Postgres mode)
        run: docker compose -f docker/docker-compose.test.yml up -d --build sa-engine
      # (keep the existing "Wait for Backend Health" step, ungated)
    ```
    and the Playwright steps get the host-view Qavren env (process env beats `.env`):
    ```yaml
        env:
          CI: true
          QAVREN_AUTH_URL: http://localhost:8080
          QAVREN_REALM: stackalchemist-ci
          AUTH_URL: http://localhost:3000
          NEXT_PUBLIC_APP_URL: http://localhost:3000
          NEXT_PUBLIC_DEMO_MODE: "false"
          DATABASE_URL: postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable
          NEXT_PUBLIC_SUPABASE_URL: ""
          NEXT_PUBLIC_SUPABASE_ANON_KEY: ""
          SUPABASE_SERVICE_ROLE_KEY: ""
          E2E_KEYCLOAK_URL: http://localhost:8080
    ```
    (`AUTH_SECRET` arrives via `$GITHUB_ENV`.) The integration step is UNGATED (no `if: skip_e2e`); the nightly step keeps `skip_e2e != 'true'` plus schedule/dispatch. The log-dump step adds `postgres` and `keycloak` logs (`--tail 100`) next to `sa-engine`.
  - Update the "Fail if E2E Integration silently skipped on main" step's message: it now means the NIGHTLY real-API specs were skipped because a Test-env secret is a placeholder; keep the failure (it still catches secret regressions).

- [ ] **Step 3: `db-pooler-nightly` job**:
```yaml
  db-pooler-nightly:
    name: qavren-db pooler smoke (nightly)
    runs-on: ubuntu-latest
    environment: test
    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
    needs: [frontend]
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-node@v7
        with: { node-version: "${{ env.NODE_VERSION }}", cache: npm, cache-dependency-path: src/StackAlchemist.Web/package-lock.json }
      - working-directory: src/StackAlchemist.Web
        run: npm ci
      - name: Migrate qavren-db-test (session URL)
        working-directory: src/StackAlchemist.Web
        env: { DATABASE_URL_MIGRATE: "${{ secrets.DATABASE_URL_MIGRATE }}" }
        run: |
          set -euo pipefail
          if [ -z "${DATABASE_URL_MIGRATE:-}" ]; then echo "::warning::DATABASE_URL_MIGRATE not set in the Test environment; skipping"; exit 0; fi
          echo "::add-mask::${DATABASE_URL_MIGRATE}"
          npm run db:migrate
      - name: Isolation suite through the transaction pooler
        working-directory: src/StackAlchemist.Web
        env: { TEST_DATABASE_URL: "${{ secrets.DATABASE_URL }}" }
        run: |
          set -euo pipefail
          if [ -z "${TEST_DATABASE_URL:-}" ]; then echo "::warning::DATABASE_URL not set in the Test environment; skipping"; exit 0; fi
          echo "::add-mask::${TEST_DATABASE_URL}"
          npx vitest run __tests__/data/drizzle-store.integration.test.ts
```
(Write it in the file's expanded YAML style, not flow style. Not in `quality-gate`'s `needs`.)

- [ ] **Step 4: Validate** `actionlint -ignore 'label "prod" is unknown' -ignore 'label "ARM64" is unknown' .github/workflows/ci.yml` clean; PyYAML parse; `grep -n CI_SUPABASE_DB_URL .github` → nothing; `grep -n supabase_url .github/workflows/ci.yml` → only `deploy-prod.yml`/`deploy-test.yml` keep it.

- [ ] **Step 5: Commit** `ci(e2e): run the integration lane on Postgres + Keycloak; drop the CI Supabase project; nightly pooler smoke`

### Task 4: Runbooks

- [ ] `docs/runbooks/ci-e2e-keycloak-postgres.md`: what the lane starts and in which order; the two `DATABASE_URL` views; the realm JSON's load-bearing fields (client id, redirect URI, PKCE, `firstName`/`lastName`, `verifyEmail: false`) and how it deliberately differs from qavren-auth's realm; the fixture password is a public constant; how to run the same suite locally (Task 2 Step 4 recipe) and how to debug (Keycloak admin console `admin`/`admin` on :8080, `docker compose … logs keycloak`); traps: `AUTH_URL` must be the app origin, `NEXT_PUBLIC_DEMO_MODE=false` explicitly, ports 5432/8080/9000 must be free, `start-dev --import-realm` skips an existing realm (use `down -v` to re-import), KC 26 "Account is not fully set up".
- [ ] `docs/runbooks/ci-supabase-migrations.md`: add a banner under the title: the CI section is superseded by phase D (link); the prod deploy section still applies until phase E.
- [ ] Commit `docs(runbooks): CI e2e on Postgres + Keycloak; mark the CI Supabase runbook superseded`

### Task 5: PR, review, merge, prove the lane

- [ ] PR `ci: E2E integration lane on Postgres + Keycloak (re-platform phase D)`; body: decisions 1–9, the parent-plan deviation (deploy-prod untouched until E), local run results, what to expect on the main push.
- [ ] Opus review blocking classes: any secret or realm password echoed in a log line; `deploy-prod.yml` touched; `quality-gate` weakened; the integration Playwright step gated on `skip_e2e`; a Supabase reference left in `ci.yml`.
- [ ] Merge (standing StackAlchemist authorization). **Then the real proof:** the `E2E Integration (Playwright, Main/Nightly)` job on the main push must be GREEN — the first green integration run since the CI Supabase project started auto-pausing. Record run id + duration here. If it fails, fix-forward on a branch; do not revert phase B/C.
- [ ] `workflow_dispatch` once to exercise the nightly path (`db-pooler-nightly` + the Spark nightly specs); record.
- [ ] Vault + memory: the CI Supabase auto-pause note (`reference_stackalchemist_ci_supabase_autopause`) becomes "RESOLVED by phase D"; the StackAlchemist state note.

## Execution record (2026-09-30)

- **Amendments found in execution:** `openid` is not a Keycloak client scope (dropped); the explicit `defaultClientScopes` list left out `basic`/`acr` and the access token lost `sub` — the CI client now inherits the realm defaults; `directAccessGrantsEnabled: false` for platform parity; with `registrationEmailAsUsername` the Keycloak login field is labelled "Email"; `prompt=create` renders the registration form on the same `/protocol/openid-connect/auth` URL (no `/registration` path); the sign-out race is replayed deterministically with the pre-sign-out cookie through `page.request` (a `page.evaluate(fetch)` dies with the form navigation); exact realm session counts need `endUserSessions()` in `beforeEach` and `mode: "default"`; nightly specs sign in from `/dashboard`, not `/simple?q=` (that would start a real build); `playwright.config.ts` gained an opt-in `PLAYWRIGHT_CHANNEL` (system Chrome/Edge) because the bundled Chromium could not be downloaded from the dev network; `up -d --wait` is bounded with `--wait-timeout 180`; the pooler test's own postgres-js client needed `prepare: false`.
- **Blocker caught by the final review:** `docker/docker-compose.test.yml` is shared with `deploy-test.yml`, whose bare `up -d` would have started Keycloak (`admin`/`admin`) and Postgres on the test mirror and repointed the mirror Engine at an unmigrated database. The CI-only services and the Engine override moved to `docker/docker-compose.ci.yml`, passed as a second `-f` in every `ci.yml` compose call; the base file is byte-identical to before.
- **Runner-image regression fixed on the way:** GitHub's `ubuntu-24.04` image `20260927.320.1` dropped Helm, turning `Tier3InfrastructureCompileTests.HelmChart_Lints` red on every PR (the 18:41 main push on `20260920.314.1` still passed). The Backend job now installs Helm explicitly (`azure/setup-helm@v5`, `version: v4.3.0`).
- **Task 5 results:** PR #437 (8 commits; spec + Opus reviews per task, final whole-branch Opus review APPROVED after the overlay fix) squash-merged as `58f0a299`. Main-push CI run 36766447873: **`E2E Integration (Playwright, Main/Nightly)` GREEN** — `migrations applied (2)`, Engine healthy after ~2 s in Postgres mode, Playwright `7 passed (39.9s)`, nightly step skipped (push trigger), Quality Gate success; `deploy-prod` 36766447657 success, `/api/healthz` 200 (prod unchanged, Supabase mode). First green integration run since the CI Supabase project began auto-pausing on 2026-09-23. Not yet exercised: `db-pooler-nightly` and the Spark nightly specs — a manual dispatch of `ci.yml` on `main` (Actions tab → CI → Run workflow) runs both.
- **Owner follow-ups:** delete `CI_SUPABASE_DB_URL` from the Test environment (keep the other Supabase secrets; `deploy-test.yml` uses them); the CI Supabase project `cdlefpvsvyepofsboepc` may be paused (retired in phase F); sweep the fleet for other test gates that assume preinstalled Helm/kubectl/terraform.

## Exit criteria

- [x] `E2E Integration` runs on every main push with no Supabase secret, against Postgres + Keycloak containers, and passes.
- [x] The signed-in suite covers sign-in, session, sign-out (incl. the in-flight race), percent-encoded gate, registration.
- [x] `CI_SUPABASE_DB_URL` and the Supabase CLI are gone from CI; `deploy-prod.yml` unchanged.
- [ ] Nightly pooler smoke exists and passed once via dispatch.

Phase E (cutover, owner-run) can start once the first line is true and phase A's owner tasks are done.
