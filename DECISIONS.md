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
