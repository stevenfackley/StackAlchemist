# Re-platform phase A: platform provisioning

> **For agentic workers:** phase A is platform ops in two other repos (qavren-db, qavren-auth) plus owner-only console/apply steps. No StackAlchemist code changes. Tasks 1-3 were executed inline on 2026-09-29; tasks 4-7 are the owner's. Parent plan: `2026-09-28-qavren-replatform.md`.

**Goal:** A `stackalchemist` schema + role on qavren-db (test now, prod later) and a `stackalchemist` Keycloak realm (plus a `stackalchemist-dev` realm for localhost), with the connection strings and hostname in place, so phases B and C have something to point at.

**Architecture:** Nothing here is app code. qavren-db's provisioner renders one idempotent SQL template per app (schema owned by a login role that holds no other grants) and prints the connection strings once. qavren-auth's provisioner renders a realm YAML from the fleet template; keycloak-config-cli applies it; Terraform owns the `auth.<domain>` edge (CNAME + tunnel ingress + 308 bridge from the shared host).

**Tech stack:** PowerShell 7 provisioners, psql 18 client, keycloak-config-cli, Terraform + Cloudflare, `gh secret set` (reads the value from stdin).

---

## Decisions specific to this phase

- **Two realms.** `stackalchemist` carries only the production callback (`https://stackalchemist.app/api/auth/callback/keycloak`); `stackalchemist-dev` carries `http://localhost:3000/...` and nothing else, and stays on the shared auth host. The fleet learned this the hard way (see the `clients:` comment in qavren-auth `realms/apps/fairsquare.yaml`): a localhost redirect URI on a production realm hands any signed-in user's authorization code to whatever listens on that user's port 3000.
- **`registrationAllowed: true` and `verifyEmail: true` together.** Never one without the other (qavren-auth `realms/apps/haulcall.yaml` records the incident). Google users arrive verified via the broker's `trustEmail`.
- **`frontendUrl` + tfvars entry + `bridge = true` in one PR.** The runbook orders these across days for realms with live integrators. This realm has none, so there is nothing to break by doing it at once (recharacter did the same on 2026-08-16).
- **Connection strings carry `?sslmode=require`.** postgres-js reads it from the URL; Npgsql does not parse libpq URIs at all, so phase B converts the URL into an Npgsql connection string in the Engine (or adds a second secret). Noted here so nobody wonders why the Engine can't read `DATABASE_URL` verbatim.
- **No Keycloak admin service account.** StackAlchemist has no account-deletion or export feature (parent plan, facts table), so unlike recharacter it needs no `manage-users` client.
- **No favicon in the skin yet.** The product ships an SVG favicon; keycloak.v2 wants `img/favicon.ico`. It falls back to Qavren's until `tools/Make-Favicon.ps1` is run (needs Playwright Chromium + Pillow once).

---

### Task 1: qavren-db test schema + role — DONE 2026-09-29

**Files (qavren-db):**
- Create: `apps/stackalchemist.yaml` (generated, then `notes:` filled in)
- Modify: `docs/migration-playbook.md` row 14

- [x] **Step 1: Provision on `qavren-db-test`, credentials to a file, not the terminal**

`mise exec` did not work from the Bash tool (exit 127), so the `.env` was loaded in-process:

```powershell
$D = 'C:\Users\steve\projects\qavren-db'
Get-Content "$D\.env" | ForEach-Object {
  if ($_ -match '^\s*([A-Z_][A-Z0-9_]*)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2].Trim().Trim('"'), 'Process') }
}
pwsh -NoProfile -File "$D\tools\provision-app.ps1" -App stackalchemist -Env test -Apply *> "$env:USERPROFILE\stackalchemist-credentials-vault-20260929\provision-test.txt"
Get-Content "...\provision-test.txt" | Where-Object { $_ -notmatch '^(password|session_url|pooler_url)=' }
```

Expected (got): `app=stackalchemist`, `env=test`, `status=created`, `wrote ...apps/stackalchemist.yaml`.

