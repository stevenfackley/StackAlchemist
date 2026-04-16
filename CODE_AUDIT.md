# StackAlchemist Web — Code & Design-Pattern Audit

**Date:** 2026-04-16
**Scope:** `src/StackAlchemist.Web/` (Next.js 15 App Router frontend)
**Auditor:** code review pass after Week 1–3 SEO + content scaffold shipped
**Build status at audit time:** green (43 static pages, pnpm build exit 0)

---

## Summary

Codebase is in better shape than the typical pre-launch SaaS. Manifest + dynamic-route pattern is sound, demo-mode fallback is disciplined, and the new `/blog`, `/compare`, `/solutions` surfaces follow the established `/docs` pattern cleanly. The findings below are genuine improvements, not "we would have done it differently" nitpicks — but none block ship.

Severity key:
- **HIGH** — fix before public launch (PH / Show HN)
- **MED** — fix within 2 weeks of launch
- **LOW** — polish, nice-to-have

---

## HIGH

### H1. JSON-LD duplication across 8 files
Every dynamic route builds `BreadcrumbList` schema inline; layout emits `SoftwareApplication`; pricing emits `Product`; faq emits `FAQPage`; blog index emits `Blog`. Eight files, each with its own JSON.stringify pattern.
- **Files:** `src/app/layout.tsx`, `src/app/pricing/page.tsx`, `src/app/faq/page.tsx`, `src/app/blog/page.tsx`, `src/app/blog/[slug]/page.tsx`, `src/app/compare/[slug]/page.tsx`, `src/app/solutions/[vertical]/page.tsx`, `src/app/docs/[slug]/page.tsx`
- **Risk:** schema drift across pages. Already visible — `SoftwareApplication.offers` in `layout.tsx:65` contradicts `Product.offers` in `pricing/page.tsx:147-165`, which Google's Rich Results Tester flags.
- **Fix:** Extract to `src/lib/jsonld.ts`:
  ```ts
  export function breadcrumbJsonLd(trail: Array<{ name: string; path: string }>, siteUrl: string) { ... }
  export function blogPostingJsonLd(meta: BlogPostMeta, siteUrl: string) { ... }
  export function faqPageJsonLd(faqs: FaqEntry[]) { ... }
  export function productJsonLd(tiers: PricingTier[], siteUrl: string) { ... }
  ```
  Import and stringify once per page. Single test suite covers schema generation.
- **Effort:** 2 hours + unit tests.

### H2. ContentHeader not adopted by existing marketing pages
`src/components/content-header.tsx` exists and is used by `/blog`, `/compare`, `/solutions`. Marketing pages still inline a near-identical sticky nav.
- **Files still inlining:** `src/app/about/page.tsx`, `src/app/story/page.tsx`, `src/app/faq/page.tsx`, `src/app/pricing/page.tsx`, `src/app/simple/SimpleModePage.tsx`, `src/app/advanced/AdvancedModePage.tsx`, `src/app/login/LoginPageClient.tsx`, `src/app/register/RegisterPageClient.tsx`, `src/app/HomePageClient.tsx`
- **Risk:** nav rot — adding a new top-level link (e.g. `/changelog`) means 10+ edits.
- **Fix:** migrate pages one at a time, verifying visual parity. `ContentHeader` may need a `variant` prop for `/login` + `/register` which intentionally omit some links. Marketing pages often include extra marketing links (Story, Simple, Advanced) — either extend ContentHeader or add `ContentHeader.Marketing` for those.
- **Effort:** 1 page = 10 min. All 9 pages = ~2 hours. Do in one PR with Playwright visual regression if configured.

### H3. Zero test coverage on manifests + new loaders
`blog-manifest.ts`, `blog.ts`, `compare-manifest.ts`, `compare.ts`, `solutions-manifest.ts`, `solutions.ts`, `docs-manifest.ts`, `faq-manifest.ts`, `components/content-header.tsx` have **no** unit tests.
- **Risk:** duplicate slugs, malformed dates, missing relativePath files all fail at runtime instead of at CI.
- **Fix:** added in this session — see `__tests__/lib/content-manifests.test.ts`, `__tests__/lib/content-loaders.test.ts`, `__tests__/components/content-header.test.tsx`.

### H4. No e2e smoke test for the new public SEO surfaces
`/blog`, `/compare`, `/solutions`, and their dynamic children have no Playwright coverage. Existing smoke tests cover Simple/Advanced mode, checkout, dashboard only.
- **Risk:** a bad metadata change, a broken sitemap entry, or a missing markdown file in `content/` would ship silently.
- **Fix:** added in this session — see `e2e/smoke/seo-content-routes.spec.ts`.

---

## MED

### M1. Markdown loader duplication (`blog.ts`, `compare.ts`, `solutions.ts`, `docs.ts`)
All four loaders implement the same pattern: `fs.readFileSync(BLOG_ROOT/relativePath)`, demo-mode fallback, `null` on miss. They differ only in the manifest they consult and the demo-content template.
- **Files:** `src/lib/blog.ts`, `src/lib/compare.ts`, `src/lib/solutions.ts`, `src/lib/docs.ts`
- **Factory candidate:**
  ```ts
  // src/lib/content-loader.ts
  export function createMarkdownLoader<T extends { relativePath: string }>(
    root: string,
    lookup: (slug: string) => T | undefined,
    demoFallback: (meta: T) => string
  ) { ... }
  ```
- **Trade-off:** small enough duplication (~20 lines × 4 files) that the factory might not earn its keep unless a 5th loader lands. Defer until `/changelog` or similar ships.
- **Decision:** LEAVE. Revisit when the 5th loader is needed.

