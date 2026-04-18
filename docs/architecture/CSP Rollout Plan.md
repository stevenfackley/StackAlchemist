# CSP Rollout Plan

**Status:** Report-Only (monitoring) · **Owner:** Steve Ackley · **Last reviewed:** 2026-04-18

We ship Content-Security-Policy as `Content-Security-Policy-Report-Only` first, collect violation reports from real traffic, then flip to enforce once the allowlist is stable. This doc is the runbook — monitor, tighten, flip, roll back.

## Current state

- Header: `Content-Security-Policy-Report-Only` (see `src/StackAlchemist.Web/next.config.ts`)
- Report endpoint: `POST /api/csp-report` (middleware-exempt, logs to stdout)
- Coverage: all routes via `source: "/:path*"` in Next.js headers config

## Allowlist (as of 2026-04-18)

```
default-src 'self'
script-src  'self' 'unsafe-inline' 'unsafe-eval' js.stripe.com plausible.io static.cloudflareinsights.com
style-src   'self' 'unsafe-inline'
img-src     'self' data: blob: https:
font-src    'self' data:
connect-src 'self' *.supabase.co wss://*.supabase.co api.stripe.com plausible.io cloudflareinsights.com
frame-src   'self' js.stripe.com hooks.stripe.com
worker-src  'self' blob:
object-src  'none'
frame-ancestors 'self'
upgrade-insecure-requests
report-uri  /api/csp-report
```

`'unsafe-inline'` and `'unsafe-eval'` in `script-src` are transitional — Next.js App Router still injects inline bootstrap scripts. Plan is to adopt `strict-dynamic` + nonces in a follow-up once we confirm no third-party snippet relies on document-level inline execution.

## Phase 1 — Monitor (current)

**Duration:** minimum 7 days of production traffic after each deploy that touches `<head>`, analytics, payments, or third-party embeds.

**What to watch for** in `docker logs sa-web 2>&1 | grep '\[csp-report\]'`:

- `violated-directive` — which rule tripped
- `blocked-uri` — source URL or inline-hash
- `document-uri` — which page
- `effective-directive` — post-spec-normalized directive name

**Triage categories:**

| Pattern | Action |
|---|---|
| Known good third-party (new Stripe subdomain, Plausible path) | Add to allowlist, stay in Report-Only one more cycle |
| Inline script Next.js emits | Add a sha256 hash OR plan strict-dynamic migration |
| Browser-extension noise (`chrome-extension://`, `moz-extension://`) | Ignore — extensions inject regardless |
| Suspicious origin (unknown CDN, tracker) | Investigate before allowing — may be a real vuln |

A violation-free 7-day window on a stable head/payments/analytics surface is the gate for Phase 2.

## Phase 2 — Enforce

**Trigger:** 7 consecutive days of Report-Only with zero actionable (non-extension) violations on main production domains (`stackalchemist.app`, `www.stackalchemist.app`).

**Change:**

```diff
- { key: "Content-Security-Policy-Report-Only", value: cspReportOnly }
+ { key: "Content-Security-Policy",              value: cspEnforce }
```

Keep `report-uri /api/csp-report` in the enforce policy — violations after enforce become actual blocks AND still get reported, which is what we want for post-launch monitoring.

**Recommended:** run both headers in parallel for 48h at the cutover:
- `Content-Security-Policy` (enforce, tight)
- `Content-Security-Policy-Report-Only` (experimental — tighter candidate with `'strict-dynamic'` or no `'unsafe-eval'`)

The browser honors whichever is more restrictive on enforce, and the Report-Only stream tells us what the next tightening would break.

## Phase 3 — Rollback procedure

If enforce breaks production (payment flow, analytics, docs rendering, etc.):

1. Revert the header constant in `next.config.ts` to `Content-Security-Policy-Report-Only`.
2. Commit as `fix(sec): revert CSP to report-only — <symptom>`.
3. Push to `main`; the deploy-prod workflow ships the fix.
4. File the broken directive + `blocked-uri` into the allowlist discussion doc before re-attempting Phase 2.

Cloudflare sits in front, but we do **not** override CSP at the edge — the origin is the source of truth. Do not add a Cloudflare Transform Rule for CSP; that splits the policy across two systems and makes rollback ambiguous.

## Non-goals for v1

- `strict-dynamic` with per-request nonces (requires middleware rewrite to inject nonces into Next.js's inline bootstrap) — tracked separately
- Trusted Types enforcement — phase 4, after enforce is stable
- Subresource Integrity on third-party scripts — blocked by Stripe/Plausible not publishing stable SRI hashes
