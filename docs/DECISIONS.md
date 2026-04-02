# StackAlchemist — Architectural Decisions Log

Reference this file instead of re-reading source files when possible.

---

## Phase 1 — Project Scaffolding & UI Foundations (2026-04-02)

### Frontend Stack
- **Next.js 15.5** (App Router) — upgraded from pinned 15.0.0 RC which had peer dep conflicts with React 19 RC
- **React 19 stable** — `^19.0.0` / `^19.0.0` (not the RC build in the original package.json)
- **Tailwind CSS 3.4** — not v4; existing project constraints + v4's `@tailwindcss/postcss` setup conflicted
- **TypeScript strict mode** on; `moduleResolution: bundler`
- **`@xyflow/react` v12** for React Flow entity canvas (both Simple and Advanced modes)
- Fonts: Inter (UI) + JetBrains Mono (code/terminal) loaded via `next/font/google`

### Design System
- Dark-only. No light mode toggle. `dark` class forced on `<html>`.
- Color tokens defined as Tailwind `extend.colors`: `void`, `slate-surface`, `electric`, `emerald`, `rose`, `slate-border`
- 0px/4px border radius only. No shadows.

### Routing
- `/` — Landing page (hero, features, pricing teaser)
- `/simple?q=<prompt>` — Simple Mode: generation animation → editable React Flow canvas
- `/advanced?step=<1|2|3>` — Advanced Mode: 3-step stepper wizard

### Pricing placement
- Full pricing grid removed from landing page per owner feedback.
- Pricing details live exclusively in `/advanced?step=3`.
- Landing page has a teaser CTA linking to that route.

### Environment / Tooling
- npm cache env var `npm_config_cache` was set to `F:\packages\npm` (F: drive does not exist).
- `.npmrc` updated to `G:\packages\npm`. Must prefix npm commands with `npm_config_cache="G:/packages/npm"` or fix the shell env permanently.
- `next.config.ts`: `outputFileTracingRoot: __dirname` set to suppress pnpm-lock.yaml workspace root warning.

### .NET Engine
- `dotnet new webapi --no-https --use-controllers=false --framework net10.0`
- Minimal API template. No controllers yet — Phase 3 will add services.
- 1 warning: invalid VS BuildTools LIB path in env — harmless, pre-existing env issue.

---
