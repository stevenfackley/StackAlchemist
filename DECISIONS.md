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
