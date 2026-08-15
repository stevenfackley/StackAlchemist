# Supabase Auth Custom SMTP Runbook

How to move Supabase Auth off the built-in mailer and onto Resend SMTP, and
how to tell whether it worked.

Nothing in this repo reads these settings at runtime. They live on the
Supabase project, which is why this has stayed broken through several
deploys that all went green.

## The problem

Supabase Auth sends magic links, signup confirmations, email-change
confirmations and password resets from **its own mailer**, not through the
Engine's `ResendEmailService`. Those are two independent senders that happen
to share a vendor:

| Sender | Transport | Configured in | Sends |
| --- | --- | --- | --- |
| `ResendEmailService` (Engine) | Resend **HTTP API** | `RESEND_API_KEY` in `.env` | build-finished, refund notices |
| Supabase Auth | **SMTP** | Supabase project config | magic link, signup confirm, password reset |

Prod project `ctqhwykryoglhdwatljt` ("StackAlchemist-Production", West US)
has never had a custom SMTP server attached, so Supabase Auth falls back to
its built-in mailer. That mailer is capped at **2 emails per hour,
project-wide**, and Supabase documents it as best-effort with no delivery
guarantee. It exists for local development, not production.

This is bug #3 of the 2026-07-11 auth incident. Two users signing in within
the same hour is enough to exhaust the quota; from the user's side it looks
identical to "the magic link is broken", because Supabase returns success and
simply never sends. There is no error, no bounce, and no log line in this
repo — the failure is entirely invisible from inside the application.

## Prerequisites

Both are human steps requiring account access. Neither can be scripted.

### 1. `stackalchemist.app` verified as a sending domain in Resend

Resend will not relay mail claiming to be from a domain it has not verified,
and receiving servers will junk it even if it does.

1. <https://resend.com/domains> → **Add Domain** → `stackalchemist.app`.
2. Pick the region closest to prod (US West, matching the Supabase project).
3. Resend generates a set of DNS records — an MX record and TXT records for
   SPF and DKIM, on a `send.` subdomain plus a `resend._domainkey` entry.
   **Copy them verbatim from that screen.** They are account- and
   region-specific; do not reuse values from any other project.
4. Add them at the DNS host for `stackalchemist.app` (Cloudflare, same
   account that terminates the prod tunnel).

   > **Cloudflare gotcha:** mail records must be **DNS only** (grey cloud),
   > never proxied (orange cloud). A proxied MX or SPF record breaks
   > verification and the failure mode is a silent one — Resend just keeps
   > reporting the domain as unverified.

5. Back in Resend, wait for the domain to flip to **Verified**. Propagation
   is usually minutes; give it up to an hour before suspecting a typo.

### 2. An API key scoped to sending

1. <https://resend.com/api-keys> → **Create API Key**.
2. Name it `supabase-auth-smtp` so it is distinguishable in the audit log
   from the Engine's key.
3. Permission: **Sending access**. Domain: `stackalchemist.app`.
4. Copy the key (`re_…`). Resend shows it exactly once.

   > Use a **separate key** from the Engine's `RESEND_API_KEY`. They fail
   > independently and rotate on different schedules; one shared key means
   > rotating the Engine's mail silently kills sign-in.

## Applying the configuration

Two routes, writing the same seven settings — pick one, not both. Route A is a
dashboard click-path across two panels; Route B sends them in a single PATCH.
Sender address, sender name, host, port, username, password, per-user interval
and the hourly rate limit end up identical either way. Email sign-in
(`external_email_enabled`) is the one difference: Route B sets it explicitly,
whereas the dashboard exposes it separately under **Authentication → Sign In /
Providers → Email**. It is on by default, so Route A only needs it if sign-in
was previously disabled.

### Route A — Supabase dashboard (recommended for the first run)

Open <https://supabase.com/dashboard/project/ctqhwykryoglhdwatljt>.

**Authentication → Emails → SMTP Settings**, then enable **Enable Custom
SMTP**. On older dashboard builds the same panel lives at *Project Settings →
Authentication → SMTP Settings*.

Fill in exactly these values:

| Dashboard field | Value | Where it comes from |
| --- | --- | --- |
| Sender email | `noreply@stackalchemist.app` | Must be at the domain verified above |
| Sender name | `StackAlchemist` | Display name in the recipient's inbox |
| Host | `smtp.resend.com` | Fixed, same for every Resend account |
| Port number | `587` | STARTTLS. `465` (implicit TLS) also works |
| Username | `resend` | The **literal string** `resend` — not an email, not the key |
| Password | the `re_…` API key | From prerequisite 2 |
| Minimum interval between emails | `60` seconds | Per-user cooldown; leave at the default unless you have a reason |

Click **Save**.

> The Username/Password pair is the single most common mistake here. The
> username is always `resend`; the API key is the **password**. Swapping them
> produces an SMTP 535, which Supabase surfaces to the user as nothing more
> specific than "Error sending magic link email".

Then, **Authentication → Rate Limits → "Rate limit for sending emails"**:
raise it from the built-in `2` to **`100`** per hour and save.

