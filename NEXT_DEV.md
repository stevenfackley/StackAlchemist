# StackAlchemist Production Readiness Plan

## Context

StackAlchemist is a micro-SaaS (Next.js 15 + .NET 10 + Supabase + Cloudflare R2 + Stripe + Anthropic Claude) that generates compile-verified code repos from natural language. A full audit reveals **critical production gaps**: the LLM engine silently falls back to mock data in prod, no retry logic on API failures, no cost tracking, mobile-broken editor pages, zero SEO infrastructure, legacy "Bolt.new" references in code, and 40KB monolith components. This plan addresses all of it.

---

## Phase 1: LLM Engine Hardening (CRITICAL -- Do First)

### 1.1 Fix Production Startup Validation [S]
**File:** `src/StackAlchemist.Engine/Program.cs:83-97`
- Add `ANTHROPIC_API_KEY`, `SUPABASE_SERVICE_ROLE_KEY`, `R2_ACCESS_KEY_ID` to the `required` array
- Add startup log warning when MockLlmClient is active (lines 217-225)
- **Why:** Prod currently starts fine without an API key and silently returns fake code to paying users
- **Verify:** Start engine in Production mode without `ANTHROPIC_API_KEY` -- must throw `InvalidOperationException`

### 1.2 Add Retry with Exponential Backoff [M]
**File:** `src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs:56-70`
**File:** `src/StackAlchemist.Engine/Program.cs:185-189` (HttpClient registration)
- Add `Microsoft.Extensions.Http.Resilience` standard resilience handler on the HttpClient, OR implement retry loop in `GenerateAsync` with: retry on 429/500/502/503/529, parse `retry-after` header, exponential backoff (1s base), max 3 retries, jitter
- **Verify:** Unit test with mock HttpMessageHandler returning 429 twice then 200

### 1.3 Persist LLM Token Usage [M]
**File:** `src/StackAlchemist.Engine/Services/ILlmClient.cs`
**File:** `src/StackAlchemist.Engine/Services/AnthropicLlmClient.cs:72-76`
**File:** `src/StackAlchemist.Engine/Services/GenerationOrchestrator.cs:69`
**File:** `src/StackAlchemist.Engine/Services/CompileWorkerService.cs:163`
- Change `ILlmClient.GenerateAsync` return from `Task<string>` to `Task<LlmResponse>` (new record: `Text`, `InputTokens`, `OutputTokens`, `Model`)
- Add Supabase migration: `input_tokens`, `output_tokens`, `model_used` columns on `generations`
- Add `UpdateTokenUsageAsync` to `IDeliveryService`/`SupabaseDeliveryService`
- Update both callers
- **Verify:** Run generation, query DB, confirm token columns populated

### 1.4 Add Schema Size Limits [S]
**File:** `src/StackAlchemist.Engine/Services/SchemaExtractionService.cs:68-86`
**File:** `src/StackAlchemist.Web/src/lib/actions.ts:207`
- After `MapToSchema`, validate: max 20 entities, max 30 fields/entity, max 30 relationships. Throw `SchemaValidationException` if exceeded
- Add matching client-side validation in `actions.ts`
- **Verify:** Submit schema with 25 entities -- rejected with clear error

### 1.5 Sanitize Personalization Data [S]
**File:** `src/StackAlchemist.Engine/Services/PromptBuilderService.cs:82-129`
- Create `SanitizeUserInput(string input, int maxLength)` that: caps length (500 chars descriptions, 50 names), strips `[[FILE:`, `[[END_FILE]]`, `##` line starts, unicode control chars
- Apply to all personalization fields before prompt interpolation
- **Verify:** Submit generation with `BusinessDescription` containing `[[FILE:etc/passwd]]` -- stripped

### 1.6 Fix Build Log Race Condition [M]
**File:** `src/StackAlchemist.Engine/Services/SupabaseDeliveryService.cs:84-123`
- Create Supabase migration with `append_build_log(gen_id uuid, chunk text)` Postgres function (atomic concatenation)
- Replace fetch-append-patch with single RPC call
- **Verify:** Concurrent appends produce complete, ordered log

