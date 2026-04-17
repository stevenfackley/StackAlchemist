# StackAlchemist — Session Handoff

**Session:** SEO Week 1–3 + code audit + test coverage + infra hardening
**Branch:** `develop`
**As of:** 2026-04-17 (session 3, end)
**Build status:** green — deploy test passing, 54 unit tests, 24/24 e2e smoke
**Site:** https://stackalchemist.app (prod) / https://test.stackalchemist.app (test mirror, noindex + basic auth)

---

## What's in git (develop, all pushed)

### SEO content surfaces (prior session)

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
- 2 seed entries: `/compare/v0`, `/compare/bolt-new`

**Solutions** — `/solutions` + `/solutions/[vertical]`
- Scaffold: `src/lib/solutions-manifest.ts`, `src/lib/solutions.ts`, `src/app/solutions/page.tsx`, `src/app/solutions/[vertical]/page.tsx`
- 3 seed verticals: `/solutions/ai-ecommerce-platform`, `/solutions/ai-lms-builder`, `/solutions/fintech-saas-generator`

**Shared infra**
- `src/components/content-header.tsx` — shared nav header; now adopted by /blog, /compare, /solutions, /faq, /about, /story
- `src/app/sitemap.ts` — auto-includes BLOG_POSTS, COMPARE_ENTRIES, SOLUTION_ENTRIES
- `src/app/robots.ts` — explicit allow for /blog, /compare, /solutions, /faq
- `src/lib/jsonld.ts` — centralised JSON-LD builders (extracted from 8 files)
- `src/lib/faq-manifest.ts` — FAQ data (was missing from git, fixed)

### Infra hardening (this session)

- **Basic Auth gate** — `src/middleware.ts` enforces HTTP Basic Auth when `NEXT_PUBLIC_IS_TEST_SITE=true`. `/api/healthz` exempt. Timing-safe comparison.
- **Dynamic OG image** — `src/app/opengraph-image.tsx` replaces static 866 KB PNG
- **Web manifest** — `src/app/manifest.ts`
- **Health endpoint** — `src/app/api/healthz/route.ts` (auth-exempt, returns 200 ok)
- **llms.txt** — `public/llms.txt` for AI crawlers
- **X-Robots-Tag header** — added to `next.config.ts` for test mirror
- **Docker healthchecks** — both compose files now probe `/api/healthz` (not `/`)
- **CI env wiring** — `setup-env` action + both deploy workflows now pass `NEXT_PUBLIC_PLAUSIBLE_DOMAIN`, `GOOGLE_SITE_VERIFICATION`, `TEST_SITE_BASIC_AUTH_USER/PASS`
- **SEO metadata** — `/about`, `/advanced`, `/simple`, `/story`, `/` all have updated keyword-rich descriptions and canonical alternates

### Session 3 additions

- **Error boundaries** — per-segment `src/app/blog/[slug]/error.tsx`, `src/app/compare/[slug]/error.tsx`, `src/app/solutions/[vertical]/error.tsx`. Graceful fallback + "Back to index" link. (Root `src/app/error.tsx` was already in place.)
- **SITE_URL constant** — `src/lib/constants.ts` replaces 8+ scattered `process.env.NEXT_PUBLIC_APP_URL` reads.
- **Manifest literal narrowing** — BLOG/COMPARE/SOLUTIONS/FAQ manifests upgraded to `as const satisfies readonly X[]` for literal-type slug narrowing.
- **FAQ deep-link anchors** — `/faq` articles get `id={questionToAnchor(q)}` + hover-reveal `#` link; `questionToAnchor` extracted to `src/lib/faq-manifest.ts` for testability.
- **Demo mode boot warning** — `src/lib/runtime-config.ts` emits a one-time `console.warn` on server start when demo mode is auto-enabled because Supabase envs are missing (non-prod only).

### Tests (54 unit, 24 e2e smoke)

| File | Tests |
|------|-------|
| `__tests__/lib/content-manifests.test.ts` | 24 |
| `__tests__/lib/content-loaders.test.ts` | 7 |
| `__tests__/components/content-header.test.tsx` | 2 |
| `e2e/smoke/seo-content-routes.spec.ts` | 13 |
| `e2e/smoke/` (other smoke specs) | 11 |

