# CI E2E on Postgres + Keycloak Runbook

Since phase D the `E2E Integration` lane (`.github/workflows/ci.yml`, job
`e2e-integration`) runs the web app in Qavren mode against a Postgres and a
Keycloak that the job starts itself from `docker/docker-compose.test.yml`. No
Supabase project and no Test-environment database secret is involved. This
document covers what the lane does, the CI realm, the Playwright suite, a local
recipe, the nightly pooler smoke, the traps and what changed against the
Supabase-era lane. The flag and env contract live in `qavren-auth.md`; the
schema and migrator live in `qavren-db-migrations.md`. The old CI Supabase
runbook, `ci-supabase-migrations.md`, is superseded for CI only.

## What the lane does

The job runs on push to `main`, on the nightly schedule and on
`workflow_dispatch`, never on a PR (PRs get `e2e-smoke`). It needs `frontend`,
`backend` and `docker` first and uses `environment: test`. In order:

1. **Checkout, self-test, Setup Node.js, Install Dependencies, Install
   Playwright Browsers.** The self-test refuses to run if any workflow echoes a
   `*_SERVICE_ROLE_*` variable.
2. **Setup .env (test).** The `setup-env` action writes the repo-root `.env`.
   `database_url` is the container view of Postgres (below). The Stripe,
   Anthropic, R2 and Engine values still come from the Test environment; the
   Engine loads them, but only the nightly specs exercise them.
3. **Re-mask sensitive .env values.** Re-registers the `.env` secrets as masks so
   derived forms cannot leak.
4. **Validate Required Integration Secrets.** Sets the job output `skip_e2e`
   (below). A missing or placeholder secret warns and sets `skip_e2e=true`; it no
   longer stops the job. A value that is present but malformed (wrong prefix or
   length) still fails this step, and with it the job.
5. **Start Postgres + Keycloak (test compose stack).** `docker compose ... up -d
   --wait --wait-timeout 180 postgres keycloak`. Both services have healthchecks;
   Keycloak's realm import takes ~30 s, and 180 s makes a stuck container fail
   here instead of at the job timeout.
6. **Mint AUTH_SECRET for this run.** `openssl rand -base64 32`, masked and
   exported through `GITHUB_ENV`. Auth.js only has to agree with itself for the
   life of the job, so no repository secret exists for it in this lane.
7. **Apply qavren-db migrations to the CI Postgres.** `npm run db:migrate`, the
   web app's own migrator, never `drizzle-kit migrate` or `push` (see Traps in
   `qavren-db-migrations.md`).
8. **Copy .env to Next.js project.** Next reads env files from the web project
   directory, not the repo root.
9. **Build and Start Engine (Postgres mode)** and **Wait for Backend Health.**
   `sa-engine` only; it reaches Postgres by service name. The wait loop polls
   `http://localhost:5000/healthz` for 60 s and dumps the Engine log on timeout.
10. **Run Playwright Integration Tests.** `npm run e2e:integration`, ungated: it
    runs whatever `skip_e2e` says. Playwright's `webServer` starts `npm run dev`,
    which inherits the step env.
11. **Run Playwright Nightly Tests (real APIs, non-blocking).** Gated on
    `skip_e2e != 'true'` and on `schedule` or `workflow_dispatch`;
    `continue-on-error`. Same Keycloak and Postgres env as step 10.
12. **Dump backend container logs**, then **Upload Integration Playwright
    Report** and **Artifacts**, all `if: always()`. The log dump covers
    `sa-engine`, `keycloak` and `postgres`.
13. **Teardown Backend Services.** `docker compose ... down -v` (`if: always()`),
    which also drops Keycloak's H2 data and the Postgres volume.

### The two `DATABASE_URL` views

The same database is reached by two names:

| Who | Value | Where it is set |
|---|---|---|
| Engine, inside the compose network | `postgres://postgres:postgres@postgres:5432/stackalchemist?sslmode=disable` | `.env` (from the `setup-env` `database_url` input), and again as `sa-engine.environment` in `docker-compose.test.yml` so the Engine does not depend on the file |
| Migrator and Playwright, on the runner | `postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable` | step `env` of the migrate step (as `DATABASE_URL_MIGRATE`) and of both Playwright steps (as `DATABASE_URL`) |

The copied `src/StackAlchemist.Web/.env` carries the container name, which the
runner cannot resolve. `next dev` lets a variable already in the process
environment beat the env file, so the host view in the step `env` wins. The
blank `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY` and
`SUPABASE_SERVICE_ROLE_KEY` in the same blocks keep the app out of Supabase mode.
The URL carries `sslmode=disable` because the CI Postgres has no TLS. Both
values are throwaway CI constants, not secrets.

### `skip_e2e` and the Quality Gate