### M2. `process.env.NEXT_PUBLIC_APP_URL` read in 8+ places
Every dynamic route re-derives `siteUrl`:
```ts
const siteUrl = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");
```
- **Fix:** extract to `src/lib/runtime-config.ts`:
  ```ts
  export const SITE_URL = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");
  ```
- **Effort:** 15 min. Trivial grep-and-replace.

### M3. No route-level `error.tsx` / `loading.tsx`
There is no `app/error.tsx`, no `app/global-error.tsx`, and no per-segment `loading.tsx`. A thrown error in a markdown loader (e.g. disk permission, encoding issue) falls through to Next.js's default error page.
- **Fix:** add `src/app/error.tsx` (global) + `src/app/blog/[slug]/error.tsx` (segment). Render a "this post isn't available right now" card with a link back to `/blog`.
- **Effort:** 1 hour.

### M4. Manifest type safety uses interface, not `satisfies`
All manifests declare `export const X: Interface[] = [...]`. That's fine for validation but loses literal-type narrowing (slug unions, etc.).
- **Upgrade:**
  ```ts
  export const BLOG_POSTS = [ /* ... */ ] as const satisfies readonly BlogPostMeta[];
  ```
- **Benefit:** `getAllBlogSlugs()` can return `readonly ["compile-guarantee-...", ...]` instead of `string[]`, enabling compile-time slug checks in link destinations.
- **Effort:** 30 min. Light regression risk because widening to `string[]` still works.

### M5. `src/lib/faq-manifest.ts` has no slug-level metadata
FAQs are referenced only via category+question. If someone wants to deep-link to a specific Q (`/faq#pricing-subscription`), there's no stable id generator. Google's FAQPage rich result doesn't need it, but an "ask-AI citation" strategy does.
- **Fix:** add `slug` to `FaqEntry`; derive slug from question-kebab. Expose `<section id={faq.slug}>`.
- **Effort:** 1 hour.

### M6. Demo-mode detection is implicit
`isDemoMode` in `src/lib/runtime-config.ts:1-3` returns `true` when `NODE_ENV !== "production"` AND `NEXT_PUBLIC_SUPABASE_URL` is unset. Playwright e2e tests run with `NEXT_PUBLIC_DEMO_MODE=true` explicitly — but someone running `pnpm dev` locally without env gets demo mode silently.
- **Risk:** LOW, but surprising. Log a console warning at boot when demo mode is auto-enabled.
- **Effort:** 10 min.

---

## LOW

### L1. No `alternates.canonical` on `src/app/HomePageClient.tsx`-routed pages
The homepage metadata lives in the `page.tsx` but the page is a client component wrapper. Canonical added in Week 1 — verified still present.

### L2. Inline gradient styles
`src/app/pricing/page.tsx:191` sets `style={{ backgroundImage: "..." }}` inline. Minor. Tailwind custom utility would centralize.

### L3. `src/components/logo.tsx` imported by ContentHeader and every page separately
Not a bug, just a note. Logo is correctly a shared component.

### L4. `readingTimeMinutes` is manually authored in `blog-manifest.ts`
Auto-computable from markdown word count at build time. Would prevent authors forgetting to update it. Not urgent.

### L5. JSON-LD `BreadcrumbList.position` values are string-literal integers
Schema.org accepts numbers. Minor stylistic — keep as-is until extraction (H1).

---

## Patterns in good shape (keep doing)

1. **Manifest + dynamic route pattern** — `lib/*-manifest.ts` + `lib/*.ts` loader + `app/.../[slug]/page.tsx` with `generateStaticParams`. Clean, scales linearly, zero runtime overhead. Mirrored across docs/blog/compare/solutions.
2. **Demo-mode fallback in loaders** — `if (isDemoMode) return { meta, content: "<placeholder>" }`. Prevents crashes when repo-level content isn't accessible (Stackblitz previews, headless CI).
3. **`@/` import aliases** — consistent throughout. No relative `../../../` offenders spotted.
4. **`DocsMarkdown` shared renderer** — single source of truth for markdown rendering (react-markdown + remark-gfm + rehype-highlight) across docs, blog, compare, solutions.
5. **Typed metadata exports** — every page has `export const metadata: Metadata = { ... }` or `generateMetadata()`. Next 15 static-metadata pattern used correctly.
6. **Manifests are `interface` + typed array** — not loose object literals. Strong foundation for M4 upgrade.
7. **`generateStaticParams` on every dynamic route** — all new routes SSG at build, confirmed by `●` marker in build output.
8. **`robots.ts` + `sitemap.ts` are code, not static files** — easy to extend, typed, auto-include new manifests.
9. **Content files outside `/src`** — `/content/blog/*.md` etc. live at repo root, keeping them reviewable by non-engineers and not coupled to webpack.

---

## Recommended fix order

**Week of launch:**
1. H3 + H4 — land the test suite (done in this session).
2. H1 — extract `src/lib/jsonld.ts`. High-leverage, low-risk.
3. M2 — `SITE_URL` constant. 15 min.
4. M3 — `error.tsx` boundaries.

**Post-launch week 1:**
5. H2 — migrate marketing pages to ContentHeader.
6. M4 — `satisfies` on manifests.
7. M5 — FAQ slug support for AI citation.

**Backlog (revisit at 5+ content types):**
8. M1 — content-loader factory.

---

## Metrics to track post-fix

- Google Search Console: coverage / indexing status of new routes (`/blog/*`, `/compare/*`, `/solutions/*`)
- Rich Results Test pass rate across all schema-emitting pages
- Playwright CI run time (should stay under 5 min with the new smoke tests)
- Vitest run time (should stay under 30 sec)
- Lighthouse scores on each SEO surface page

---

*End of audit.*