e2e smoke requires `pnpm exec playwright install chromium` on a fresh machine (one-time).

---

## What's verified

- [x] Deploy test: green
- [x] `pnpm exec vitest run` — 53/53
- [x] `pnpm e2e:smoke` — 24/24 (first execution this session)
- [x] `pnpm exec tsc --noEmit` — clean
- [x] Plausible snippet wired in `src/app/layout.tsx` (line 113) — loads only when `NEXT_PUBLIC_PLAUSIBLE_DOMAIN` set and not test site
- [x] GSC verification wired in layout metadata (line 80) — reads `GOOGLE_SITE_VERIFICATION`

---

## Outstanding (ordered)

### Immediate — user action required (can't be done in code)

1. **Add GitHub secrets** — go to repo Settings → Secrets → Actions and add:
   - `TEST_SITE_BASIC_AUTH_USER` + `TEST_SITE_BASIC_AUTH_PASS` — gates the test mirror
   - `PLAUSIBLE_DOMAIN` — value: `stackalchemist.app` (prod deploy only)
   - `GOOGLE_SITE_VERIFICATION` — value from GSC HTML tag method (token only, not full tag)
   Without these the test mirror runs open and prod has no analytics.

2. **GSC sitemap re-submit** — /blog, /compare, /solutions are new since last submit. Go to GSC → Sitemaps → submit `https://stackalchemist.app/sitemap.xml`.

### This week — code work

3. **ContentHeader on remaining pages** — `/pricing`, `/simple`, `/advanced`, `/login`, `/register` all have their own inline headers. These were intentionally skipped (different bg theme or auth-page minimal nav). Evaluate whether to unify or fork `ContentHeader` with a variant.

4. **`global-error.tsx`** — segment + root boundaries in place; add a `global-error.tsx` for root-layout crashes.

### Pre-launch press

5. **Product Hunt hunter outreach** — pick 5 Cat-A + 5 Cat-B from `content/press/product-hunt-hunter-strategy.md`, set a launch date, send first 3 emails.

6. **Show HN submission** — schedule per `content/press/show-hn.md` — Tuesday 9am ET, 2+ weeks before PH launch.

7. **Demo video** — 90-sec recording (user task, blocked on user).

### Post-launch content cadence

8. 2 blog posts/month starting May.
9. 1 new `/compare/*` page per month (lovable, cursor, replit-agent, bubble, retool).
10. Roll out remaining 15 `/solutions/*` pages over 3 months.

---

## How to pick up

```bash
cd src/StackAlchemist.Web
pnpm install          # if fresh clone
pnpm dev              # localhost:3000

pnpm exec vitest run  # 53 unit tests
pnpm lint             # 0 warnings
pnpm build            # verify static route count
pnpm e2e:smoke        # 24 playwright smoke (needs chromium installed once)
```

**Add a blog post:**
1. Append to `BLOG_POSTS` in `src/lib/blog-manifest.ts`
2. Create `/content/blog/<relativePath>.md`
3. `pnpm build` — new slug appears in prerender list
4. `pnpm exec vitest run` — auto-covered

Same three-step pattern for compare (`compare-manifest.ts`) and solutions (`solutions-manifest.ts`).

---

## Key context

- **Git identity:** commits authored as `Steve Fackley` (git config). Content bylines say **Steve Ackley** (real user). Don't rename content.
- **Test site:** Cloudflare Tunnel → sa-web:3000 (no nginx). Basic Auth in middleware. `/api/healthz` exempt.
- **Engine backend:** uses **Claude 3.5 Sonnet** — strict 95%+ margin requirement. Do not switch to Opus/Sonnet 4.x.
- **Blog rendering:** `react-markdown` + `remark-gfm` + `rehype-highlight` via `DocsMarkdown` component — don't fork it.
- **Demo mode:** `isDemoMode` = true when `NEXT_PUBLIC_DEMO_MODE=true` OR Supabase envs missing in non-prod.
- **Plausible:** wired in layout, guarded by `NEXT_PUBLIC_PLAUSIBLE_DOMAIN` env + `!isTestSite`. Code is ready; just needs the secret added.
- **GSC:** wired in layout metadata via `GOOGLE_SITE_VERIFICATION` env. Code is ready; just needs the secret.

---

*End of handoff.*
