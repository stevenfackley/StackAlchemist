# StackAlchemist — Session Handoff

**Session:** SEO Week 1–3 + code audit + test coverage
**Branch:** `develop`
**As of:** 2026-04-16
**Build status:** green — 43 static pages, 53 unit tests passing, lint clean
**Site:** https://stackalchemist.app (prod) / https://test.stackalchemist.app (test mirror, noindex + basic auth)

---

## What shipped this session

### Week 3 SEO content surfaces (H1, H2, H6 from the audit plan)

**Blog** — `/blog` + `/blog/[slug]`
- Scaffold: `src/lib/blog-manifest.ts`, `src/lib/blog.ts`, `src/app/blog/page.tsx`, `src/app/blog/[slug]/page.tsx`
- 5 seed posts bylined **Steve Ackley** in `/content/blog/`:
  - `compile-guarantee-why-ai-codegen-must-verify.md` (2026-04-16)
  - `swiss-cheese-method-deterministic-templates-llm-logic.md` (2026-04-18)
  - `what-ai-code-generators-cant-do-yet.md` (2026-04-22)
  - `prompt-to-production-dotnet-nextjs-in-12-minutes.md` (2026-04-25)
  - `why-we-charge-once-not-monthly.md` (2026-04-28)
- JSON-LD: BlogPosting on each post, Blog on index, BreadcrumbList on children
- Prev/Next nav between posts; all sorted newest-first

**Compare** — `/compare` + `/compare/[slug]`
- Scaffold: `src/lib/compare-manifest.ts`, `src/lib/compare.ts`, `src/app/compare/page.tsx`, `src/app/compare/[slug]/page.tsx`
- 2 seed entries with honest bi-directional comparison (wins for them AND for us):
  - `/compare/v0`
  - `/compare/bolt-new`
- Verdict-bearing "short answer" CTA block above the fold on each page

**Solutions** — `/solutions` + `/solutions/[vertical]`
- Scaffold: `src/lib/solutions-manifest.ts`, `src/lib/solutions.ts`, `src/app/solutions/page.tsx`, `src/app/solutions/[vertical]/page.tsx`
- 3 seed verticals with keyword-targeted titles/descs:
  - `/solutions/ai-ecommerce-platform`
  - `/solutions/ai-lms-builder`
  - `/solutions/fintech-saas-generator`

**Shared infra**
- `src/components/content-header.tsx` — new shared nav header used by all three new surfaces. Marketing pages (/about, /story, /pricing, /simple, /advanced, /login, /register, /) still inline their own — see audit H2.
- `src/app/sitemap.ts` — extended to auto-include BLOG_POSTS, COMPARE_ENTRIES, SOLUTION_ENTRIES with correct `lastModified` from manifest
- `src/app/robots.ts` — explicit allow for `/blog`, `/compare`, `/solutions`, `/faq`

### Press / launch prep
- `/content/press/show-hn.md` — full Show HN draft with title options, body text, submission checklist, pre-drafted FAQ replies, signals to watch. Tuesday 9am ET recommended window.
- `/content/press/product-hunt-hunter-strategy.md` — 4-week action plan, Category A/B/C candidate lists (Chris Messina, Kevin William David, Ben Lang, Jiaona Zhang), outreach template, self-hunt fallback.

### Tests added (53 total, up from 21)

New files under `src/StackAlchemist.Web/__tests__/`:

| File | Tests | Covers |
|------|-------|--------|
| `lib/content-manifests.test.ts` | 23 | blog / compare / solutions / docs / faq manifest shape, unique slugs, ISO dates, sorted helper, FAQ sentence budget, category integrity |
| `lib/content-loaders.test.ts` | 7 | fs-backed loaders return meta+content for every manifest entry; null for unknown slugs; Steve byline check on seed post |
| `components/content-header.test.tsx` | 2 | all 5 primary nav links render with correct hrefs |

New e2e smoke spec:
- `e2e/smoke/seo-content-routes.spec.ts` — 13 specs covering index + dynamic routes for /blog, /compare, /solutions, /faq with canonical + JSON-LD + sitemap + robots assertions. Runs against `NEXT_PUBLIC_DEMO_MODE=true`.

### Infra fixes
- `vitest.config.ts` — fixed `@` alias to map to `./src` matching Next.js tsconfig. Previously `@/lib/*` imports in tested code failed to resolve.

### Audit
- `CODE_AUDIT.md` at repo root — findings report with HIGH/MED/LOW severity, file:line citations, concrete recommended fixes, and a ranked fix-order plan.

---

## Commit status

Nothing has been committed yet. `develop` branch has the full session's work staged but unpushed. Review with:
```bash
git status
git diff --stat develop
```

