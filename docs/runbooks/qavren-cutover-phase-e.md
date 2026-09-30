# Phase E — prod cutover to qavren-db + Qavren Auth

Owner-run checklist for the last phase of the re-platform
(`docs/superpowers/plans/2026-09-28-qavren-replatform.md`). Phases B, C and D
are on `main` and deployed; prod still runs in **Supabase mode** because the
Prod environment lacks the secrets that select the other mode. This runbook
flips it, verifies it, and starts the retirement clock. It does not repeat the
mechanics already written down; it points at them:

- `docs/runbooks/qavren-auth.md` — what `QAVREN_AUTH_URL` does, the env
  contract, the flip order, troubleshooting, session lifetime.
- `docs/runbooks/qavren-db-migrations.md` — the two URLs (pooler vs session),
  the prod migrate step, traps.
- `docs/superpowers/plans/2026-09-28-qavren-replatform-A-platform.md` — the
  platform tasks (4 to 7 are the owner's and are restated below with the state
  measured on 2026-09-30).

Everything here is idempotent or has a stated rollback. Do the sections in
order; each has a verification line, and the flip section refuses to start
until the pre-flight table is all green.

## State measured 2026-09-30 (after phase D deployed)

| Prerequisite | State | Owner action |
|---|---|---|
| qavren-db `stackalchemist` schema on `qavren-db-test` | done 2026-09-29 | — |
| qavren-db `stackalchemist` schema on `qavren-db-prod` | **missing** | §1.1 |
| qavren-db manifest `apps/stackalchemist.yaml` (qavren-db #41) | merged 2026-09-30, lists `[test, prod]` | see the warning in §1.1 |
| Prod secrets `DATABASE_URL`, `DATABASE_URL_MIGRATE` | **absent** | §1.1 |
| Prod secret `AUTH_SECRET` | **set 2026-09-30** (minted `openssl rand -base64 32`; inert until `QAVREN_AUTH_URL` exists) | — |
| Prod secret `QAVREN_AUTH_URL` | absent (set it in §2, not before) | §2 |
| qavren-auth realms `stackalchemist` + `stackalchemist-dev`, hostname entry, login skin (qavren-auth #164) | merged 2026-09-30 (`f2f0ae8`) | — |
| Terraform edge for `auth.stackalchemist.app` | **planned, not applied**: `terraform plan` on 2026-09-30 = 1 add (CNAME), 2 in-place (tunnel ingress, bridge ruleset), 0 destroy; the apply is classifier-blocked for the assistant | §1.2 |
| `auth.stackalchemist.app` | no DNS (curl exit 6 / HTTP 000) | §1.2 |
| Realm `stackalchemist` on the prod Keycloak | not applied (`auth.qavrensolutions.com/realms/stackalchemist-dev` → 404 too) | §1.4 |
| Google redirect URIs for both realms | not registered | §1.3 |
| `www.stackalchemist.app` | **serves the app (HTTP 200, no redirect)** — must 301 to the apex before the flip | §1.5 |
| Prod app | Supabase mode: `/api/healthz` 200, `/api/auth/session` 404, `/login` renders the Supabase client; last deploy 36766447657 green | — |
| `deploy-prod.yml` guards (preflight, Drizzle drift guard, mode check) | this PR | — |

Decisions carried in (parent plan §Decisions, unchanged): **fresh provision,
no data copy** (1 Supabase user, 0 transactions, 18 owner test generations —
owner may veto if any generation must survive; then the qavren-db playbook
§2–§4 copy applies and `generations.user_id` must be remapped); sign-in is
email/password + Google; Realtime is already polling.

One deviation from the parent plan's step E.2 ("remove the Supabase secrets
from the box"): **keep every Supabase secret in both environments through
phase E.** In Qavren mode nothing reads them (the web picks the store and the
auth mode from `DATABASE_URL` / `QAVREN_AUTH_URL`; the Engine picks Postgres
when `DATABASE_URL` is set), demo mode cannot auto-enable in production
(`runtime-config.ts` excludes `NODE_ENV=production`), `deploy-test.yml` still
consumes them, and removing them is bundled with the code deletions in phase F.
Removing them now buys nothing and adds a variable to a deploy that already
changes four.

## 1. Pre-flight (owner; order matters only where stated)

### 1.1 Provision the prod schema, then the two Prod secrets — do this first

**Why first:** qavren-db's nightly backup (`nightly-backup.yml`, 07:17 UTC)
checks every committed manifest against the live schemas on **both** targets
and throws `stackalchemist not provisioned on prod` after uploading the
others. The manifest was merged 2026-09-30, so from **2026-10-01 07:17 UTC**
the prod backup job is red every night until this runs (the dumps of the other
apps still upload; it is the job status that lies). The runbook in qavren-db
(`docs/runbook.md`, "Every committed manifest must exist on BOTH targets")
records fairsquare doing exactly this for ten days.

From a pwsh prompt in `C:\Users\steve\projects\qavren-db` on `main` with
`.env` loaded (it holds `QAVREN_DB_PROD_ADMIN_URL`; the Task 1 loop or
`mise exec --`):

```powershell
git switch main; git pull --ff-only
pwsh tools/provision-app.ps1 -App stackalchemist -Env prod -Apply *> "$env:USERPROFILE\stackalchemist-credentials-vault-20260929\provision-prod.txt"
Get-Content "$env:USERPROFILE\stackalchemist-credentials-vault-20260929\provision-prod.txt" | Select-String '^(app|env|status)='
```

The file holds the operator contract (`app=`, `env=`, `status=`, and once:
`password=`, `session_url=`, `pooler_url=`). The password prints exactly once;
the vault directory is the same one Task 1 used for test. Then, without the
values touching the terminal:

```bash
V="$USERPROFILE/stackalchemist-credentials-vault-20260929/provision-prod.txt"
printf '%s?sslmode=require' "$(sed -n 's/^pooler_url=//p'  "$V")" | gh secret set DATABASE_URL         --repo stevenfackley/StackAlchemist --env Prod
printf '%s?sslmode=require' "$(sed -n 's/^session_url=//p' "$V")" | gh secret set DATABASE_URL_MIGRATE --repo stevenfackley/StackAlchemist --env Prod
gh secret list --repo stevenfackley/StackAlchemist --env Prod | awk '{print $1}' | grep -E '^(DATABASE_URL|DATABASE_URL_MIGRATE|AUTH_SECRET)$'
```

Expect the three names. Runtime → pooler `:6543` (the Drizzle client already
uses `prepare: false`); migrations → session `:5432`. Both need
`?sslmode=require` (the Test-environment values carry it).

Do **not** set `QAVREN_AUTH_URL` yet. Setting only these two puts the next
prod deploy into "qavren-db store + Supabase Auth" (the preflight step warns:
rows written under Supabase identities do not carry over). That is harmless
for a deploy that happens before §2 but pointless; nothing in this runbook
triggers a deploy before the flip.

Afterwards, in qavren-db, update the `notes:` of `apps/stackalchemist.yaml`
(prod provisioned date) on a branch + PR; CI's manifest tests read the file.

### 1.2 Apply the edge for `auth.stackalchemist.app`

The qavren-auth checkout at `C:\Users\steve\projects\qavren-auth` is on
`main` at `f2f0ae8` (pulled 2026-09-30), providers are initialised, and a
saved plan sits at `infra/tfplan-hostnames`. A saved plan is valid only while
the state has not moved; if the apply below refuses it, re-plan.

```powershell
Set-Location C:\Users\steve\projects\qavren-auth
terraform -chdir=infra apply tfplan-hostnames
# if refused as stale:
terraform -chdir=infra plan -out=tfplan-hostnames   # expect: 1 to add, 2 to change, 0 to destroy
terraform -chdir=infra apply tfplan-hostnames
```

What the three changes are (verified in the 2026-09-30 plan): a proxied CNAME
`auth` in zone `stackalchemist.app` to the shared tunnel; the tunnel config
gains one ingress rule `auth.stackalchemist.app → http://keycloak:8080` with
the catch-all 404 still last; the bridge ruleset gains one 308 rule for
`/realms/stackalchemist/` on `auth.qavrensolutions.com`. The many `~` lines on
existing rules are Terraform re-rendering an ordered list after an insertion,
not changes to those rules.

Verify (a 404 body is right until §1.4 applies the realm):

```bash
curl -sI https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration | head -1
# HTTP/2 404   (HTTP 000 means DNS has not been created)
curl -sI https://auth.qavrensolutions.com/realms/stackalchemist/ | grep -iE '^HTTP|^location'
# HTTP/2 308  location: https://auth.stackalchemist.app/realms/stackalchemist/
```

### 1.3 Google Cloud Console (no API for this)

GCP project `qavren-auth` → APIs & Services → Credentials → OAuth 2.0 Client
IDs → `qavren-auth` → Authorized redirect URIs, add both:

- `https://auth.stackalchemist.app/realms/stackalchemist/broker/google/endpoint`
- `https://auth.qavrensolutions.com/realms/stackalchemist-dev/broker/google/endpoint`

Then OAuth consent screen (Branding) → Authorized domains: add
`stackalchemist.app` if it is not there. No verification needed; Google
accepts the change immediately. `docs/google-oauth-setup.md` in qavren-auth
has the screenshots-in-words.

### 1.4 Apply the realms and ship the login skin (off-hours)

`-Themes` recreates the Keycloak container so `start` re-reads the mounted
themes: **about 40 s of 503s at the edge for every realm on the box**
(haulcall, squarelog, recharacter, trailtold, gavel-suite, fairsquare, …).
Run it once, off-hours, and put the dev realm in the second, restart-free
run. Run the script **in-process** from a pwsh prompt (an outer
`pwsh ./update-realms.ps1 …` mangles list parameters; the script header says
why). Needs `aws` CLI auth (the `terraform-local` IAM user works from this
machine) and the terraform state in `infra/` (it reads `instance_id`).

```powershell
Set-Location C:\Users\steve\projects\qavren-auth\infra
./update-realms.ps1 -Realm stackalchemist -Themes
./update-realms.ps1 -Realm stackalchemist-dev
```

The realm files reference `$(env:GOOGLE_CLIENT_ID)`, `$(env:GOOGLE_CLIENT_SECRET)`
and the `QAVREN_SMTP_*` pair; all are fleet-wide and already in the box's
`.env.prod`, so no `-SecretName` is needed.

Verify:

```bash
curl -s https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration | jq -r '.issuer'
# https://auth.stackalchemist.app/realms/stackalchemist
curl -s https://auth.stackalchemist.app/realms/stackalchemist/protocol/openid-connect/certs | jq '.keys | length'
# >= 1
curl -s https://auth.qavrensolutions.com/realms/stackalchemist-dev/.well-known/openid-configuration | jq -r '.issuer'
# https://auth.qavrensolutions.com/realms/stackalchemist-dev
```

Open `https://auth.stackalchemist.app/realms/stackalchemist/account` in a
browser: void-dark login page, hexagon mark, "StackAlchemist" header, a Google
button, a "Register" link. **Do not create an account on the prod realm yet**
(§3 does, through the app, so the profile row and the session are created the
way users will create them). Poke the dev realm if you want to poke something.

### 1.5 `www.stackalchemist.app` must redirect to the apex

Measured 2026-09-30: `https://www.stackalchemist.app/` answers **200 and
serves the app** (an optional tunnel hostname per
`docs/advanced-docs/prod-ec2-runner-and-oidc.md`). In Qavren mode that is a
bug, not a convenience: the Auth.js same-origin check and `AUTH_URL` pin one
origin, so a user who lands on `www.` gets a callback on the apex and a
session cookie the `www.` origin never sees (`qavren-auth.md`, "One canonical
host"). Nothing manages the `stackalchemist.app` zone as code (the repo has no
`infra/`), so this is a dashboard change:

Cloudflare dashboard → zone `stackalchemist.app` → Rules → Redirect Rules →
Create rule (the "Redirect from WWW to Root" template does exactly this):
when `http.host eq "www.stackalchemist.app"` → dynamic redirect to
`concat("https://stackalchemist.app", http.request.uri.path)`, status 301,
preserve query string. Keep the DNS record for `www` (the rule needs a proxied
record to run on); removing the `www` public hostname from the tunnel is
optional and can wait.

Verify:

```bash
curl -sI 'https://www.stackalchemist.app/login?x=1' | grep -iE '^HTTP|^location'
# HTTP/2 301   location: https://stackalchemist.app/login?x=1
```

### 1.6 Tunnel ingress has no path rules

Zero Trust → Networks → Tunnels → the StackAlchemist prod tunnel → Public
Hostnames. Every row must be host-only (`stackalchemist.app`, optionally
`www.stackalchemist.app`) to `http://sa-reverse-proxy:80` (or
`http://reverse-proxy:80`); the Path column empty. nginx owns routing:
`/api/auth/` and `/api/csp-report` go to `sa-web`, everything else under
`/api/` to the Engine. A path rule in the tunnel would bypass that. If every
row is host-only, nothing to do.

### 1.7 Pre-flight table

Do not start §2 until each line is true:

- [ ] §1.1 `gh secret list … --env Prod` shows `DATABASE_URL`, `DATABASE_URL_MIGRATE`, `AUTH_SECRET`
- [ ] §1.2 `auth.stackalchemist.app` resolves (discovery 404 or 200, not 000)
- [ ] §1.3 both Google redirect URIs saved; `stackalchemist.app` an authorized domain
- [ ] §1.4 discovery issuer is `https://auth.stackalchemist.app/realms/stackalchemist`; login page shows the skin
- [ ] §1.5 `www.` → 301 to the apex
- [ ] §1.6 tunnel rows are host-only
- [ ] The last `deploy-prod` run (the merge of this PR) is green and its summary says `Prod verified in Supabase Auth mode` — proves the mode check works before you rely on it for the flip

## 2. The flip (one secret, one deploy)

Set the fourth secret. The deploy then reads all four; `AUTH_URL` derives from
`NEXT_PUBLIC_APP_URL` in compose, `QAVREN_REALM` defaults to
`stackalchemist`, nginx already routes `/api/auth/` to the web (phase C).

```bash
printf 'https://auth.stackalchemist.app' | gh secret set QAVREN_AUTH_URL --repo stevenfackley/StackAlchemist --env Prod
gh workflow run deploy-prod.yml --repo stevenfackley/StackAlchemist --ref main
sleep 20; gh run list --repo stevenfackley/StackAlchemist --workflow deploy-prod.yml -L 1
gh run watch --repo stevenfackley/StackAlchemist "$(gh run list --repo stevenfackley/StackAlchemist --workflow deploy-prod.yml -L 1 --json databaseId --jq '.[0].databaseId')" --exit-status
```

What the run does, in order, and what to expect:

1. **Preflight — re-platform secrets are consistent**: `Deploy mode: qavren-db store + Qavren Auth`. Any `::error` here means a secret is missing and the old stack is still serving; fix the secret and re-run.
2. **Apply Supabase migrations (prod)**: `NOT applied (PROD_SUPABASE_DB_URL unset)`. Expected; that secret never existed in Prod.
3. **Apply qavren-db migrations (prod)**: applies the two Drizzle migrations (`0000_init`, `0001_functions`) to the empty `stackalchemist` schema; summary `qavren-db migrations applied`. A failure here aborts before the build; nothing has changed on the box.
4. Build, maintenance page, swap, health checks (unchanged from every other deploy).
5. **Verify site is reachable**: healthz 200, then `prod is in Qavren Auth mode (/api/auth/session → 200; /login marker present)`. If this step fails, the stack is up but in the wrong mode; go to §5.

Total is the usual deploy length plus under a minute for the migrator.

## 3. Verify (owner, in a browser, about 15 minutes)

Do these in order; each proves one seam.

1. **Mode:** `curl -s https://stackalchemist.app/api/auth/session` → `null` with HTTP 200. Anonymous `https://stackalchemist.app/dashboard` → `/login?returnTo=%2Fdashboard`; the page says sign-in happens at `auth.stackalchemist.app` (one button).
2. **Register the owner account** through the app: `/register` → Keycloak registration on `auth.stackalchemist.app` → verification mail (fleet SES, sender "StackAlchemist") → back on `/dashboard` with the email shown and a quota read from Postgres. This is the first `profiles` row in the prod schema. Google sign-in can be tried as a second account later; it needs §1.3.
3. **Tier 0 generation end to end:** Simple mode → submit → status page (polling, no Realtime) → build log streams → download the ZIP from R2. On the box, `docker compose -f /opt/stackalchemist-prod/docker-compose.prod.yml logs --tail 200 sa-engine | grep -iE 'postgres|npgsql|DATABASE_URL'` shows the Engine on the Postgres store and no `supabase.co` requests.
4. **Money path:** buy the cheapest paid tier. If the Prod Stripe keys are live keys this is a real charge; refund it from the Stripe dashboard afterwards, which exercises `StripeRefundService` too. Expect: the dashboard shows the paid generation credit, and on the box the Engine log shows `process_checkout_completed` ran once for the event (replaying the webhook from the Stripe dashboard must not add a second transaction).
5. **Sign out:** Sign Out → `/login`; `https://auth.stackalchemist.app/realms/stackalchemist/account` asks for a password again (realm session ended); `curl -s https://stackalchemist.app/api/auth/session` → `null`.
6. **Quota:** a second Tier 0 submission over the free quota is refused with the quota message (the `enforce_free_generation_quota` trigger lives in the schema).

If 1–3 pass, the cutover is done. 4–6 failing are bugs to fix forward, not
reasons to roll back, unless the money path double-charges.

## 4. Old Supabase project: pause and start the 7-day clock

Only after §3.1–3.3 pass. Playbook step (qavren-db `docs/migration-playbook.md`
§6): the source is never written to again; delete after 7 clean days.

Before pausing: **the test mirror still authenticates against Supabase**
(`deploy-test.yml`; the Test environment has `SUPABASE_*` and no
`QAVREN_AUTH_URL`; the `stackalchemist-dev` realm only knows `localhost:3000`).
If its `SUPABASE_URL` is the prod project `ctqhwykryoglhdwatljt`, pausing
breaks the mirror's sign-in until phase F gives it a realm. Check which
project the mirror is built against (the anon URL is baked into its client
bundle): with the mirror's Basic Auth credentials,
`curl -su '<user>:<pass>' https://test.stackalchemist.app/login | grep -oE '[a-z]{20}\.supabase\.co' | sort -u`
(the `app_url` input in `deploy-test.yml`). If it prints
`ctqhwykryoglhdwatljt`, either accept a dead test-mirror sign-in until phase
F, or defer the pause.

Supabase dashboard → project `ctqhwykryoglhdwatljt` → Project Settings →
General → **Pause project**. Data is kept; unpausing is one click. Record the
date here:

- Paused: ____-__-__ → delete on or after ____-__-__ (+7 days), phase F.

Do **not** touch the CI project `cdlefpvsvyepofsboepc`; CI no longer uses it
(phase D) and phase F deletes it.

## 5. Rollback

One deploy, no rebuild, no data copy back:

```bash
for s in QAVREN_AUTH_URL DATABASE_URL DATABASE_URL_MIGRATE; do gh secret delete "$s" --repo stevenfackley/StackAlchemist --env Prod; done
gh workflow run deploy-prod.yml --repo stevenfackley/StackAlchemist --ref main
```

`AUTH_SECRET` may stay (inert). The preflight prints `Deploy mode: Supabase
(store + auth)` and the last step must say `Prod verified in Supabase Auth
mode`. Anything written to qavren-db meanwhile stays there; if the Supabase
project was paused in §4, unpause it first. A rollback after §4 also means the
re-registered accounts are gone from the users' point of view (they live on
the realm, which is untouched); the Supabase accounts come back.

Rolling back only auth (`QAVREN_AUTH_URL` off, `DATABASE_URL` on) is refused by
nothing but is a bad state (Supabase identities writing Postgres rows); the
preflight warns. Remove all three.

## 6. Bookkeeping after the flip

- Append the execution record (date, run id, the §3 results) to this file and
  to the vault note `Projects/stack-alchemist/stack-alchemist.md`.
- qavren-db `apps/stackalchemist.yaml` `notes:` → prod provisioned + cut over
  dates (if not done in §1.1).
- StackAlchemist #431 (Supabase-mode gaps) becomes the phase F tracking issue;
  add: pause date, delete-after date, the test mirror's auth (needs a realm
  with the mirror's callback, or retire the mirror's sign-in), remove the
  Supabase secrets from **both** environments, delete `supabase/`, retire
  `docs/runbooks/ci-supabase-migrations.md` and `supabase-auth-smtp.md`, and
  update `CLAUDE.md`'s "Database/Auth: Supabase" line.
- Workspace memory `project_stackalchemist_state.md`: phase E date; next = F.

## Commands the assistant cannot run (hook or classifier), for the record

- `terraform … apply` in qavren-auth (classifier: protected-scope IaC apply) — §1.2.
- `pwsh tools/provision-app.ps1 -Env prod -Apply` in qavren-db (classifier: prod DB provisioning) — §1.1.
- `infra/update-realms.ps1` (SSM `send-command` to the prod box) — §1.4.
- `gh workflow run`, `gh secret delete`, `gh pr merge`, `gh api -X …` (guard-writes hook) — §2, §5, and:
  `gh workflow run ci.yml --repo stevenfackley/StackAlchemist --ref main` (exercises `db-pooler-nightly` and the nightly specs once, phase D follow-up);
  `gh secret delete CI_SUPABASE_DB_URL --repo stevenfackley/StackAlchemist --env Test` (unused since phase D).
