# E2E Contract Policy

## Stable Selector Rules
- Use `data-testid` for flow-critical assertions and transitions.
- Prefer semantic roles only when they are unique and contractually stable.
- Do not gate core flow tests on marketing copy, cosmetic headings, or broad regex text matches.
- Avoid ambiguous locators that can match multiple elements in strict mode.

## Suite Strategy
- `e2e/smoke`: deterministic PR gate, run in demo mode.
- `e2e/integration`: main/nightly verification in non-demo mode with real environment wiring.
- `e2e/nightly`: full generation runs; they also need the Engine (and R2), and sign in through Keycloak first.

## Integration suite (Postgres + Keycloak)
`e2e/integration/auth.spec.ts` runs the app in Qavren mode against a real Postgres and a real Keycloak:
anonymous gating (including a percent-encoded path), sign-in through the realm's login page, the
dashboard identity, sign-out (app cookie and realm session both gone, and a late gated response cannot
resurrect the session), and registration through Keycloak's own form (`prompt=create`). The browser helpers
live in `e2e/helpers/keycloak.ts`.

What it needs running:
- `postgres` and `keycloak` from `docker/docker-compose.test.yml`; Keycloak imports
  `docker/keycloak/stackalchemist-ci-realm.json` (realm `stackalchemist-ci`, client `stackalchemist-ci-web`).
- The schema migrated (`npm run db:migrate`).
- The app in Qavren mode. Playwright's `webServer` starts `npm run dev`, which inherits the shell's env.

Local recipe (PowerShell, from `src/StackAlchemist.Web`; ports 3000, 5432, 8080 and 9000 must be free):

```powershell
docker compose -f ..\..\docker\docker-compose.test.yml up -d --wait postgres keycloak
$env:DATABASE_URL_MIGRATE = 'postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable'
npm run db:migrate

$env:QAVREN_AUTH_URL = 'http://localhost:8080'
$env:QAVREN_REALM = 'stackalchemist-ci'
$env:AUTH_SECRET = '<any 32-byte base64, e.g. openssl rand -base64 32>'
$env:AUTH_URL = 'http://localhost:3000'
$env:NEXT_PUBLIC_APP_URL = 'http://localhost:3000'
$env:DATABASE_URL = 'postgres://postgres:postgres@localhost:5432/stackalchemist?sslmode=disable'
$env:NEXT_PUBLIC_SUPABASE_URL = ''; $env:NEXT_PUBLIC_SUPABASE_ANON_KEY = ''; $env:SUPABASE_SERVICE_ROLE_KEY = ''
$env:ENGINE_API_URL = 'http://127.0.0.1:5000'
$env:E2E_KEYCLOAK_URL = 'http://localhost:8080'
npm run e2e:integration          # sets NEXT_PUBLIC_DEMO_MODE=false itself

docker compose -f ..\..\docker\docker-compose.test.yml down -v
```

If the bundled Chromium cannot be downloaded, set `$env:PLAYWRIGHT_CHANNEL = 'chrome'` (or `msedge`) to run
the `chromium` project on an installed browser; CI leaves it unset.

Notes:
- The fixture user `e2e@stackalchemist.test` / `E2e-Fixture-2026!` and the Keycloak `admin`/`admin` bootstrap
  account are public CI constants for a throwaway realm, not secrets. Registration tests create their own
  user and delete it through the admin API afterwards.
- The sign-in tests end the fixture user's realm sessions before each test (browser contexts close, SSO
  sessions stay), so the suite can run repeatedly against the same stack.
- Keycloak selectors come from its stock `keycloak.v2` login theme (accessible names such as "Email",
  "Password", "Register"), not from `data-testid`s; a Keycloak upgrade or a custom theme can move them.
  With `registrationEmailAsUsername` the login field is labelled "Email", not "Username or email".
- Realm edits need `down -v`: Keycloak imports into its embedded H2 with IGNORE_EXISTING.

## When UI Changes
- If a UI change touches a contract anchor (home/simple/advanced/pricing/generate), update the corresponding `data-testid` and matching E2E spec in the same PR.
- If behavior intentionally changes (for example route semantics or phase order), update the affected spec first so CI reflects the new contract rather than a stale assumption.
- Keep artifact-producing CI steps intact so failures always include report + trace/screenshot outputs.
