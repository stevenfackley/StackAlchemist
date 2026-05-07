# Decisions

ADR log. One entry per architectural decision. Append-only; supersede with a new entry.

## Format

```
## {{DATE}} — {{title}}
**Status:** proposed | accepted | superseded by #N
**Context:** why we had to decide
**Decision:** what we chose
**Consequences:** what follows (pros, cons, risks)
```

---

## 2026-04-17 — Bump pytest 8.3.3 → 9.0.3 in V1-Python-React template

**Status:** accepted
**Context:** Dependabot alert #1 flagged pytest < 9.0.3 (GHSA-6w46-j5rx-g56g / CVE-2025-71176, CVSS 6.8, tmpdir symlink DoS) in `src/StackAlchemist.Templates/V1-Python-React/backend/requirements.txt`. StackAlchemist is .NET 10 + Next.js 15 — pytest is not used by this repo's CI. The file is a scaffold template used at runtime to generate end-user Python/React apps. No workflow in `.github/workflows/` installs or runs pytest; `dependabot.yml` has no pip ecosystem configured.
**Decision:** Bump template pin `pytest==8.3.3` → `pytest==9.0.3` (first patched). Template kept, not deleted — it's a product feature (one of two V1 stack variants; sibling `V1-DotNet-NextJs/` also lives here).
**Consequences:**
- Alert closes on next Dependabot re-scan.
- End users who scaffold Python/React apps from this template now get a patched pytest.
- pytest 9.x drops Python 3.8 support — template's implied Python floor is 3.11+ anyway (fastapi 0.115, pydantic 2.10), so no breakage.
- If future alerts hit the other template deps (fastapi/sqlalchemy/etc), same playbook: bump pin, no CI impact.

---

## {{DATE}} — Initial stack: .NET 10 Native AOT

**Status:** accepted
**Context:** Greenfield service under portfolio `repo-template-dotnet10-aot`. Target: fast cold-start, small image, Linux deploys.
**Decision:** .NET 10 with `PublishAot=true`, `linux-musl-x64`, distroless static runtime.
**Consequences:**
- Cold start < 100ms, image ~15MB.
- Reflection, dynamic code gen restricted — must stay AOT-compatible.
- No Application Insights SDK (banned by CI); stdout logs only.

---

## 2026-04-29 — Pin eslint at ^9, complete eslint-config-next 16 + flat-config migration, opt build out of Turbopack

