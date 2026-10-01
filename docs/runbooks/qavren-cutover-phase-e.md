# Phase E — prod cutover to qavren-db + Qavren Auth

Owner-run checklist for the last phase of the re-platform
(`docs/superpowers/plans/2026-09-28-qavren-replatform.md`). Phases B, C and D
are on `main` and deployed; prod still runs in **Supabase mode** because the
Prod environment lacks the secrets that select the other mode. This runbook
flips it, verifies it, and starts the retirement clock. It does not repeat the
mechanics already written down; it points at them:

- `docs/runbooks/qavren-auth.md` — what `QAVREN_AUTH_URL` does, the env
  contract, troubleshooting, session lifetime.
- `docs/runbooks/qavren-db-migrations.md` — the two URLs (pooler vs session),
  the prod migrate step, traps.
- `docs/superpowers/plans/2026-09-28-qavren-replatform-A-platform.md` — the
  platform tasks; its Tasks 4–7 are superseded by §1 below.

Every command is idempotent or has a stated rollback, and every section ends
with a verification line. §2 refuses to start until the §1.7 table is green.

## The one rule

Prod has exactly two legitimate shapes:

- **Supabase mode:** neither `DATABASE_URL` nor `QAVREN_AUTH_URL` is set.
- **Qavren mode:** both are set, plus `DATABASE_URL_MIGRATE` and `AUTH_SECRET`.

`DATABASE_URL` and `QAVREN_AUTH_URL` are set **together, in §2, in one deploy**.
The other two may exist earlier: `AUTH_SECRET` is inert alone, and
`DATABASE_URL_MIGRATE` alone only makes the deploy pre-apply migrations to a
schema nothing reads yet. Any other shape is refused by the deploy's first
step (see §2) because it either boots the web into its consistency throw,
runs the store against a schema nothing migrates, or writes prod rows under
Supabase identities that no Keycloak account will ever own. Any push to `main`
outside `paths-ignore` deploys prod, so a half-set of secrets is not "waiting
for the flip"; it is the next deploy's configuration.

## State measured 2026-09-30, updated 2026-10-01 01:30 UTC