Suggested commit split (Conventional Commits):
1. `feat(blog): scaffold /blog route + seed 5 launch posts`
2. `feat(compare): scaffold /compare route + seed v0, bolt-new`
3. `feat(solutions): scaffold /solutions route + seed 3 verticals`
4. `feat(components): extract ContentHeader used by new content routes`
5. `feat(seo): sitemap and robots include new SEO surfaces`
6. `docs(press): add Show HN draft + Product Hunt hunter strategy`
7. `test(lib): cover manifests and content loaders`
8. `test(components): cover ContentHeader`
9. `test(e2e): smoke for /blog /compare /solutions /faq`
10. `fix(vitest): align @ alias with Next.js tsconfig paths`
11. `docs(audit): add CODE_AUDIT.md and HANDOFF.md`

Ship as one PR or split by feat vs. test vs. docs — caller's choice.

---

## Audit findings summary (full detail in CODE_AUDIT.md)

**HIGH (fix before PH/Show HN launch):**
1. JSON-LD generation duplicated across 8 files — extract to `src/lib/jsonld.ts`. Layout's `SoftwareApplication.offers="0"` already conflicts with pricing `Product.offers="299|599|999"`.
2. `ContentHeader` not adopted on 9 marketing pages yet.
3. ~~Zero test coverage on new manifests/loaders~~ — DONE this session.
4. ~~No e2e for new SEO surfaces~~ — DONE this session.

**MED:**
- Markdown loader factory candidate (blog.ts / compare.ts / solutions.ts / docs.ts duplicate shape — defer until 5th loader)
- `process.env.NEXT_PUBLIC_APP_URL` read in 8+ places — extract `SITE_URL` constant
- No `error.tsx` / `loading.tsx` boundaries on dynamic routes
- Manifests should use `as const satisfies readonly X[]` for literal-type slug narrowing
- FAQ entries lack slugs for deep-linking / AI-citation targeting
- `isDemoMode` auto-enables silently in non-prod when Supabase env is missing — warn at boot

**LOW:**
- Inline gradient `style` on pricing — extract Tailwind utility
- `readingTimeMinutes` manually authored in blog-manifest — auto-compute from word count

---

## What's outstanding (ordered)

