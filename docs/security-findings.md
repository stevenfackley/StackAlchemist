# Security Findings — Health Pass (2026-06-19)

Produced as part of the `chore/health-pass` branch sweep.
Severity levels: **Critical** / **High** / **Medium** / **Low** / **Informational**.

---

## Fixed in This Pass

### [F1] npm dependency CVEs (High) — FIXED

| Package | CVE / Advisory | Severity | Fixed in |
|---|---|---|---|
| `undici` | GHSA-vmh5-mc38-953g (TLS cert bypass via SOCKS5 ProxyAgent) | High | npm audit fix |
| `undici` | GHSA-pr7r-676h-xcf6 (cross-user info disclosure via cache whitespace) | High | npm audit fix |
| `brace-expansion` | GHSA-jxxr-4gwj-5jf2 (large numeric range DoS) | Moderate | npm audit fix |
| `@babel/core` | (low severity) | Low | npm audit fix |

`package-lock.json` updated. Zero vulnerabilities remain per `npm audit`.

---

## Informational / Already Mitigated

### [I1] `.p8` private key file present in working directory

`AuthKey_K2A93UC96X.p8` is a private key file that exists in the repo's working directory at `C:\Users\steve\projects\StackAlchemist\AuthKey_K2A93UC96X.p8`. It is **not tracked by git** (`.gitignore` includes `*.p8`) and has **no git history** in this repo.

Action: The file is untracked and gitignored. Confirm it is not present on the CI runner's checkout path and is intended to be local-only. If it is an Apple Push Notification key (the filename format `AuthKey_<10-char-id>.p8` is the APNs convention), ensure it is stored in a secrets manager, not the developer's project directory.

### [I2] Basic Auth credentials: timing-safe comparison implemented correctly

`src/middleware.ts:timingSafeEqual` uses bitwise XOR over char codes to avoid short-circuit — correct pattern, resistant to timing attacks.

### [I3] BYOK key encryption

BYOK encryption uses AES-256-GCM (authenticated encryption) with a scrypt-derived key. The IV is properly randomized per-encrypt. The format is `v1:<iv_b64>:<tag_b64>:<ciphertext_b64>`. This is sound.

Note: The encryption secret falls back to `ENGINE_SERVICE_KEY` if `BYOK_ENCRYPTION_KEY` is not set:
```typescript
function getByokEncryptionSecret() {
  return process.env.BYOK_ENCRYPTION_KEY ?? process.env.ENGINE_SERVICE_KEY ?? "";
}
```
If only `ENGINE_SERVICE_KEY` is configured, rotating the engine key would invalidate all stored BYOK keys. Consider documenting this dependency or enforcing `BYOK_ENCRYPTION_KEY` as a distinct secret.

### [I4] Prompt injection in `sanitizePrompt` (server action)

`sanitizePrompt` in `src/lib/actions.ts` strips `[[FILE:...]]` markers and `[[END_FILE]]` to prevent prompt injection into the engine's template format. The `PromptBuilderService.SanitizeUserInput` on the .NET side does the same. Double-layer defense is good.

Potential gap: `sanitizePrompt` blocks file-block injection but does not filter `## heading` lines, which `SanitizeUserInput` does. If the user prompt from the frontend is passed straight to the LLM schema-extraction prompt (not to the Handlebars injection), this is safe since the schema extraction path only outputs JSON. No action required.

### [I5] Engine API key (`X-Engine-Key`) authentication

The `.NET` Engine checks `X-Engine-Key` header in production. In `Program.cs`, the service key is validated to be non-empty before startup in production. The Next.js server action always sends the header via `engineHeaders()`. This is correct.

Gap: The validation in CI's quality gate (`validate-secrets`) checks `ENGINE_SERVICE_KEY` with `^.{32,}$` (must be >= 32 chars). Good.

### [I6] NuGet packages: zero vulnerable packages

`dotnet list package --vulnerable` confirmed 0 vulnerable packages across all 4 projects.

### [I7] SQL injection posture

The Engine uses Dapper with parameterized queries (enforced via code review and the injection prompt constraint "Use parameterized SQL — no string interpolation in queries"). Supabase PostgREST handles the web-facing queries via RLS-filtered selects. No raw SQL interpolation found.

### [I8] CSP report endpoint

`POST /api/csp-report` logs violations to stdout only, never persists PII, never returns 5xx. The `runtime = "nodejs"` directive is correct (Edge runtime cannot log to stdout). This is well-implemented.

### [I9] Test-site Basic Auth fail-open behavior

If `NEXT_PUBLIC_IS_TEST_SITE=true` but credentials are missing, the middleware logs a warning and continues open (fail-open). This is the documented behavior and intentional — a missing credential should not take the test mirror down. However, it means a misconfigured deploy would expose the test mirror publicly. The warning in logs is the only signal. Consider adding a CI assertion that if `IS_TEST_SITE=true`, the credentials are present.

---

## CI Gaps Noted

1. **No `npm audit` step in CI**: The frontend CI does not run `npm audit`. The 3 vulnerabilities fixed in this pass would not have been caught automatically. Recommend adding `npm audit --audit-level=moderate` as a CI step in the `frontend` job.

2. **Coverage thresholds**: `vitest.config.ts` sets 80%/75%/80%/80% thresholds but the CI uploads coverage artifacts without enforcing thresholds as a gate. Add `--coverage --coverage.thresholds.lines=80` or similar to the vitest CI command to make thresholds blocking.

3. **Playwright E2E needs live stack**: Integration E2E requires secrets, a running Docker stack, and live Supabase. The `validate-secrets` step gracefully skips when secrets are placeholders. On `main`, the CI correctly fails if E2E was skipped (`Fail if E2E Integration silently skipped on main` step). This is sound.