| Prerequisite | State | Owner action |
|---|---|---|
| qavren-db `stackalchemist` schema on `qavren-db-test` | done 2026-09-29 | — |
| qavren-db `stackalchemist` schema on `qavren-db-prod` | **provisioned 2026-10-01** (`status=created`; schema + role verified read-only; 0 tables until the first migrate). Credentials in `provision-prod.txt` in the vault directory | — |
| qavren-db manifest `apps/stackalchemist.yaml` (qavren-db #41) | merged 2026-09-30, lists `[test, prod]`; both targets now exist, so the nightly backup passes. Notes update: qavren-db #42 | — |
| Prod secret `DATABASE_URL_MIGRATE` | **set 2026-10-01** (session pooler `:5432` on the `*.pooler.supabase.com` host, so IPv4; `?sslmode=require`) | dispatch one deploy (§1.1) |
| Prod secret `DATABASE_URL` | absent (set it in §2, not before) | §2 |
| Prod secret `AUTH_SECRET` | **set 2026-09-30** (minted `openssl rand -base64 32`; inert until `QAVREN_AUTH_URL` exists) | — |
| Prod secret `QAVREN_AUTH_URL` | absent (set it in §2, not before) | §2 |
| qavren-auth realms `stackalchemist` + `stackalchemist-dev`, hostname entry, login skin (qavren-auth #164) | merged 2026-09-30 (`f2f0ae8`) | — |
| Terraform edge for `auth.stackalchemist.app` | **applied 2026-09-30** (1 added, 2 changed, 0 destroyed, after the owner granted the apply) | — |
| `auth.stackalchemist.app` | resolves; `/realms/master/.well-known/openid-configuration` 200, `/realms/stackalchemist/…` 404 until §1.4; shared host 308s `/realms/stackalchemist/*` to it | — |
| Realms `stackalchemist` + `stackalchemist-dev` on the prod Keycloak | **applied 2026-10-01** without `-Themes` (config-cli only, no Keycloak restart): issuer `https://auth.stackalchemist.app/realms/stackalchemist`, 2 signing keys, login page "Sign in to StackAlchemist" with Google + Register on Keycloak's **built-in** theme; dev realm issuer `https://auth.qavrensolutions.com/realms/stackalchemist-dev` | ship the skin off-hours (§1.4) |
| Google redirect URIs for both realms | not registered | §1.3 |
| `www.stackalchemist.app` | **301 to the apex since 2026-10-01** (zone redirect ruleset created via the API with one rule, `http.host eq "www.stackalchemist.app"` → `concat("https://stackalchemist.app", http.request.uri.path)`, query preserved; rule id `21701121e05140b9bada3d8f58f8ac03`). Verified: `/login?x=1` and `/generate/abc?tier=0` keep path and query | — |
| `sa-prod-tunnel` ingress | **host-only** (checked via API 2026-10-01): `stackalchemist.app` and `www.stackalchemist.app` → `http://sa-reverse-proxy:80`, catch-all 404 | — |
| Prod app | Supabase mode: `/api/healthz` 200, `/api/auth/session` 404, `/login` renders the Supabase client; last deploy 36766447657 green; the last four deploys took 2–4 minutes each | — |
| `deploy-prod.yml` guards (preflight with shape checks, mode check) | PR #439 | — |

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

## 1. Pre-flight (owner)

### 1.1 Provision the prod schema; set `DATABASE_URL_MIGRATE` only — do this first

**Why first:** qavren-db's nightly backup (`nightly-backup.yml`, 07:17 UTC)
checks every committed manifest against the live schemas on **both** targets
and throws `stackalchemist not provisioned on prod` after uploading the
others. The manifest was merged 2026-09-30, so from **2026-10-01 07:17 UTC**
the prod backup job is red every night until this runs (the other apps' dumps
still upload; it is the job status that lies). qavren-db's `docs/runbook.md`
("Every committed manifest must exist on BOTH targets") records fairsquare
doing exactly this for ten days.

**Why only the migrate URL:** see "The one rule". The pooler URL is captured
to the vault file now and moved into `DATABASE_URL` in §2.

The provisioner prints the operator contract once (`app=`, `env=`, `status=`,
and on creation `password=`, `session_url=`, `pooler_url=`). It prints
`pooler_url=` only when **all three** prod variables are in `.env`:
`QAVREN_DB_PROD_ADMIN_URL`, `QAVREN_DB_PROD_PROJECT_REF`,
`QAVREN_DB_PROD_POOLER_HOST` (the local `.env` has them). A re-run prints
`status=updated` and no password, so the capture below refuses to overwrite
an existing file; if the password is ever lost, the recovery is
`-RotatePassword` (prints a new one, and every stored URL must be re-set).

From a pwsh prompt in `C:\Users\steve\projects\qavren-db` on `main` with
`.env` loaded (the Task 1 loop or `mise exec --`):

```powershell
git switch main; git pull --ff-only
$f = "$env:USERPROFILE\stackalchemist-credentials-vault-20260929\provision-prod.txt"
if (Test-Path $f) { Write-Error "refusing to overwrite $f (a re-run prints no password; use -RotatePassword if it is lost)" } else { pwsh tools/provision-app.ps1 -App stackalchemist -Env prod -Apply *> $f }
Select-String -Path $f -Pattern '^(app|env|status)='
(Select-String -Path $f -Pattern '^(session_url|pooler_url)=').Count   # must print 2; names only, values stay in the file
```

If the count is not 2, stop: check the three `QAVREN_DB_PROD_*` variables,
delete the file only if `status=created` is NOT in it, and re-run. Then, from
Git Bash, set the session-mode URL only, with a shape guard so an empty
capture can never become a secret:

```bash
V="$USERPROFILE/stackalchemist-credentials-vault-20260929/provision-prod.txt"
S=$(sed -n 's/^session_url=//p' "$V" | tr -d '\r')
case "$S" in postgres://*:5432/postgres|postgresql://*:5432/postgres) ;; *) echo "session_url missing or not a :5432 URL — stop"; false;; esac \
  && printf '%s?sslmode=require' "$S" | gh secret set DATABASE_URL_MIGRATE --repo stevenfackley/StackAlchemist --env Prod \
  && gh secret list --repo stevenfackley/StackAlchemist --env Prod | awk '{print $1}' | grep -E '^(DATABASE_URL|DATABASE_URL_MIGRATE|QAVREN_AUTH_URL|AUTH_SECRET)$'
```

Expect exactly two names, `DATABASE_URL_MIGRATE` and `AUTH_SECRET`. Then
deploy once, now, so the first use of the new secret happens on a run you are
watching rather than on whatever push lands next:

```bash
gh workflow run deploy-prod.yml --repo stevenfackley/StackAlchemist --ref main
```

Expect `Deploy mode: Supabase (store + auth)` with the warning that the migrate
URL is set without a store, `qavren-db migrations applied` (the migrate step
applies `0000_init` and `0001_functions` to the empty schema), and
`Prod verified in Supabase Auth mode`. Every later deploy repeats the warning
and no-ops the migrations; the flip's run does too. If the migrate step cannot
connect (one known way: the session URL's host is the IPv6-only
`db.<ref>.supabase.co` form and the ARM runner has no IPv6), every deploy is
blocked at that step until it is fixed: delete `DATABASE_URL_MIGRATE` to
unblock, and see qavren-db `docs/runbook.md` for the session-pooler form of
the URL before setting it again.

Afterwards, in qavren-db, update the `notes:` of `apps/stackalchemist.yaml`
(prod provisioned date) on a branch + PR; CI's manifest tests read the file.

### 1.2 Edge for `auth.stackalchemist.app` — DONE 2026-09-30

Applied from the qavren-auth checkout (`main` at `f2f0ae8`) with
`terraform -chdir=infra apply tfplan-hostnames`: `1 added, 2 changed, 0
destroyed`. The three changes: a proxied CNAME `auth` in zone
`stackalchemist.app` to the shared tunnel; one tunnel ingress rule
`auth.stackalchemist.app → http://keycloak:8080` (catch-all 404 still last);
one 308 bridge rule for `/realms/stackalchemist/` on `auth.qavrensolutions.com`.
Measured right after:

```text
https://auth.stackalchemist.app/realms/master/.well-known/openid-configuration         -> 200
https://auth.stackalchemist.app/realms/stackalchemist/.well-known/openid-configuration -> 404  (until §1.4)
https://auth.qavrensolutions.com/realms/stackalchemist/protocol/openid-connect/auth?x=1 -> 308 -> https://auth.stackalchemist.app/realms/stackalchemist/protocol/openid-connect/auth?x=1
```

If the hostname ever needs re-creating: `terraform -chdir=infra plan` in
qavren-auth must show `0 to add` for this entry; anything else means the
Cloudflare side drifted, and `docs/hostname-cutover-runbook.md` there applies.

### 1.3 Google Cloud Console (no API for this)

GCP project `qavren-auth` → APIs & Services → Credentials → OAuth 2.0 Client
IDs → `qavren-auth` → Authorized redirect URIs, add both:

- `https://auth.stackalchemist.app/realms/stackalchemist/broker/google/endpoint`
- `https://auth.qavrensolutions.com/realms/stackalchemist-dev/broker/google/endpoint`

Then OAuth consent screen (Branding) → Authorized domains: add
`stackalchemist.app` if it is not there. Google accepts the change
immediately. `docs/google-oauth-setup.md` in qavren-auth has the details.

### 1.4 Apply the realms and ship the login skin (off-hours)

**Realms applied 2026-10-01 without `-Themes`** (`./update-realms.ps1 -Realm stackalchemist`, then `-Realm stackalchemist-dev`): config-cli only, no Keycloak restart, other realms unaffected. Keycloak serves its built-in login theme until the skin ships, because the realm names a theme the server does not have yet. What remains is the one restart that ships the skin, off-hours, from `qavren-auth/infra` in a pwsh prompt: `./update-realms.ps1 -Realm stackalchemist -Themes`. The original instructions follow for reference.

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
`/api/auth/`, `/api/csp-report` and `/api/healthz` go to `sa-web`, everything
else under `/api/` to the Engine. A path rule in the tunnel would bypass that.
If every row is host-only, nothing to do.

### 1.7 Pre-flight table

Do not start §2 until each line is true:

- [x] §1.1 `gh secret list … --env Prod` shows `DATABASE_URL_MIGRATE` and `AUTH_SECRET`, and NOT `DATABASE_URL`; `provision-prod.txt` has 2 URL lines (2026-10-01)
- [ ] §1.1 the deploy dispatched right after is green with `qavren-db migrations applied` and `Prod verified in Supabase Auth mode`
- [x] §1.2 `auth.stackalchemist.app` resolves (done 2026-09-30)
- [ ] §1.3 both Google redirect URIs saved; `stackalchemist.app` an authorized domain
- [x] §1.4 discovery issuer is `https://auth.stackalchemist.app/realms/stackalchemist` (2026-10-01)
- [ ] §1.4 login page shows the skin (`./update-realms.ps1 -Realm stackalchemist -Themes`, off-hours; cosmetic, does not block the flip)
- [x] §1.5 `www.` → 301 to the apex (2026-10-01)
- [x] §1.6 tunnel rows are host-only (2026-10-01)
- [ ] The last `deploy-prod` run is green and its summary says `Prod verified in Supabase Auth mode` — proves the mode check works before you rely on it for the flip (the merge of PR #439 produced the first such run)

## 2. The flip (two secrets, one deploy)

Set `DATABASE_URL` (the pooler URL from the §1.1 vault file) and
`QAVREN_AUTH_URL` back to back, then dispatch. The deploy reads all four;
`AUTH_URL` derives from `NEXT_PUBLIC_APP_URL` in compose, `QAVREN_REALM`
defaults to `stackalchemist`, nginx already routes `/api/auth/` to the web
(phase C). Do not let a `main` push land between the two `gh secret set`
lines; they take seconds.

```bash
V="$USERPROFILE/stackalchemist-credentials-vault-20260929/provision-prod.txt"
P=$(sed -n 's/^pooler_url=//p' "$V" | tr -d '\r')
case "$P" in postgres://*:6543/postgres|postgresql://*:6543/postgres) ;; *) echo "pooler_url missing or not a :6543 URL — stop"; P=;; esac
if [ -n "$P" ]; then
  printf '%s?sslmode=require' "$P" | gh secret set DATABASE_URL --repo stevenfackley/StackAlchemist --env Prod \
  && printf 'https://auth.stackalchemist.app' | gh secret set QAVREN_AUTH_URL --repo stevenfackley/StackAlchemist --env Prod \
  && gh workflow run deploy-prod.yml --repo stevenfackley/StackAlchemist --ref main \
  && sleep 20 \
  && RUN=$(gh run list --repo stevenfackley/StackAlchemist --workflow deploy-prod.yml -L 1 --json databaseId --jq '.[0].databaseId') \
  && echo "watching run $RUN" \
  && gh run watch --repo stevenfackley/StackAlchemist "$RUN" --exit-status
fi
```

Everything after the guard is one `&&` chain on purpose: if the guard prints
"stop", nothing is set and nothing is watched (an unchained `gh run watch`
would happily report the previous, green, Supabase-mode run).

What the run does, in order, and what to expect (whole run: 2–4 minutes, like
every recent deploy, plus under a minute for the migrator):

1. **Preflight — re-platform secrets are consistent** (first step, before
   checkout): `shape ok` for the three URLs, then
   `Deploy mode: qavren-db store + Qavren Auth`. Any `::error` here means a
   secret is missing or malformed; **nothing has changed on the box**. Fix the
   secret and re-run.
2. **Apply Supabase migrations (prod)**: `NOT applied (PROD_SUPABASE_DB_URL unset)`. Expected; that secret never existed in Prod.
3. **Apply qavren-db migrations (prod)**: applies `0000_init` and
   `0001_functions` to the `stackalchemist` schema, or no-ops if a deploy
   between §1.1 and now already did; summary `qavren-db migrations applied`. A
   failure here aborts before the build; nothing has changed on the box.
4. Build, maintenance page, swap, health checks (unchanged from every other
   deploy). **If the run goes red at "Deploy production stack" or "Wait for
   health checks", prod is down behind the maintenance page: go to §5 now**,
   do not debug first. The likely causes are the pooler refusing the
   credentials or the boot assert; both show in the step's `docker compose
   logs` tail.
5. **Verify site is reachable**: healthz 200, then
   `prod is in Qavren Auth mode (/api/auth/session → 200; /login marker present)`.
   If this step fails, the stack is up and serving but in the other mode; §5.

## 3. Verify (owner, in a browser, about 15 minutes)

Do these in order; each proves one seam.

1. **Mode:** `curl -s https://stackalchemist.app/api/auth/session` → `null`
   with HTTP 200. Anonymous `https://stackalchemist.app/dashboard` →
   `/login?returnTo=/dashboard`; the page says sign-in happens at
   `auth.stackalchemist.app` (one button).
2. **Register the owner account** through the app: `/register` → Keycloak
   registration on `auth.stackalchemist.app` → verification mail (fleet SES,
   sender "StackAlchemist") → back on `/dashboard` with the email shown and a
   quota read from Postgres. This is the first `profiles` row in the prod
   schema. Google sign-in can be tried as a second account later; it needs
   §1.3.
3. **Tier 0 generation end to end:** Simple mode → submit → status page
   (polling, no Realtime) → build log streams → download the ZIP from R2. The
   Engine logs nothing special when it selects the Postgres store, so prove
   the store from the data. From Git Bash, with the session URL taken from
   the §1.1 vault file (no owner shell has `DATABASE_URL_MIGRATE` set; do this
   before §6 deletes the file):
   `V="$USERPROFILE/stackalchemist-credentials-vault-20260929/provision-prod.txt"; DBM="$(sed -n 's/^session_url=//p' "$V" | tr -d '\r')?sslmode=require"; psql "$DBM" -c "select count(*) from stackalchemist.generations"`
   → the generation you just ran (and the `profiles` row from step 2 via
   `select count(*) from stackalchemist.profiles`).
4. **Money path:** buy the cheapest paid tier. If the Prod Stripe keys are live
   keys this is a real charge; refund it from the Stripe dashboard afterwards,
   which exercises `StripeRefundService` too. Expect: the dashboard shows the
   paid generation credit, and on the box
   `docker compose -p stackalchemist-prod -f /opt/stackalchemist-prod/docker-compose.prod.yml logs --tail 300 sa-engine | grep -E 'Stripe event (received|.* skipped: duplicate)'`
   shows one `received` for the event; replaying it from the Stripe dashboard
   adds a `skipped: duplicate` line and no second transaction
   (`select count(*) from stackalchemist.transactions` stays 1).
5. **Sign out:** Sign Out → lands on `/`;
   `https://auth.stackalchemist.app/realms/stackalchemist/account` asks for a
   password again (realm session ended);
   `curl -s https://stackalchemist.app/api/auth/session` → `null`.
6. **Quota:** a second Tier 0 submission over the free quota is refused with
   the quota message (the `enforce_free_generation_quota` trigger lives in the
   schema).

If 1–3 pass, the cutover is done. 4–6 failing are bugs to fix forward, not
reasons to roll back, unless the money path double-charges.

## 4. Old Supabase project: pause and start the 7-day clock

Only after §3.1–3.3 pass. Playbook step (qavren-db `docs/migration-playbook.md`
§6): the source is never written to again; delete after 7 clean days.

Before pausing: **the test mirror still authenticates against Supabase**
(`deploy-test.yml`; the Test environment has `SUPABASE_*` and no
`QAVREN_AUTH_URL`; the `stackalchemist-dev` realm only knows `localhost:3000`).
If its `SUPABASE_URL` is the prod project `ctqhwykryoglhdwatljt`, pausing
breaks the mirror's sign-in until phase F gives it a realm. The project ref is
baked into the mirror's client bundle, not into the HTML, so scan the chunks
(with the mirror's Basic Auth credentials):

```bash
U='<basic-auth-user>'; P='<basic-auth-pass>'; B='https://test.stackalchemist.app'
curl -su "$U:$P" "$B/login" | grep -oE '/_next/static/chunks/[^"]+\.js' | sort -u \
  | while read -r c; do curl -su "$U:$P" "$B$c"; done | grep -oE '[a-z]{20}\.supabase\.co' | sort -u
```

Against prod (no Basic Auth) the same scan prints `ctqhwykryoglhdwatljt.supabase.co`,
which is how you know the scan works. If the mirror prints that ref too,
either accept a dead test-mirror sign-in until phase F, or defer the pause. An
empty result means the scan failed (wrong credentials, wrong host), not "safe".

Supabase dashboard → project `ctqhwykryoglhdwatljt` → Project Settings →
General → **Pause project**. Data is kept; unpausing is one click. Record the
date here:

- Paused: ____-__-__ → delete on or after ____-__-__ (+7 days), phase F.

Do **not** touch the CI project `cdlefpvsvyepofsboepc`; CI no longer uses it
(phase D) and phase F deletes it.

## 5. Rollback

**Fast path (prod is down behind the maintenance page, or in the wrong
mode):** the images are already on the box, so a recreate with the three
variables blanked takes well under a minute. On the box (SSM session or SSH):

```bash
cd /opt/stackalchemist-prod
sudo sed -i -E 's/^(DATABASE_URL|DATABASE_URL_MIGRATE|QAVREN_AUTH_URL)=.*/\1=/' .env   # .env is root-owned (the deploy sudo-cp's it)
docker compose -p stackalchemist-prod -f docker-compose.prod.yml up -d --force-recreate sa-web sa-engine
docker rm -f sa-reverse-proxy 2>/dev/null || true    # the maintenance page is a plain `docker run` container holding that name
docker compose -p stackalchemist-prod -f docker-compose.prod.yml up -d reverse-proxy
docker network connect stackalchemist-prod sa-tunnel 2>/dev/null || true   # re-attach the tunnel, as the deploy does
curl -s https://stackalchemist.app/api/auth/session -o /dev/null -w '%{http_code}\n'   # 404 = Supabase mode is back
```

Prefix the `docker` lines with `sudo` if the session user (`ssm-user`, say)
is not in the `docker` group. In the wrong-mode case (prod serving, no
maintenance page) the `docker rm -f` removes the live nginx, so expect a few
seconds of 502 until `up -d reverse-proxy` brings it back, the same as a
normal deploy swap. The images are the ones the flip built; the
mode is chosen at runtime, so recreating with blanked variables is enough,
and nginx resolves its upstreams per request and needs no restart.

Then make the secrets match, or the next `main` push flips it again:

```bash
for s in QAVREN_AUTH_URL DATABASE_URL DATABASE_URL_MIGRATE; do gh secret delete "$s" --repo stevenfackley/StackAlchemist --env Prod; done
gh workflow run deploy-prod.yml --repo stevenfackley/StackAlchemist --ref main
```

**Slow path (prod is serving, you just want Supabase mode back):** the second
block alone. It is a full deploy (the build step always runs, warm but 2–4
minutes). `AUTH_SECRET` may stay (inert). The preflight prints
`Deploy mode: Supabase (store + auth)` and the last step must say
`Prod verified in Supabase Auth mode`.

Anything written to qavren-db meanwhile stays there; if the Supabase project
was paused in §4, unpause it first. A rollback after §4 also means the
re-registered accounts vanish from the users' point of view (they live on the
realm, which is untouched); the Supabase accounts come back.

## 6. Bookkeeping after the flip

- Append the execution record (date, run id, the §3 results) to this file and
  to the vault note `Projects/stack-alchemist/stack-alchemist.md`.
- Delete `provision-prod.txt` and `provision-test.txt` from the vault
  directory (plaintext prod and test DB passwords) once §3 passes, or move
  them into the password manager. The secrets live in GitHub from then on;
  a lost password is `-RotatePassword`, not a file.
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

- `terraform … apply` in qavren-auth (classifier: protected-scope IaC apply) — §1.2; the owner granted it the same day and it ran.
- `pwsh tools/provision-app.ps1 -Env prod -Apply` in qavren-db (classifier: prod DB provisioning) — §1.1.
- `infra/update-realms.ps1` (SSM `send-command` to the prod box) — §1.4.
- `gh workflow run`, `gh secret delete`, `gh pr merge`, `gh api -X …` (guard-writes hook) — §2, §5, and:
  `gh workflow run ci.yml --repo stevenfackley/StackAlchemist --ref main` (exercises `db-pooler-nightly` and the nightly specs once, phase D follow-up);
  `gh secret delete CI_SUPABASE_DB_URL --repo stevenfackley/StackAlchemist --env Test` (unused since phase D).