**Status:** accepted (supersedes the eslint 9→10 portion of the 2026-04-28 entry)
**Context:** Dependabot PR #53 (next-react group: next 15→16, eslint-config-next 15→16) merged but only updated `next`/`react`/`react-dom` in `package.json`, leaving `eslint-config-next` at `^15.3.0` while the lockfile pinned it at `16.2.4`. `npm ci` failed on every CI run for the lockfile mismatch. Investigating turned up two more issues: (a) PR #59 (eslint 9→10) was premature — every plugin in the chain (`@typescript-eslint/utils 8.59`, `eslint-plugin-react 7.37`, `eslint-plugin-react-hooks 7.1`, `eslint-plugin-import 2.32`, `eslint-plugin-jsx-a11y 6.10`) caps `eslint` peer at `^9`, and `typescript-eslint` calls `scopeManager.addGlobals(...)` which eslint 10 removed → runtime `TypeError`; (b) `eslint-config-next` 16 dropped legacy `.eslintrc.*` support entirely, so the existing `cross-env ESLINT_USE_FLAT_CONFIG=false` shim no longer works.
**Decision:**
- **Pin `eslint` at `^9.39.4`** in `package.json` until the typescript-eslint / next ecosystem ships eslint-10 peers (effectively a partial revert of PR #59).
- **Complete the eslint-config-next bump** to `^16.2.4` in `package.json` (matches the lockfile dependabot already wrote).
- **Migrate to flat config**: new `eslint.config.mjs` extends `eslint-config-next/core-web-vitals`, registers `react-hooks` plugin in the same config object as the rule override (flat-config plugin scoping), preserves the prior custom rules. Delete `.eslintrc.json`. Lint script becomes `eslint --max-warnings 0` (drop `cross-env ESLINT_USE_FLAT_CONFIG=false` and `--ext`).
- **Refactor `GenerateClientPage` demo init** out of the effect into a lazy `useState` initializer (the new `react-hooks/set-state-in-effect` rule in plugin v7 caught the pattern; refactor is the proper fix, not a disable).
- **Pass `--webpack` in `build-wrapper.mjs`** to opt out of Turbopack (now the default in Next 16). The custom `webpack` hook in `next.config.ts` exists specifically for the Windows + pnpm symlink-casing static-prerender bug; removing it would re-break that, and migrating it to a Turbopack equivalent is out of scope for a "make CI green" PR.
**Consequences:**
- CI green again on `main`. `npm ci` succeeds. lint, typecheck, 54 unit tests all pass. `next build` produces a normal standalone bundle.
- Dependabot will retry eslint 10 every cycle. When the plugin ecosystem catches up (typescript-eslint v9, eslint-plugin-react ^9.7+ for eslint 10, etc.), unblock by bumping `eslint` and re-running the lint suite.
- Next.js 16 also auto-rewrote `next-env.d.ts` (now `import` instead of `///<reference path>`) and `tsconfig.json` (`jsx: preserve → react-jsx`, added `.next/dev/types/**/*.ts` to includes). Both files Next owns; behavior unchanged.
- Build emits a warning that the `middleware` file convention is deprecated in Next 16 (rename to `proxy`). Functional today; left for a follow-up.

---

## 2026-04-28 — Dependabot sweep: AWSSDK.S3 3→4, eslint 9→10, lucide-react 0→1, plus minors

**Status:** accepted (awareness-only stub per saved sweep policy)
**Context:** 11 open Dependabot PRs swept across `/src/StackAlchemist.Web` (npm) and root (NuGet). Three majors warranted ADR notes (this entry).
**Decision:** Auto-merge per policy.
**Consequences — majors to watch:**
- **AWSSDK.S3 3.7.511.6 → 4.0.22.1** (PR #63):
  - **AWSSDK v4** is the .NET v4 SDK rewrite. New namespaces stay backward-compatible by default but several legacy clients/types moved.
  - **AmazonS3Client constructor:** still works. **`PutObjectRequest`/`GetObjectRequest`** APIs unchanged for the common path.
  - **Endpoint resolution:** rewritten — region-only setups still work; custom endpoint discovery code may need adjustment.
  - **`AmazonS3Client.UploadPartCopy`** signature tightened. We don't use multipart copy directly.
  - Risk: low for typical bucket read/write; medium if we touch presigned URLs or transfer utility paths. Smoke-test on first deploy.
- **eslint 9.39.4 → 10.2.1** (PR #59):
  - **ESLint 10:** drops Node 18, requires Node 20.10+. CI on Node 24 → fine.
  - **Default config still flat config (`eslint.config.*`).** Legacy `.eslintrc` is officially gone.
  - Several deprecated rules removed; `typescript-eslint` already on 8.x is compatible.
  - Risk: low — most pain came at 9.x with flat-config migration; 10.x is incremental.
- **lucide-react 0.454.0 → 1.11.0** (PR #55):
  - See steveackleyorg DECISIONS for the same bump. Pre-1.0 → 1.0 is mostly a rename. ESM-only now. Risk: low.
**Why no review:** private/solo repo, deploy workflows are the real build, revert is cheap.

---

## 2026-05-06 — Bump Anthropic default to claude-sonnet-4-6

**Status:** accepted
**Context:** Issue #92 — engine returns 404 from api.anthropic.com because
`claude-3-5-sonnet-20241022` was retired. Prod compose was on
`claude-sonnet-4-5-20250929`; CI was falling back to the dead engine default
because `ANTHROPIC_MODEL` was not threaded through the workflow.
**Decision:**
- Engine default → `claude-sonnet-4-6` (in code, appsettings, all test fixtures).
- Web `DEFAULT_MODEL` → `claude-sonnet-4-6`. Old IDs stay in
  `ALLOWED_PROFILE_MODELS` so existing profiles aren't silently rewritten.
- Wire `ANTHROPIC_MODEL` through `setup-env` action and all workflows; resolve
  from `vars.ANTHROPIC_MODEL` (repo/environment variable, not secret) with
  `'claude-sonnet-4-6'` as the workflow-level fallback.
- New Supabase migration bumps `profiles.preferred_model` column default.
**Consequences:**
- One env var (`ANTHROPIC_MODEL`) is now the single point of control for the
  active model — future deprecations require flipping one variable rather
  than editing source.
- Pricing parity with 4.5 means no cost regression. Watch Anthropic's
  deprecation page; downgrade only if 4.5 drops in price.
- BYOK paths still allow legacy 3.5 IDs for users who explicitly chose them.
