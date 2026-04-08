# E2E Contract Policy

## Stable Selector Rules
- Use `data-testid` for flow-critical assertions and transitions.
- Prefer semantic roles only when they are unique and contractually stable.
- Do not gate core flow tests on marketing copy, cosmetic headings, or broad regex text matches.
- Avoid ambiguous locators that can match multiple elements in strict mode.

## Suite Strategy
- `e2e/smoke`: deterministic PR gate, run in demo mode.
- `e2e/integration`: main/nightly verification in non-demo mode with real environment wiring.

## When UI Changes
- If a UI change touches a contract anchor (home/simple/advanced/pricing/generate), update the corresponding `data-testid` and matching E2E spec in the same PR.
- If behavior intentionally changes (for example route semantics or phase order), update the affected spec first so CI reflects the new contract rather than a stale assumption.
- Keep artifact-producing CI steps intact so failures always include report + trace/screenshot outputs.