**Immediate (pre-launch):**
1. Decide commit/PR strategy and push.
2. Fix audit HIGH #1 — extract `src/lib/jsonld.ts`. ~2 hours, low risk.
3. Install Plausible snippet with `NEXT_PUBLIC_PLAUSIBLE_DOMAIN` env (Week 1 SEO plan C3 — confirm it's actually loaded in prod).
4. Verify Google Search Console ownership is still valid and the sitemap has been re-submitted after this session's route additions.
5. Run e2e smoke locally: `pnpm e2e:smoke` — the new `seo-content-routes.spec.ts` has not been executed yet.

**This week:**
6. Audit HIGH #2 — migrate `/about`, `/story`, `/pricing`, `/simple`, `/advanced`, `/login`, `/register`, `/` to `ContentHeader`. One PR with visual regression checked.
7. Audit MED #3 — add `app/error.tsx` + per-segment error boundaries on `/blog/[slug]`, `/compare/[slug]`, `/solutions/[vertical]`.
8. Audit MED #2 — extract `SITE_URL` constant (15 min).
9. Wire Product Hunt Ship page (from PH hunter strategy doc, fallback path).

**Next 2 weeks:**
10. Execute PH hunter outreach — user picks 5 Cat-A + 5 Cat-B candidates from `content/press/product-hunt-hunter-strategy.md`, commits to a launch date, sends first 3 emails.
11. Schedule Show HN submission per `content/press/show-hn.md` — Tuesday 9am ET, 2+ weeks before PH launch.
12. Record 90-sec demo video (user task — blocked on user).
13. Audit MED #4 — upgrade manifests to `satisfies` for literal-type slugs.

**Post-launch:**
14. 2 blog posts/month cadence starting May.
15. 1 new `/compare/*` page per month (lovable, cursor, replit-agent, bubble, retool next).
16. Roll out remaining 15 `/solutions/*` pages over 3 months.
17. Audit LOW items — inline gradient, auto-computed readingTime.

---

## How to pick up

**Run the app:**
```bash
cd src/StackAlchemist.Web
pnpm install        # if fresh clone
pnpm dev            # localhost:3000
```

**Verify tests:**
```bash
cd src/StackAlchemist.Web
pnpm exec vitest run     # 53 tests in ~3s
pnpm lint                # must pass with 0 warnings
pnpm build               # must produce 43 static routes
pnpm e2e:smoke           # full Playwright chromium smoke (not executed yet this session)
```

**Where content lives:**
- Markdown: `/content/blog/*.md`, `/content/compare/*.md`, `/content/solutions/*.md`, `/content/press/*.md`
- Manifests (add a new slug here first, then drop the .md file): `src/lib/{blog,compare,solutions}-manifest.ts`
- Loaders follow identical pattern: `src/lib/{blog,compare,solutions}.ts`

**Add a new blog post:**
1. Append entry to `BLOG_POSTS` in `src/lib/blog-manifest.ts` — slug, title, description, publishedAt, tags, relativePath, readingTimeMinutes.
2. Create `/content/blog/<relativePath>` with the markdown.
3. `pnpm build` — verify the new path appears under `/blog/[slug]` prerender list.
4. `pnpm exec vitest run __tests__/lib/content-manifests.test.ts __tests__/lib/content-loaders.test.ts` — auto-covers the new entry.
5. Sitemap picks it up automatically.

**Add a new comparison or solution:** same three-step pattern with `compare-manifest.ts` / `solutions-manifest.ts`.

---

## Key context for the next session

- **Git identity:** commits are authored as `Steve Fackley` (the git config). Content bylines say **Steve Ackley** (the real user). Do not rename content; do rename git config eventually.
- **Test site:** `test.stackalchemist.app` is Cloudflare Tunnel → sa-web:3000 (no nginx). Basic Auth is enforced in Next.js middleware. `/api/healthz` is exempt. Tunnel/Docker probes MUST hit `/api/healthz`.
- **Engine backend:** StackAlchemist's own backend (the Swiss Cheese engine) uses **Claude 3.5 Sonnet** — strict business requirement for 95%+ gross margin. Do not switch the engine to Opus 4.x / Sonnet 4.6. That constraint applies to the product being built; the *builder* (this conversation) runs on Opus 4.7 and that's fine.
- **Blog rendering:** `react-markdown` + `remark-gfm` + `rehype-highlight`. The `DocsMarkdown` component is the single renderer for docs/blog/compare/solutions — don't fork it.
- **Demo mode:** `isDemoMode` returns true when `NEXT_PUBLIC_DEMO_MODE=true` OR when Supabase envs are missing in non-prod. Loaders fall back to a placeholder message so pages don't crash on Stackblitz/preview.

---

## Files created this session

```
CODE_AUDIT.md
HANDOFF.md
content/blog/compile-guarantee-why-ai-codegen-must-verify.md
content/blog/swiss-cheese-method-deterministic-templates-llm-logic.md
content/blog/what-ai-code-generators-cant-do-yet.md
content/blog/prompt-to-production-dotnet-nextjs-in-12-minutes.md
content/blog/why-we-charge-once-not-monthly.md
content/compare/v0.md
content/compare/bolt-new.md
content/solutions/ai-ecommerce-platform.md
content/solutions/ai-lms-builder.md
content/solutions/fintech-saas-generator.md
content/press/show-hn.md
content/press/product-hunt-hunter-strategy.md
src/StackAlchemist.Web/src/lib/blog-manifest.ts
src/StackAlchemist.Web/src/lib/blog.ts
src/StackAlchemist.Web/src/lib/compare-manifest.ts
src/StackAlchemist.Web/src/lib/compare.ts
src/StackAlchemist.Web/src/lib/solutions-manifest.ts
src/StackAlchemist.Web/src/lib/solutions.ts
src/StackAlchemist.Web/src/components/content-header.tsx
src/StackAlchemist.Web/src/app/blog/page.tsx
src/StackAlchemist.Web/src/app/blog/[slug]/page.tsx
src/StackAlchemist.Web/src/app/compare/page.tsx
src/StackAlchemist.Web/src/app/compare/[slug]/page.tsx
src/StackAlchemist.Web/src/app/solutions/page.tsx
src/StackAlchemist.Web/src/app/solutions/[vertical]/page.tsx
src/StackAlchemist.Web/__tests__/lib/content-manifests.test.ts
src/StackAlchemist.Web/__tests__/lib/content-loaders.test.ts
src/StackAlchemist.Web/__tests__/components/content-header.test.tsx
src/StackAlchemist.Web/e2e/smoke/seo-content-routes.spec.ts
```

## Files modified this session

```
src/StackAlchemist.Web/src/app/sitemap.ts    # auto-include new manifests
src/StackAlchemist.Web/src/app/robots.ts     # explicit allow for new surfaces
src/StackAlchemist.Web/vitest.config.ts      # align @ alias with Next tsconfig
```

---

*End of handoff.*