### 1.7 Make Model Version Configurable via Env [S]
**File:** `src/StackAlchemist.Engine/Program.cs:60-79`
**File:** `src/StackAlchemist.Engine/appsettings.json:15`
- Add `["Anthropic:Model"] = Ev("ANTHROPIC_MODEL")` to env mapping
- Verify current model identifier is latest stable Claude 3.5 Sonnet
- **Verify:** Set `ANTHROPIC_MODEL` env var, confirm engine uses it

---

## Phase 2: Mobile / Responsive UI (Parallel with Phase 1)

### 2.1 Add Explicit Viewport Export [S]
**File:** `src/StackAlchemist.Web/src/app/layout.tsx:19-26`
- Add `export const viewport: Viewport` with `width: "device-width"`, `initialScale: 1`, `maximumScale: 5`, `viewportFit: "cover"`
- Import `Viewport` from `next`
- **Verify:** Inspect `<meta name="viewport">` in page source

### 2.2 Fix Advanced Mode React Flow on Mobile [M]
**File:** `src/StackAlchemist.Web/src/app/advanced/AdvancedModePage.tsx:612,616`
- Replace `hidden lg:flex` with collapsible "Preview" toggle visible on mobile/tablet
- Replace `minHeight: "400px"` with `min-h-[250px] md:min-h-[400px]`
- **Verify:** Test at 375px, 768px, 1024px viewports

### 2.3 Fix Simple Mode Touch Optimization [M]
**File:** `src/StackAlchemist.Web/src/app/simple/SimpleModePage.tsx`
- Enable `panOnDrag`, `zoomOnPinch` on React Flow
- Fix modal `fixed inset-0 py-8` for mobile keyboard avoidance (use `pb-safe` or dynamic viewport units `dvh`)
- **Verify:** Test touch interactions on mobile emulator

### 2.4 Reduce Background Blob Sizes [S]
**File:** `src/StackAlchemist.Web/src/app/page.tsx:290-296`
**File:** `src/StackAlchemist.Web/src/app/pricing/page.tsx` (similar lines)
- Replace `h-[800px] w-[800px]` with `h-[300px] w-[300px] md:h-[600px] md:w-[600px] lg:h-[800px] lg:w-[800px]`
- Same for 600px blobs
- **Verify:** No horizontal overflow on 375px viewport

### 2.5 Add Mobile Navbar Menu [M]
**File:** `src/StackAlchemist.Web/src/components/navbar.tsx`
- Convert to client component with hamburger toggle (Menu/X from lucide-react)
- Add mobile dropdown with navigation links
- Home page (`page.tsx:347-368`) already has working mobile menu -- extract and share pattern
- **Verify:** Hamburger appears at 375px, links accessible

### 2.6 Add prefers-reduced-motion [S]
**File:** `src/StackAlchemist.Web/src/app/globals.css`
- Add `@media (prefers-reduced-motion: reduce)` rule zeroing animation/transition durations
- **Verify:** Enable "Reduce motion" in OS, confirm no animations

### 2.7 Add Global Focus Indicators [S]
**File:** `src/StackAlchemist.Web/src/app/globals.css`
- Add `:focus-visible { outline: 2px solid var(--color-electric, #4da6ff); outline-offset: 2px; }`
- **Verify:** Tab through pages, all interactive elements show visible outline

---

## Phase 3: SEO (Parallel with Phase 2)

### 3.1 Add Open Graph + Twitter Card Tags [S]
**File:** `src/StackAlchemist.Web/src/app/layout.tsx:19-26`
- Expand `metadata` with `metadataBase`, `openGraph` (type, title, description, siteName, images), `twitter` (card, title, description, images)
- Use template pattern: `title: { default: "StackAlchemist", template: "%s | StackAlchemist" }`
- Create `public/og-image.png` (1200x630) with StackAlchemist branding
- **Verify:** metatags.io or Twitter Card Validator