Before phase D, `skip_e2e=true` meant the whole lane was skipped because the
Supabase secrets were absent. Now it means **only the nightly real-API specs
(`e2e/nightly`) are skipped**: a Test-environment secret (Stripe, Anthropic, R2,
Engine key, tunnel token) is missing or a placeholder. The integration suite does
not read it.

The Quality Gate job still has "Fail if E2E Integration silently skipped on
main". It fails a `main` run whose `skip_e2e` is `true`, so a rotated or deleted
Test secret cannot quietly stop the nightly specs from protecting `main`. That
failure does not mean the integration suite skipped; it ran.

## The CI realm

`docker/keycloak/stackalchemist-ci-realm.json`, mounted read-only into the
container's import directory and loaded by `start-dev --import-realm`. Realm
`stackalchemist-ci`; the image is `quay.io/keycloak/keycloak:26.5.5`.

Load-bearing fields:

| Field | Value | Why it matters |
|---|---|---|
| Client id | `stackalchemist-ci-web` | The app derives the client as `${QAVREN_REALM}-web`, so `QAVREN_REALM=stackalchemist-ci` selects it. Rename the realm and the client together. |
| Redirect URI | `http://localhost:3000/api/auth/callback/keycloak` | The one exact Auth.js callback (provider id `keycloak`), not a wildcard. |
| Web origin | `http://localhost:3000` | |
| Access type | public client, standard flow, PKCE `S256` (`pkce.code.challenge.method`), no secret | Mirrors prod's client shape, plus `directAccessGrantsEnabled` (prod's client has none), which lets a script mint a user token without a browser. The suite itself signs in through the browser and uses direct grant only for the admin API (`admin-cli` on the master realm). |
| Post-logout allow-list | `post.logout.redirect.uris` = `http://localhost:3000/*` | `/auth/signout` sends `post_logout_redirect_uri = ${NEXT_PUBLIC_APP_URL}/`. |
| `registrationAllowed` | `true` | The registration test goes through `prompt=create`. |
| `registrationEmailAsUsername` | `true` | The email is the username: the registration form has no username field and the login field is labelled "Email". |
| Fixture user | `e2e@stackalchemist.test`, `emailVerified: true`, `firstName` `E2E`, `lastName` `Fixture`, non-temporary password, no required actions | First and last name are mandatory (Keycloak 26 refuses a nameless user, see Troubleshooting). |

Deliberate differences from qavren-auth's `stackalchemist-dev` realm:

- `verifyEmail: false`. There is no SMTP, so a sign-up must not wait for a
  verification mail.
- `sslRequired: none`. The stack is plain http on `localhost`.
- No Google identity provider, no custom login theme, no SMTP server. The suite
  drives Keycloak's stock theme (next section).

Things that bit:

- **The fixture password (`E2e-Fixture-2026!`) and the `admin`/`admin` bootstrap
  account are public CI constants** for a throwaway realm on a runner-local
  container. Never reuse either anywhere real.
- **`openid` is not a Keycloak client scope.** Keycloak handles it itself, so it
  was dropped from `defaultClientScopes`; do not add it back. The list is
  `profile`, `email`, `roles`, `web-origins`.
- **Discovery advertises both `plain` and `S256`** PKCE methods. That is the
  server's capability list, not the client's policy: only the client requires
  `S256`.
- **Edits to the JSON only take effect on a fresh container.** Keycloak imports
  into its embedded H2 database with `IGNORE_EXISTING`: a realm that already
  exists is left alone. After an edit run `down -v`, or `up -d --force-recreate
  keycloak`. CI always starts fresh, so the trap is local only.

## The Playwright suite

`src/StackAlchemist.Web/e2e/integration/auth.spec.ts`, seven tests, run by
`npm run e2e:integration` (chromium project, `NEXT_PUBLIC_DEMO_MODE=false`).

| Group | What it proves |
|---|---|
| Anonymous routing (4) | `/dashboard` and `/generate/<uuid>` redirect to `/login?returnTo=...`; `/%73imple` (percent-encoded `/simple`) is still gated; `/api/auth/providers` lists `keycloak` and `/api/auth/session` answers 200 with `null` |
| Sign-in (1) | The real Keycloak login form returns the browser to `/dashboard`, the page shows the fixture email, `/api/auth/session` carries that email, and the realm holds exactly one session for the user |
| Sign-out (1) | Sign-out ends BOTH the app cookie and the realm session (session count 0, `/dashboard` bounces to `/login`), even with an in-flight response (below) |
| Registration (1) | `/register` hands off to Keycloak with `prompt=create`, the user fills the form, lands signed in on `/dashboard`; `afterEach` deletes the user through the admin API |