- [x] **Step 2: Commit the manifest + playbook row, open the PR**

qavren-db PR **#41** (`feat/stackalchemist-app`). Re-running the provisioner is a no-op (`status=updated`, "password unchanged").

### Task 2: StackAlchemist `Test` environment secrets — DONE 2026-09-29

- [x] **Step 1: Pipe the URLs in without displaying them**

```bash
V=~/stackalchemist-credentials-vault-20260929/provision-test.txt; R=stevenfackley/StackAlchemist
grep -oP '^pooler_url=\K.*'  "$V" | sed 's/$/?sslmode=require/' | tr -d '\r\n' | gh secret set DATABASE_URL         --repo $R --env Test
grep -oP '^session_url=\K.*' "$V" | sed 's/$/?sslmode=require/' | tr -d '\r\n' | gh secret set DATABASE_URL_MIGRATE --repo $R --env Test
gh secret list --repo $R --env Test | grep DATABASE_URL
```

Expected (got): both names listed with today's timestamp. `DATABASE_URL` is the Supavisor transaction pooler (`:6543`, runtime, `prepare: false`); `DATABASE_URL_MIGRATE` is session mode (`:5432`, migrations only).

The vault file at `C:\Users\steve\stackalchemist-credentials-vault-20260929\provision-test.txt` still holds the test password. Delete it once phase B's CI has proven the secrets work, or keep it until prod is provisioned; owner's call.

### Task 3: qavren-auth realms, hostname, login skin — PR opened 2026-09-29

**Files (qavren-auth):**
- Create: `realms/apps/stackalchemist.yaml`, `realms/apps/stackalchemist-dev.yaml`
- Create: `themes/stackalchemist/login/theme.properties`, `themes/stackalchemist/login/resources/css/stackalchemist.20260929a.css`, `themes/stackalchemist/login/resources/img/logo.20260929a.svg`
- Modify: `infra/hostnames.auto.tfvars`, `docs/hostname-cutover-runbook.md` (realm table)

- [x] **Step 1: Render both realms from the template**

```powershell
pwsh tools/provision-app.ps1 -App stackalchemist     -DisplayName StackAlchemist         -RedirectUris 'https://stackalchemist.app/api/auth/callback/keycloak'
pwsh tools/provision-app.ps1 -App stackalchemist-dev -DisplayName 'StackAlchemist (Dev)' -RedirectUris 'http://localhost:3000/api/auth/callback/keycloak'
```