### 3.2 Add Per-Page Metadata [M]
**Files missing metadata (confirmed -- only root layout and docs layout have it):**
- `src/app/about/page.tsx`
- `src/app/pricing/page.tsx`
- `src/app/story/page.tsx`
- `src/app/login/page.tsx`
- `src/app/register/page.tsx`
- `src/app/dashboard/page.tsx`
- `src/app/simple/page.tsx`
- `src/app/advanced/page.tsx`
- Add `export const metadata: Metadata` to each with unique title + description
- For client-component pages, metadata must be in the server `page.tsx` wrapper
- **Verify:** View source on each page, confirm unique `<title>` tags

### 3.3 Create sitemap.ts + robots.ts [S]
**New files (confirmed neither exists):**
- `src/StackAlchemist.Web/src/app/sitemap.ts` -- all static routes + dynamic doc slugs
- `src/StackAlchemist.Web/src/app/robots.ts` -- allow public pages, disallow `/dashboard`, `/generate`, `/api`
- **Verify:** Visit `/sitemap.xml` and `/robots.txt` in browser

### 3.4 Add JSON-LD Structured Data [S]
**File:** `src/StackAlchemist.Web/src/app/layout.tsx`
- Add `SoftwareApplication` schema via `<script type="application/ld+json">`
- Add `Product` schema on pricing page with tier pricing
- **Verify:** Google Rich Results Test

### 3.5 Configure Image Optimization [S]
**File:** `src/StackAlchemist.Web/next.config.ts`
- Add `images: { formats: ["image/avif", "image/webp"] }`
- Audit all `<Image>` components for missing `alt` text
- **Verify:** Network tab shows WebP/AVIF format

---

## Phase 4: Code Quality (Parallel with Phases 2-3)

### 4.1 Decompose SimpleModePage.tsx [L]
**File:** `src/StackAlchemist.Web/src/app/simple/SimpleModePage.tsx` (859 lines, ~40KB)
- Extract: `simple/types.ts`, `simple/constants.ts`, `simple/hooks/useSchemaGraph.ts`
- Extract shared: `components/schema-editor.tsx`, `components/tier-selector.tsx`
- Keep `SimpleModePage.tsx` as orchestrator
- **Verify:** `pnpm lint`, `pnpm build`, manual test of simple mode flow

### 4.2 Decompose AdvancedModePage.tsx [L]
**File:** `src/StackAlchemist.Web/src/app/advanced/AdvancedModePage.tsx` (671 lines, ~36KB)
- Extract each step: `steps/StepEntities.tsx`, `steps/StepRelationships.tsx`, `steps/StepEndpoints.tsx`, `steps/StepReview.tsx`, `steps/StepTier.tsx`
- Extract shared types to `advanced/types.ts`
- Reuse `tier-selector.tsx` from Task 4.1
- **Verify:** `pnpm lint`, `pnpm build`, manual test all 5 wizard steps

### 4.3 Clean Up Legacy "Bolt.new" References [S]
**Confirmed locations:**
- `src/StackAlchemist.Web/src/app/error.tsx:23` -- "In Bolt.new this usually means a backend-only path was reached" -- rewrite to StackAlchemist context
- `src/StackAlchemist.Web/src/lib/demo-data.ts:86,91` -- demo data references Bolt.new -- update to StackAlchemist
- `docs/product/Market Requirements Document.md:19` -- competitor mention, this one is intentional/correct
- **Verify:** `grep -r "Bolt.new" src/` returns only the intentional docs reference

### 4.4 Add Per-Route Error Boundaries [S]
**New files:**
- `src/app/simple/error.tsx`
- `src/app/advanced/error.tsx`
- `src/app/generate/[id]/error.tsx`
- `src/app/dashboard/error.tsx`
- Each: context-appropriate error message + "Try Again" button, StackAlchemist branding (not generic slate-800)
- **Verify:** Temporarily throw in each route, confirm boundary catches