> This field is only editable once custom SMTP is enabled. Skipping it is the
> classic half-done migration: delivery works when you test it by hand, then
> caps at 2/hour in production exactly as before. Attaching SMTP without
> raising the limit buys you nothing.

### Route B — scripted, via the Management API

`scripts/supabase-auth-smtp.mjs` applies the same settings, so the intended
configuration is reviewable in git rather than living only in someone's
browser history.

```sh
export SUPABASE_ACCESS_TOKEN=…    # https://supabase.com/dashboard/account/tokens
export SUPABASE_PROJECT_REF=ctqhwykryoglhdwatljt
export SUPABASE_AUTH_SMTP_PASS=…  # the re_… key

node scripts/supabase-auth-smtp.mjs apply --dry-run   # prints the payload, password fingerprinted
node scripts/supabase-auth-smtp.mjs apply
```

The script never prints the password, never writes it to disk, and is not
wired into any workflow — see "Why this is not in CI" below. Anything it
prints, including API error bodies, is passed through a redaction step first,
so a credential the API echoes back comes out as a fingerprint.

## Verifying

```sh
export SUPABASE_ACCESS_TOKEN=…
node scripts/supabase-auth-smtp.mjs verify
```

Exit code 0 means the live project has custom SMTP whose host, username and
sender address match the values above, an email rate limit above the built-in
floor, and email sign-in enabled. Exit 1 prints every check that failed.

Matching, not merely present, is the point: a project pointed at the wrong
relay or sending as the wrong address is broken in a way that a
"custom SMTP is on" check reports as green. Override
`SUPABASE_AUTH_SMTP_HOST` / `_USER` / `_SENDER` in the shell if you are
verifying a project that is deliberately configured differently.

**A green verify is not a delivered email.** It proves Supabase will attempt
the relay; it says nothing about whether Resend accepts the handoff or
whether the recipient's provider accepts Resend. Finish with a real send:

1. Request a magic link for an address you control at
   <https://stackalchemist.app/login>.
2. Confirm the mail arrives, and that the From address is
   `StackAlchemist <noreply@stackalchemist.app>` — not
   `noreply@mail.app.supabase.io`. The From address is the tell: if it still
   reads `supabase.io`, the config did not take.
3. Request a second link within the same minute. On the built-in mailer that
   second request is where the 2/hour cap used to bite; it should now send.
4. Click through and confirm you land signed-in on `stackalchemist.app`, not
   on `0.0.0.0:3000` (that regression is separate — PR #212 — but the
   magic-link loop is the cheapest place to catch it coming back).

Resend's <https://resend.com/emails> log shows every relayed message with its
delivery status; if Supabase says it sent and Resend shows nothing, the
credentials are wrong.

## Rolling back

Turn **Enable Custom SMTP** off in the dashboard. The project reverts to the
built-in mailer immediately — including the 2/hour cap, so treat rollback as
an outage of sign-in, not a neutral state.

If the API key is the problem, prefer minting a replacement in Resend and
updating the Password field over disabling SMTP entirely.

## Why this is not in CI

Nothing here runs automatically, by design:

- Applying the config needs a Supabase **personal access token**, which is
  account-wide and can modify every project the owner has. Parking one in
  repo secrets to save a manual step trades a rare chore for a permanent
  blast radius.
- A workflow that can repoint the auth mailer is a phishing primitive. An
  attacker with push access to a branch could redirect every password-reset
  email to a relay they control.
- `verify` is safe to run on a schedule and would have caught this drift, but
  it still needs that same token to read the config. Wiring it up is a
  deliberate decision with a real prerequisite, not a follow-up chore — so it
  is left un-wired rather than half-wired.

## Why there is no `supabase/config.toml`

The Supabase CLI supports declaring `[auth.email.smtp]` in
`supabase/config.toml`, which looks like the obvious home for this. It is not,
for this repo:

- The CLI is used here for exactly one thing — `supabase db push --db-url` in
  the `e2e-integration` job. The project is never linked, and
  `supabase config push` is never run, so a `config.toml` would be applied by
  nobody.
- A config file that looks authoritative but is applied by nothing is worse
  than no file. It drifts from the dashboard silently, and the next person to
  debug auth reads it and believes it. That is the same failure that produced
  bug #2 of the 2026-07-11 incident: a login page shipped with a Google button
  while `external_google_enabled` was false, because the code implied a
  configuration that did not exist.
- Adding a `config.toml` also changes what the CLI loads during the CI
  migration step, for no offsetting benefit.

If the project is ever properly linked and `config.toml` becomes the applied
source of truth, move this configuration there and delete Route B.

## Related

- `docs/runbooks/ci-supabase-migrations.md` — the other Supabase-side secret,
  `CI_SUPABASE_DB_URL`
- `scripts/supabase-auth-smtp.mjs` — apply/verify implementation
- `.env.example` — the `SUPABASE_AUTH_SMTP_*` block, and the distinction
  between it and `RESEND_API_KEY`