**The sign-out race.** Phase C removed the sliding session refresh because a
gated response still in flight at sign-out re-issued the cookie and resurrected
the session (see "Session lifetime and sign-out" in `qavren-auth.md`). The test
replays that worst ordering deterministically: it captures the session cookies
the browser holds, clicks **Sign Out**, waits for `/`, then sends
`page.request.get("/dashboard")` with the pre-sign-out cookie. `page.request`
shares the browser's cookie jar, so a `Set-Cookie` on that late response would
land exactly where the browser's own would. A fetch fired from the page itself
does not work: the sign-out navigation cancels it. The test expects a 200 with no
session `Set-Cookie`, then that no non-empty session cookie remains, then that
`/dashboard` redirects to `/login`.

The 200 is part of the contract. Sessions are stateless 7-day JWTs, so the
replayed cookie still authenticates; that is what makes the assertion
non-vacuous. If it ever redirects instead, sign-out has started revoking
server-side and that line should change, not the lane.

**The helper** (`e2e/helpers/keycloak.ts`):

- `signInViaKeycloak(page, user?, startAt?)` goes to a gated page, clicks the
  app's own hand-off button (`main form button[type=submit]`) and fills
  Keycloak's form.
- `onKeycloak(url)` is true while the browser is under
  `${KC_URL}/realms/${KC_REALM}/`.
- `adminToken(request)` mints an admin token with the `admin-cli` client on the
  master realm, password grant, `admin`/`admin`.
- `findUserId(request, email)` looks a user up by exact username.
- `userSessionCount(request, email)` reads `/users/<id>/sessions`: the honest
  check that RP-initiated logout worked.
- `endUserSessions(request, email)` posts `/users/<id>/logout`, ending every realm
  session the user holds.
- `deleteUserByEmail(request, email)` removes a user (registration cleanup).
- `KC_URL` defaults to `http://localhost:8080` (`E2E_KEYCLOAK_URL`), `KC_REALM`
  to `stackalchemist-ci` (`QAVREN_REALM`).

**Selectors** come from Keycloak 26.5.5's stock `keycloak.v2` theme, by
accessible name, not from `data-testid`s. A Keycloak upgrade or a custom theme
can move them:

- Login: textbox **Email** (the regex also accepts "Username or email"; with
  `registrationEmailAsUsername` the label is plain "Email"), textbox **Password**
  (anchored `/^password$/i` so it never matches the "Show password" toggle),
  button **Sign in**.
- Registration: heading **Register**; textboxes **Email**, **Password**,
  **Confirm password**, **First name**, **Last name**; button **Register**.

**Why `test.describe.configure({ mode: "default" })` and `endUserSessions()` in
`beforeEach`.** The sign-in and sign-out tests assert exact realm session counts
(1 after sign-in, 0 after sign-out). The project runs `fullyParallel: true`, and
locally with several workers; `mode: "default"` runs the two tests in order on
one worker. Closing a browser context leaves its SSO session alive on the server,
so earlier tests and earlier runs against the same stack leave sessions behind;
`endUserSessions()` starts each counting test from zero.

**The nightly specs sign in first.** `e2e/nightly/simple-mode-flow.spec.ts` and
`realtime-fallback.spec.ts` call `signInViaKeycloak(page)` in `beforeEach`,
because `/simple` and `/generate` are gated. They use the same fixture user.

**The fixture user is shared.** The integration suite and the nightly specs both
sign in as `e2e@stackalchemist.test`, and the integration suite ends that user's
sessions. Never run both against one stack at once. In CI they run one after the
other in the same job (workers = 1), which is fine.

## Run it locally

The recipe is in `src/StackAlchemist.Web/e2e/README.md`, section "Integration
suite (Postgres + Keycloak)". Do not copy it here. What bit:

- **Ports 3000, 5432, 8080 and 9000 must be free.** 5432/8080 are Postgres and
  Keycloak, 9000 is Keycloak's management port (the healthcheck), 3000 is the
  dev server. The throwaway `sa-pg` from `qavren-db-migrations.md` publishes
  55440, so it does not clash.
- **An app already listening on 3000 is reused.** Playwright's `webServer` sets
  `reuseExistingServer` outside CI, so a stale or wrong-mode dev server on 3000
  is silently tested instead of a fresh one. Stop it first.
- **`PLAYWRIGHT_CHANNEL=chrome` or `msedge`** runs the `chromium` project on an
  installed browser when the bundled Chromium cannot be downloaded. CI never sets
  it.
- **`NEXT_PUBLIC_DEMO_MODE=false` comes from the npm script** (`e2e:integration`
  and `e2e:nightly`). Running `npx playwright test` directly loses it, and demo
  mode (auto-enabled when the Supabase URL is blank outside production) switches
  the proxy gate off, so nothing redirects to sign-in.