- [x] **Step 2: Hand-edit (the template's defaults are for a social-only realm)**

Both: `loginTheme: stackalchemist`; `registrationAllowed: true` + `verifyEmail: true`; `webOrigins` explicit (`["https://stackalchemist.app"]` / `["http://localhost:3000"]`); `post.logout.redirect.uris` explicit (`https://stackalchemist.app/*` / `http://localhost:3000/*`). Prod only: `attributes: { frontendUrl: https://auth.stackalchemist.app }`.

- [x] **Step 3: Edge entry + runbook row**

`infra/hostnames.auto.tfvars`: `stackalchemist = { zone = "stackalchemist.app", hostname = "auth.stackalchemist.app", bridge = true }`. `stackalchemist.app` is already a Cloudflare zone in the account (its nameservers are Cloudflare's; the web app is behind CF), which `data.cloudflare_zone.product` needs.

- [x] **Step 4: Skin** — tokens from the product's `globals.css` `@theme` (void `#0f172a`/`#1e293b`, ink `#f1f5f9`, electric `#4da6ff`), the product's `public/logo.svg` as the mark, and fairsquare's dark-skin rules (control borders, alerts, footer band, autofill) with this palette. Contrast ratios are in the CSS header.

- [x] **Step 5: Local Pester, file-based suites** — `Invoke-Pester tests/Themes.Tests.ps1, tests/Hostnames.Tests.ps1` (the apply test needs the compose stack; CI's `provision-test` job runs it).

- [ ] **Step 6: CI green, then merge** (qavren-auth has no standing merge authorization; owner merges).

> **Tasks 4–7 are superseded (2026-09-30) by `docs/runbooks/qavren-cutover-phase-e.md`**, which carries the measured state and guarded versions of these commands (Task 4's recipe below sets `DATABASE_URL` early and has no capture guard; the runbook sets only `DATABASE_URL_MIGRATE` here and `DATABASE_URL` at the flip). Task 5 was applied that day. Follow the runbook, not the recipes below.

### Task 4 (OWNER): prod schema + `Prod` secrets

Needed before phase E, not before. From a shell in `C:\Users\steve\projects\qavren-db` with `.env` loaded (`mise exec -- ...` or the PowerShell loop from Task 1):

```powershell
pwsh tools/provision-app.ps1 -App stackalchemist -Env prod -Apply
```

Then, with the printed lines still on screen, from any shell:

```bash
printf '%s?sslmode=require' '<pooler_url>'  | gh secret set DATABASE_URL         --repo stevenfackley/StackAlchemist --env Prod
printf '%s?sslmode=require' '<session_url>' | gh secret set DATABASE_URL_MIGRATE --repo stevenfackley/StackAlchemist --env Prod
```

The classifier refuses prod DB provisioning from this session, which is why this task is yours.

### Task 5 (OWNER): edge apply for `auth.stackalchemist.app`

After qavren-auth's PR merges (the tfvars entry must be on the branch you apply from). From `C:\Users\steve\projects\qavren-auth`, with `infra/terraform.tfvars` in place:

```powershell
terraform -chdir=infra plan -out=tfplan-hostnames   # expect: 1 CNAME create, tunnel config in-place (one ingress rule), 1 redirect rule change, 0 destroys
terraform -chdir=infra apply tfplan-hostnames
```

Verify: `curl -sI https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration | head -1` → `HTTP/2 200` once Task 7 has applied the realm (404 before that is expected).

### Task 6 (OWNER): Google Cloud Console (no API for this)

Shared OAuth client `qavren-auth` (GCP project `qavren-auth`) → Authorized redirect URIs, add both:

- `https://auth.stackalchemist.app/realms/stackalchemist/broker/google/endpoint`
- `https://auth.qavrensolutions.com/realms/stackalchemist-dev/broker/google/endpoint`

OAuth consent screen → Authorized domains: add `stackalchemist.app` if missing.

### Task 7 (OWNER): apply the realms and ship the skin

From `C:\Users\steve\projects\qavren-auth` on `main` after the PR merges:

```powershell
pwsh infra/update-realms.ps1 -Realm stackalchemist -Themes      # realm + the themes archive (login skin)
pwsh infra/update-realms.ps1 -Realm stackalchemist-dev
```

`-Themes` recreates the Keycloak container so `start` re-reads the mounted themes: about 40 s of 503s at the edge for every realm, so run it once, off-hours, and put the dev realm in the second, restart-free run.

Verify:

```bash
curl -s https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration | jq '.issuer'
# "https://auth.stackalchemist.app/realms/stackalchemist"
```

Open `https://auth.stackalchemist.app/realms/stackalchemist/account` in a browser: the login page should be void-dark with the hexagon mark and "StackAlchemist" header, a Google button, and a "Register" link. Don't create an account on the prod realm yet; the dev realm at `https://auth.qavrensolutions.com/realms/stackalchemist-dev/account` is the one to poke.

## Exit criteria

- [x] `stackalchemist` schema + role on `qavren-db-test`; `DATABASE_URL` / `DATABASE_URL_MIGRATE` in StackAlchemist `Test`
- [ ] qavren-db #41 and the qavren-auth realm PR merged
- [ ] `auth.stackalchemist.app` serves the realm's discovery document with that issuer
- [ ] Google redirect URIs registered
- [ ] Prod schema + `Prod` secrets (can trail; needed by phase E)

Phase B (data) can start as soon as the first line is true, which it is.