### 4.5 Clean appsettings.json Secrets [S]
**File:** `src/StackAlchemist.Engine/appsettings.json`
- Remove empty-string secret values (ApiKey, SecretKey, etc.) -- they come from env vars via `Program.cs` mapping
- Keep only non-secret config: Model, MaxTokens, BucketName, PresignedUrlExpiryHours
- **Verify:** `dotnet build`, engine starts in dev mode

### 4.6 Enhance ESLint Config [S]
**File:** `src/StackAlchemist.Web/.eslintrc.json`
- Add: `no-console` (warn, allow warn/error), `@typescript-eslint/no-unused-vars` (warn), `react-hooks/exhaustive-deps` (warn)
- Run `pnpm lint --fix`, address warnings
- **Verify:** `pnpm lint` passes clean

---

## Phase 5: Documentation Revision (After Phases 1-4)

### 5.1 Audit & Revise All Markdown Files [M]
**31 docs across 6 directories. Priority order:**

1. **`README.md`** -- first thing users/contributors see. Verify quickstart commands, dependency versions, feature list
2. **`docs/user/getting-started.md`** -- verify matches current UI flow
3. **`docs/architecture/Software Design Document.md`** -- verify matches current implementation
4. **`docs/advanced-docs/architecture-overview.md`** -- verify current state
5. **`docs/user/` (7 files)** -- verify UI flows, tiers, pricing ($299/$599/$999), screenshots if any
6. **`docs/architecture/` (8 files)** -- verify ERD matches migrations, state machine matches `GenerationStateMachine.cs`, data flow matches current endpoints
7. **`conductor/` (7 files)** -- verify operational procedures still valid, remove stale runbooks
8. **`CI-CD-STATUS-REPORT.md`** -- update or remove if stale
9. **`AGENTS.md`** -- verify agent descriptions match current code
10. **`docs/product/` (6 files)** -- verify product docs reflect current feature set

**Check for:**
- References to phases as "future work" when already implemented
- Code snippets that no longer compile
- API endpoints that changed
- Pricing that doesn't match UI
- Any remaining "Bolt.new" references (intentional competitor mentions OK)

### 5.2 Run SEO Audit Tool [S]
- Use `/audit-website` skill against the deployed site URL
- Address findings in Phase 3 tasks or create follow-up items
- **Verify:** Re-run audit, confirm score improvements

---

## Execution Strategy

```
Week 1:
  Dev A (Backend): Phase 1 tasks 1.1-1.7
  Dev B (Frontend): Phase 2 tasks 2.1-2.7 + Phase 3 tasks 3.1-3.5

Week 2:
  Dev A: Phase 4 tasks 4.1-4.2 (component decomposition)
  Dev B: Phase 4 tasks 4.3-4.6 (cleanup)

Week 3:
  Both: Phase 5 (docs revision) + integration testing + SEO audit
```

**Dependencies:**
- Task 1.3 and 1.6 need Supabase migrations (can be batched)
- Task 4.2 depends on 4.1 (shared `tier-selector` component)
- Phase 5 should run last so docs reflect final state
- All other tasks are independent and parallelizable

---

## Verification Checklist (End-to-End)

- [ ] Engine refuses to start in prod without `ANTHROPIC_API_KEY`
- [ ] LLM calls retry on 429/5xx with backoff
- [ ] Token usage persisted to DB after each generation
- [ ] Schema with 25+ entities rejected
- [ ] Prompt injection in personalization fields stripped
- [ ] Build logs survive concurrent appends
- [ ] All pages render correctly at 375px, 768px, 1024px
- [ ] React Flow visible and interactive on mobile
- [ ] No horizontal overflow on any page
- [ ] Tab navigation shows focus rings on all interactive elements
- [ ] Animations respect prefers-reduced-motion
- [ ] Every page has unique `<title>` and `<meta description>`
- [ ] `/sitemap.xml` and `/robots.txt` serve valid content
- [ ] OG tags render in social media preview tools
- [ ] JSON-LD validates in Google Rich Results Test
- [ ] No "Bolt.new" references in application code
- [ ] `pnpm lint` and `pnpm build` pass clean
- [ ] `dotnet build` and `dotnet test` pass
- [ ] All docs accurate to current implementation