- **Kill the dev server afterwards.** Playwright stops the server it started, but
  one you started yourself, or an orphan from an aborted run, keeps port 3000.
- **`down -v` also drops the registration leftovers.** The registration test
  deletes its Keycloak user through the admin API, but any Postgres rows the app
  wrote for that user stay until the volume goes.
- **Do not run the integration suite and the nightly specs against one stack at
  once.** They share the fixture user.

## Nightly pooler smoke

Job `db-pooler-nightly` ("qavren-db pooler smoke (nightly)"). It exists because
the integration lane now tests against a plain local Postgres, and prod will talk
to qavren-db through Supavisor. This job keeps the prod-shaped path exercised.
`needs: [frontend]`, runs only on `schedule` and `workflow_dispatch`, and is not
part of the Quality Gate: it never gates a PR.

- **Migrate qavren-db-test (session URL):** `npm run db:migrate` with
  `DATABASE_URL_MIGRATE` (the `:5432` session-mode URL).
- **Isolation suite through the transaction pooler:** `npx vitest run
  __tests__/data/drizzle-store.integration.test.ts` with `TEST_DATABASE_URL` set
  from `DATABASE_URL` (the `:6543` transaction-pooler URL, `prepare: false`).

Test-environment secrets it needs: `DATABASE_URL_MIGRATE` and `DATABASE_URL`
(the qavren-db-test logins from phase A). Each step prints a warning and exits 0
when its secret is absent, so the job is green and useless until both exist;
read the warnings. Both URLs must be percent-encoded as described in
`qavren-db-migrations.md`.

## Troubleshooting

- **Keycloak never turns healthy (the compose step fails after 180 s).** Read the
  `keycloak logs` group of the log-dump step, or locally `docker compose -f
  docker/docker-compose.test.yml logs keycloak`. Usual causes: a syntax error in
  the realm JSON (including a client scope Keycloak does not know, see `openid`
  above), or a port clash on 8080/9000 (locally). A merely slow Keycloak is fine:
  the healthcheck allows 20 s plus 36 tries 5 s apart, and the compose wait 180 s.
- **"Account is not fully set up".** Keycloak 26 refuses a direct grant, and a
  browser sign-in stalls on a required-action screen, for a user with no first or
  last name. Give the user both, `emailVerified: true` and no required actions,
  in the realm JSON or through the admin API.
- **The login field cannot be found as "Username or email".** With
  `registrationEmailAsUsername` Keycloak labels it "Email". Selectors must accept
  the plain name (the helper does).
- **`waitForURL` on the registration form never fires.** `prompt=create` renders
  the registration form on the SAME `/protocol/openid-connect/auth?...&prompt=create`
  URL. There is no `/registration` path; match the `prompt` query parameter, not a
  path.
- **A CSP violation on every sign-out in CI.** The `form-action` directive in
  `next.config.ts` hardcodes `https://auth.stackalchemist.app`, so the redirect to
  `localhost:8080` is reported. The policy is report-only, so nothing breaks and
  nothing needs fixing before CSP is enforced; at that point CI's Keycloak origin
  must be allowed.
- **A cookie captured before sign-out still authenticates afterwards.** Expected:
  the session is a stateless JWT that lives until its 7-day expiry. The sign-out
  race test depends on it.
- **A realm edit has no effect.** H2 plus `IGNORE_EXISTING`; run `down -v` (see
  above).
- **The first cold `next dev` compile times out several tests.** Once, locally, 5
  tests hit the 30 s per-test timeout while routes compiled for the first time.
  It did not recur; CI has 2 retries. Re-run before debugging.
- **Sign-in works but session counts are off.** Leftover SSO sessions from an
  earlier run. The `beforeEach` clears them; if you assert counts in a new test,
  do the same.

## What changed vs. the Supabase-era lane

- **Gone:** the `CI_SUPABASE_DB_URL` secret, the Supabase CLI install and the
  "Apply Supabase migrations to CI project" step, and the old
  `e2e/integration/dashboard.spec.ts` (replaced by `auth.spec.ts`). Nothing in
  the repo reads `CI_SUPABASE_DB_URL` any more.
- **Owner action:** delete `CI_SUPABASE_DB_URL` from the Test environment. **Keep
  the other Supabase secrets there:** `deploy-test.yml` still uses them.
- **Untouched until phase E:** `deploy-prod.yml` still applies Supabase
  migrations to prod and still links to `ci-supabase-migrations.md`. That runbook
  stays; only its CI section is superseded.
- **The CI Supabase project** (`cdlefpvsvyepofsboepc`) is now unused. It is on the
  free tier and may auto-pause, which no longer breaks anything. It is retired in
  phase F.
- **`skip_e2e`** narrowed from "skip the lane" to "skip the nightly real-API
  specs" (first section).
