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
- Marketing surfaces now use softer radii and restrained shadows. Do not regress them back to a flat sharp-corner treatment.

### Marketing UI refinements
- The live palette shifted toward an elevated-slate look: `#1E293B` base, `#334155` / `#475569` support surfaces, and `#4DA6FF` as the main accent.
- The landing page now uses a full-height narrative hero first, with the "Launch Console" moved into its own section directly below.
- The home prompt includes a prompt-builder layer with grouped preset chips so visitors can compose an architecture brief quickly.
- Supporting content on the home page is attached to the launch console instead of competing with the opening hero.
- The pricing page header now uses the shared logo treatment and an explicit `Home` link.

### Routing
- `/` — Landing page (hero, features, pricing teaser)
- `/simple?q=<prompt>` — Simple Mode: generation animation → editable React Flow canvas
- `/advanced?step=<1|2|3|4>` — Advanced Mode: 4-step stepper wizard

### Pricing placement
- Full pricing grid removed from landing page per owner feedback.
- Pricing details live exclusively in `/advanced?step=4`.
- Landing page has a teaser CTA linking to that route.

### Environment / Tooling
- npm cache env var `npm_config_cache` was set to `F:\packages\npm` (F: drive does not exist).
- `.npmrc` updated to `G:\packages\npm`. Must prefix npm commands with `npm_config_cache="G:/packages/npm"` or fix the shell env permanently.
- `next.config.ts`: `outputFileTracingRoot: __dirname` set to suppress pnpm-lock.yaml workspace root warning.

---

## Phase 4 — Live LLM Orchestration, R2 Upload & Delivery (2026-04-02)

### Architecture Change: Combined-Process Pipeline
The Engine and Worker are now a **single deployable process**. `CompileWorkerService` (and the compile pipeline interfaces) were promoted from `StackAlchemist.Worker.Services` → `StackAlchemist.Engine.Services` so they can be registered as `IHostedService` inside the Engine, sharing the same in-process `Channel<GenerationContext>`.

The Worker project is preserved as a standalone host option for future scale-out (replace Channel with Redis Streams / RabbitMQ + separate Worker deployment).

### New Services

| Service | Interface | Responsibility |
|---|---|---|
| `AnthropicLlmClient` | `ILlmClient` | Calls the Anthropic Messages API (Claude 3.5 Sonnet) via `IHttpClientFactory`. Named client `"Anthropic"`, base URL pre-configured at registration. |
| `CloudflareR2UploadService` | `IR2UploadService` | Zips generated project directory in-memory (`ZipFile.CreateFromDirectory`), uploads via AWSSDK.S3 with R2 endpoint override, returns presigned GET URL. |
| `SupabaseDeliveryService` | `IDeliveryService` | PATCHes the `generations` row in Supabase PostgREST after every state transition so the frontend receives real-time status via Supabase Realtime. No-ops silently if `Supabase:Url` / `Supabase:ServiceRoleKey` are not configured. |
| `CompileService` | `ICompileService` | Moved to Engine namespace from Worker. |
| `CompileWorkerService` | `BackgroundService` | Moved to Engine namespace; extended with `IR2UploadService` + `IDeliveryService` constructor params. Replaced Phase 3 fake `ZipCreated`/`UploadedToR2` skip with real R2 upload + Supabase delivery. |

### Config-Based LLM Switching
`Program.cs` registers `AnthropicLlmClient` when `Anthropic:ApiKey` is non-empty; falls back to `MockLlmClient` otherwise. No code change required to switch from mock to live.

### New Configuration Sections
`appsettings.json` gains three new sections (populated via user-secrets / env in deployment):

```json
"Anthropic":    { "ApiKey": "", "Model": "claude-3-5-sonnet-20241022", "MaxTokens": "8192" }
"CloudflareR2": { "AccountId": "", "AccessKeyId": "", "SecretAccessKey": "",
                  "BucketName": "stackalchemist-builds", "PresignedUrlExpiryHours": "168" }
"Supabase":     { "Url": "", "ServiceRoleKey": "" }
```

### ProjectName Derivation
`GenerationOrchestrator.BuildVariables` now derives a PascalCase project name automatically:
1. Schema entities present → `"{Entity0}{Entity1}"` (or `"{Entity0}App"` for single-entity schemas)
2. Simple Mode prompt → first two non-stop-word words PascalCased
3. Fallback → `"GeneratedApp"`

### New NuGet Packages
- `AWSSDK.S3` `3.7.*` added to Engine (R2 is S3-compatible; `ForcePathStyle = true`, `AuthenticationRegion = "auto"`)
- `UserSecretsId` `"stackalchemist-engine"` added to Engine.csproj

### Test Coverage
New unit tests added to `Engine.Tests`:
- `AnthropicLlmClientTests` — 5 tests (happy path, missing key, 429 error, empty content block, header verification)
- `SupabaseDeliveryServiceTests` — 6 tests (PATCH sent, status body, download URL, no-op when unconfigured, non-throw on 5xx, auth headers)
- `CloudflareR2UploadServiceTests` — 3 unit tests (missing AccountId / AccessKeyId / SecretAccessKey config), 1 skipped integration test

### Test Results
```
Engine.Tests:  66 passed, 6 skipped (Docker integration + R2 integration — intentional)
Worker.Tests:  22 passed, 3 skipped (.NET SDK integration — intentional)
```
Zero failures across both suites.

---

## Phase 4 — Gap Closure: Schema, RLS, Extraction, Build Logs (2026-04-04)

### Supabase Migrations (checked in)
Three migration files added at `supabase/migrations/`:
- `20260404000001_create_profiles.sql` — profiles table with auto-create trigger on auth.users insert, RLS (read/update own)
- `20260404000002_create_transactions.sql` — transactions table with Stripe session uniqueness, RLS (read own + service_role manages)
- `20260404000003_create_generations.sql` — generations table matching TypeScript `Generation` type, including `build_log` and `preview_files_json` columns, `updated_at` auto-trigger, RLS (anyone inserts/reads, service_role updates), Realtime publication enabled

### Schema matches TypeScript types
`types.ts` now includes `Profile`, `Transaction`, and updated `Generation` (with `build_log`, `transaction_id`). The `Database` type covers all three tables.

### Schema Extraction Endpoint
New `POST /api/extract-schema` endpoint on the Engine:
- Accepts `{ generationId, prompt }`, updates status to `extracting_schema`
- Calls `IPromptBuilderService.BuildSchemaExtractionPrompt()` → `ILlmClient` → `ISchemaExtractionService.ParseExtractionResponse()`
- Persists extracted `schema_json` to Supabase via new `IDeliveryService.UpdateSchemaAsync()`
- Returns `{ generationId, schema }` on success; 400 with error on extraction/validation failure

### Simple Mode Wired to LLM Extraction
`SimpleModePage` now calls `extractSchema()` server action on load instead of showing hardcoded entities.
Flow: create pending generation → call Engine `/api/extract-schema` → map returned `GenerationSchema` to local editor types → render on React Flow canvas. Falls back to default example schema if extraction fails.

### Build Log Streaming
- `IDeliveryService` gained `AppendBuildLogAsync()` — fetches current `build_log`, appends chunk, PATCHes back
- `CompileWorkerService` streams build output at key points: before build, stdout, success/failure, error summaries
- `GenerateClientPage` `InProgressPanel` renders `generation.build_log` in a terminal-styled panel, updated live via Supabase Realtime

### IDeliveryService Expansion
Three new methods added to the interface:
- `UpdateStatusAsync(string generationId, string status, ...)` — string-based status overload for frontend-facing statuses
- `UpdateSchemaAsync(string generationId, GenerationSchema schema, ...)` — persists extracted schema
- `AppendBuildLogAsync(string generationId, string logChunk, ...)` — append-style build log updates

Internal refactor: extracted `PatchGenerationAsync()` shared helper to eliminate PATCH duplication.

### Test Results
```
Engine.Tests:  66 passed, 6 skipped
Worker.Tests:  22 passed, 3 skipped
Frontend:      next build + next lint pass clean
```

---

## Phase 3 — Engine Services & Test Coverage (2026-04-02)

### New Services
Three pure-logic services added to `StackAlchemist.Engine`:

| Service | Interface | Responsibility |
|---|---|---|
| `TierGatingService` | `ITierGatingService` | Maps tier (1/2/3) to `TierDeliverables` record; guards invalid values with `InvalidTierException` |
| `SchemaExtractionService` | `ISchemaExtractionService` | Parses raw LLM JSON (possibly markdown-fenced) into `GenerationSchema`; validates relationship references |
| `PromptBuilderService` | `IPromptBuilderService` | Builds generation, retry, and schema-extraction prompts for Claude 3.5 Sonnet |

### New Models
- `TierDeliverables` positional record in `Models/TierModels.cs` — immutable struct of bool flags per deliverable type.

### New Exceptions
Three new typed exceptions added to `Models/Exceptions.cs`:
- `SchemaExtractionException` — malformed / unparseable LLM JSON response
- `SchemaValidationException` — valid JSON but references non-existent entity in a relationship
- `InvalidTierException` — tier value outside 1–3 range

### Stripe Webhook
- `Stripe.net` **51.0.0** added to `StackAlchemist.Engine.csproj`
- `POST /api/webhooks/stripe` endpoint in `Program.cs`
  - Reads raw body, calls `EventUtility.ConstructEvent()` with `Stripe-Signature` header
  - Returns `401 Unauthorized` on signature failure
  - Handles `checkout.session.completed` → enqueues `GenerateRequest` with tier + prompt from session metadata
  - Idempotency key is `stripeEvent.Id` (logged; downstream dedup in Phase 4)
- `appsettings.json` gains `Stripe:{ PublishableKey, SecretKey, WebhookSecret }` keys (empty defaults; populated via user-secrets / env in deployment)

### DI Registration
`Program.cs` now registers all three Phase 3 services as singletons alongside the existing pipeline.

### Test Coverage
Scaffold tests (`Assert.True(true, "Scaffold: ...")`) were promoted to real xUnit tests:
- `TierGatingServiceTests` — 7 tests (3 tier deliverables, 4 invalid tier variations, 2 code-gen gating)
- `SchemaExtractionServiceTests` — 4 tests (happy path, malformed JSON, markdown-fenced JSON, bad relationship reference)
- `PromptBuilderTests` — 6 tests (entity inclusion, delimiter instructions, retry with errors, accumulated errors, extraction prompt content, token-budget guard)

### Test Results
```
Engine.Tests:  52 passed, 5 skipped (Docker integration — intentional)
Worker.Tests:  22 passed, 3 skipped (.NET SDK integration — intentional)
```
Zero failures across both suites.

---

## Phase 2 — Master Template Construction (2026-04-02)

### Template Structure
- Location: `src/StackAlchemist.Templates/V1-DotNet-NextJs/`
- Three subdirs: `dotnet/`, `nextjs/`, `infra/`
- Validation: `src/StackAlchemist.Engine.Tests/Services/TemplateProviderTests.cs` (unit) and `Integration/SwissCheeseEndToEndTests.cs` (e2e). The standalone `validate.mjs` Node script was removed in 2026-05; the C# tests cover both V1 and V2 template sets.

### Handlebars Variables
| Variable | Usage |
|---|---|
| `{{ProjectName}}` | PascalCase project name (e.g. GymManager) |
| `{{ProjectNameKebab}}` | kebab-case (e.g. gym-manager) for npm name |
| `{{ProjectNameLower}}` | lowercase (e.g. gymmanager) for DB name, Docker |
| `{{DbConnectionString}}` | Full Npgsql connection string |
| `{{FrontendUrl}}` | CORS allowed origin |

### LLM Injection Zones
All zones use `[[LLM_INJECTION_START: ZoneName]]` / `[[LLM_INJECTION_END: ZoneName]]` comments.

| Zone | File | Purpose |
|---|---|---|
| `RepositoryRegistrations` | `Program.cs` | DI registration for each repo |
| `RouteRegistrations` | `Program.cs` | `app.MapGroup(...)` calls |
| `Controllers` | `dotnet/Controllers/_placeholder.cs` | Minimal API endpoint groups |
| `Repositories` | `dotnet/Repositories/_placeholder.cs` | Dapper repository classes |
| `Models` | `dotnet/Models/_placeholder.cs` | C# records per entity |
| `SqlSchema` | `dotnet/Migrations/001_initial_schema.sql` | CREATE TABLE + RLS |
| `HomePageContent` | `nextjs/src/app/page.tsx` | Entity listing sections |
| `ApiRouteHandlers` | `nextjs/src/lib/api.ts` | Typed fetch helpers per entity |
| `TypeDefinitions` | `nextjs/src/types/index.ts` | TypeScript interfaces per entity |

### Validation result
- 22 templates rendered with mock data (GymManager project, User + Plan entities)
- 0 failures — all Handlebars expressions resolved, no stray `{{` in output

### .NET Engine
- `dotnet new webapi --no-https --use-controllers=false --framework net10.0`
- Minimal API template. No controllers yet — Phase 3 will add services.
- 1 warning: invalid VS BuildTools LIB path in env — harmless, pre-existing env issue.

---

## Phase 6 — Supabase SSR Session Propagation + User Dashboard (2026-04-04)

### Session Architecture
- **`@supabase/ssr` installed** — replaces the hand-rolled `createClient` browser singleton with `createBrowserClient` for cookie-based session propagation in Client Components.
- **`src/middleware.ts`** — runs on every non-asset request; calls `getUser()` to silently refresh the JWT and write updated session cookies back via `Set-Cookie` headers. Bails out early when `NEXT_PUBLIC_SUPABASE_URL` is not set so demo/CI runs are unaffected.
- **`src/lib/supabase-server.ts`** — new file exporting `createSupabaseServerClient()` (anon-key, cookie-backed) and `getServerUser()` (always uses `getUser()`, never `getSession()`, to avoid trusting stale cookie data).
- **`src/lib/supabase.ts`** — `createBrowserClient` from `@supabase/ssr` replaces raw `createClient`. Service-role `createServerClient()` retained for engine-triggered writes that must bypass RLS.

### Auth Routes
- **`/auth/callback`** (GET Route Handler) — handles PKCE code exchange for magic-link sign-ins and email confirmations. `emailRedirectTo` in login/register updated to point here with `?next=` param.
- **`/auth/signout`** (POST Route Handler) — signs out and redirects to `/`. Called via native `<form method="POST">` in navbar and dashboard header.

### User-Linked Generations
- `submitSimpleGeneration`, `submitAdvancedGeneration`, and `createPendingGeneration` now call `getServerUser()` and pass `user_id: user?.id ?? null` to every `generations` insert. Anonymous generations remain allowed (`user_id = null`).

### `getMyGenerations` Server Action
- Queries `generations` by `user_id = currentUser.id`, ordered newest-first, limit 50.
- Returns `[]` when anonymous or Supabase is unconfigured.

### Navbar — Async Server Component
- `Navbar` promoted from a plain Server Component to an `async` Server Component.
- When `getServerUser()` returns a user: shows email badge (md+), Dashboard link, and a Sign-Out `<form>` button.
- When anonymous: shows the existing Login link.

### `/dashboard` Page
- Auth-gated: `redirect("/login?returnTo=/dashboard")` when unauthenticated.
- Stats row: Total / Complete / In Progress counts.
- Generation list: tier badge, mode tag, prompt preview, status badge, View link, Download link (paid + complete only).
- BYOK settings card: account email, disabled API key input and model selector (fields wired in Phase 7).

### E2E Tests (dashboard.spec.ts)
- Existing scaffold tests promoted to real assertions:
  - Unauthenticated `/dashboard` → redirect to `/login?returnTo=...`
  - Login page: heading, sign-in button, magic-link toggle hides password field
  - Register page: heading, submit button, client-side mismatch validation

### Test Results
```
Frontend:  next build + next lint pass clean
E2E:       4 live assertions, 2 intentional skips (Phase 7)
```

---

## Phase 5 — Stripe Payment Gate + Supabase Auth (2026-04-04)

### Payment Architecture
- **Pre-payment row**: For paid tiers 1–3, `createPendingGeneration()` inserts a `generations` row with `status=pending` *before* redirecting to Stripe. Engine is NOT called at this point.
- **Webhook-triggered execution**: Engine fires only when Stripe confirms `checkout.session.completed`. The `generationId` is stored in Stripe session metadata so the webhook correlates payment → generation.
- **Tier 0 path unchanged**: Free Spark tier calls `submitAdvancedGeneration()` / `submitSimpleGeneration()` directly — creates row AND fires Engine immediately.
- **No client-side Stripe SDK**: Frontend has zero Stripe JS dependencies. Hosted Checkout URL is returned by the Engine (`/api/stripe/create-session`) and the browser does a hard redirect.

### Engine: `/api/stripe/create-session` Endpoint
- Input: `{ generationId, tier, successUrl, cancelUrl, prompt? }`
- Output: `{ sessionId, url }` — Stripe-hosted Checkout URL
- Pricing (hardcoded; no Stripe Price IDs required):
  - Tier 1 Blueprint: $299 → `29_900` cents
  - Tier 2 Boilerplate: $599 → `59_900` cents
  - Tier 3 Infrastructure: $999 → `99_900` cents
- `StripeException` caught → 400 Bad Request.

### Engine: Transaction Row on `checkout.session.completed`
- Inserts row into `transactions` table via Supabase PostgREST immediately before enqueuing the generation.
- Non-fatal: insert failure logs error but does NOT block generation enqueue.
- Fields: `stripe_session_id`, `tier`, `amount` (cents from Stripe), `status = "completed"`.

### Frontend: New Server Actions
- `createPendingGeneration(mode, tier, prompt?, schema?)` — inserts DB row, returns `generationId`. No Engine call.
- `createCheckoutSession(generationId, tier, prompt?)` — calls Engine → returns Stripe URL. Falls back to `/generate/{id}?demo=1` in demo/no-Stripe environments.

### Supabase Auth Pages
- `/login` — Email+password or Magic Link. Supports `?returnTo` query param.
- `/register` — Email+password signup with confirm-password client validation. Shows email-verification success screen.
- Auth is **optional** — anonymous generations allowed, `user_id = null`. Full `@supabase/ssr` session propagation deferred to Phase 6.

### Navigation
- Desktop and mobile "Sign In" changed from `<button>` to `<Link href="/login">`.

### `runtime-config.ts` Addition
- `hasStripeConfig()` — returns `true` when `STRIPE_SECRET_KEY` is set.

---

## Audit + Test Suite Expansion Pass (2026-04-04)

### Implementation Status Corrections
- Re-audited current implementation claims against live code and corrected stale documentation that still described the repo as "Phase 1 only".
- Updated status banners/notes in:
  - `docs/architecture/Software Design Document.md`
  - `docs/product/Product Design Document.md`
  - `docs/product/Product Requirements Document.md`
  - `docs/DEV_PROMPT.md` progress tracker
- Confirmed current state: orchestration + compile pipeline + Stripe webhook/session backend + auth/dashboard shell + Supabase migrations are implemented; personalization wizard and BYOK persistence UX remain pending.

### Test Coverage Additions
- **Engine tests added:**
  - `CompileServiceTests` (error extraction + retry-context composition)
  - `MockLlmClientTests` (delimited output format + core artifact presence)
  - `GenerationOrchestratorTests` already present and retained as part of expanded suite
- **Worker tests expanded:**
  - `RetryLogicTests` gained null-context BuildFailed transition coverage
- **Web unit tests added (Vitest):**
  - `__tests__/lib/runtime-config.test.ts`
  - `__tests__/lib/demo-data.test.ts`
- **Web E2E tests expanded (Playwright):**
  - `checkout-flow.spec.ts` converted from scaffolded skips to concrete pricing/tier assertions
  - `dashboard.spec.ts` gained generate-not-found and explicit `returnTo` redirect assertions

### Verification Results
- `dotnet test StackAlchemist.slnx --logger "console;verbosity=minimal"`
  - **Engine.Tests:** 73 passed, 6 skipped
  - **Worker.Tests:** 23 passed, 3 skipped
  - 0 failures
- `pnpm --dir "src/StackAlchemist.Web" exec vitest run __tests__/lib/runtime-config.test.ts __tests__/lib/demo-data.test.ts`
  - 2 files, 5 tests passed, 0 failed

### Documentation Sync
- `README.md` test badge/counts updated to reflect verified test totals.
- Progress guidance in `docs/DEV_PROMPT.md` updated so next implementation work starts from accurate current reality rather than stale phase assumptions.

---

## Multi-Ecosystem Expansion Pass (2026-04-05)

### Generation model + persistence
- Added `ProjectType` support across the engine request/response pipeline for `DotNetNextJs` and `PythonReact`.
- Added `project_type` to the Supabase `generations` table via migration `20260405000004_add_project_type_to_generations.sql`.
- Next.js server actions now persist `project_type` on every generation row and resend it on retry.

### Compile pipeline strategy split
- `CompileService` is now a thin selector over per-platform `IBuildStrategy` implementations.
- `DotNetBuildStrategy` runs `.NET` validation and extracts C# compiler errors.
- `PythonReactBuildStrategy` runs backend `pip` + `flake8` + `pytest --collect-only` and frontend `npm install` + `eslint` + `tsc`, plus Python/TypeScript/ESLint error extraction.
- `CompileWorkerService` now logs the selected project type in the live build stream instead of hardcoding `dotnet build`.

### Advanced mode platform selection
- Advanced Mode is now a 4-step flow:
  1. Define Entities
  2. Platform Selection
  3. Configure API
  4. Select Tier & Pay
- The submission spinner now includes a shared live build-log console backed by Supabase Realtime updates from `build_log`.

### Template + orchestration updates
- `GenerationOrchestrator` resolves template roots by `ProjectType` and uses `PromptBuilderService.BuildGenerationPrompt(schema, projectType)` for schema-backed generation requests.
- `V1-Python-React/` now includes the shared injection zone names expected by reconstruction (`Controllers`, `Models`, `Repositories`, `TypeDefinitions`, etc.).
- Added Python/React validation assets: `.flake8`, `pytest.ini`, sample backend health test, and a modern `eslint.config.js`.
- Render validation lives in C# (`TemplateProviderTests`, `SwissCheeseEndToEndTests`) — covers both V1 and V2 template sets. The Node `validate.mjs` was retired in 2026-05.

### Paid checkout reliability
- Stripe Checkout session metadata now carries `projectType`.
- The Stripe webhook now reloads `mode`, `prompt`, `schema_json`, and `project_type` from Supabase before enqueueing the engine job, fixing the preexisting paid Advanced Mode loss of schema context after redirect.

### Test coverage
- Added `MultiEcosystemPipelineTests` to verify template-set selection and queue propagation for both ecosystems.
- Expanded compile-service tests with Python/ESLint error parsing coverage.
- Updated Playwright Advanced Mode coverage for the new platform-selection step and shifted tier checkout to step 4.

### Infrastructure audit
- Verified current GitHub workflow secret names remain environment-scoped without test/prod key prefixes (`SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY`, `R2_*`, `STRIPE_*`).
- Verified the root Dockerfile remains npm-based for web/worker Node workflows (`npm install`, `npx next build`) and did not regress to pnpm.

---

## Phase 4.7 — Personalization Wizard (2026-04-05)

### Personalization Model
`GenerationPersonalization` added to `Models/GenerationModels.cs` with the following structure:

| Field | Type | Purpose |
|---|---|---|
| `BusinessDescription` | `string` | 2-3 sentence business context injected into LLM prompt for domain-aware generation |
| `ProjectName` | `string?` | User-chosen project/company name for README, comments, env config |
| `Tagline` | `string?` | Optional tagline injected into generated README and landing page |
| `ColorScheme` | `PersonalizationColorScheme?` | Selected palette (6 presets + custom) → injected into generated `tailwind.config.ts` |
| `DomainContext` | `Dictionary<string, string>` | Entity name → domain description mapping for realistic code comments and seed data |
| `FeatureFlags` | `PersonalizationFeatureFlags?` | Auth method (jwt/cookie/oauth/none), soft-delete, audit timestamps, swagger, docker-compose |

### Color Scheme Presets
`PersonalizationColorScheme` carries: Id, Name, Primary, Secondary, Accent, Background, Surface hex values. Six curated presets available plus fully custom palette via color picker.

### Feature Flags
`PersonalizationFeatureFlags` defaults: AuthMethod = "jwt", SoftDelete = false, AuditTimestamps = true, IncludeSwagger = true, IncludeDockerCompose = true.

### Storage
- New Supabase column `personalization_json` (JSONB, nullable) added via migration `20260405000005_add_personalization_to_generations.sql`
- Stored alongside `schema_json` in the `generations` table
- Stripe webhook reloads personalization from Supabase before enqueueing generation

### Frontend
- `personalization-modal.tsx` implements the 4-step wizard (Business Identity → Color Scheme → Domain Context → Feature Toggles)
- Integrated into both Simple Mode and Advanced Mode flows after schema confirmation
- Skippable — sensible defaults applied when user opts out

### Orchestrator Integration
- `GenerateRequest` and `GenerationContext` carry `Personalization` field
- `PromptBuilderService` injects business description and domain context into the Claude 3.5 generation prompt
- Color scheme and feature flags injected into Handlebars template context for deterministic config generation

---

## Phase 7 — Security Hardening (2026-04-06)

### Rate Limiting
Fixed-window rate limiting via `Microsoft.AspNetCore.RateLimiting`:

| Policy | Endpoint | Limit | Window |
|---|---|---|---|
| `generate` | `POST /api/generate` | 5 requests | 1 minute |
| `extract` | `POST /api/extract-schema` | 15 requests | 1 minute |
| `stripe-session` | `POST /api/stripe/create-session` | 3 requests | 1 minute |

429 responses include JSON error body. Per-IP tracking.

### CORS
- `Engine:AllowedOrigins` config key (defaults to `http://localhost:3000`)
- Supports comma-separated origins for multi-domain deployments
- Applied via `AddCors` / `UseCors("Frontend")` in the middleware pipeline

### Service Key Authentication
- `X-Engine-Key` header required on all `/api/*` routes (except `/api/webhooks/*`)
- Configured via `ENGINE_SERVICE_KEY` environment variable
- When unset, auth check is skipped (local dev / CI works without extra config)
- Production startup validation fails fast if `ENGINE_SERVICE_KEY` is missing

### Prompt Sanitization
- Input sanitization applied to LLM-calling routes to mitigate prompt injection against Claude 3.5 calls

### Production Startup Validation
- `STRIPE_WEBHOOK_SECRET` and `ENGINE_SERVICE_KEY` are required in Production environment
- Missing values throw `InvalidOperationException` at startup for fail-fast behavior

---

## Multi-Ecosystem Build Strategy (2026-04-05)

### IBuildStrategy Pattern
`CompileService` was refactored from a monolithic dotnet-only builder to a strategy pattern:

| Strategy | Ecosystem | Validation Steps |
|---|---|---|
| `DotNetBuildStrategy` | V1-DotNet-NextJs | `dotnet build` → C# compiler error extraction |
| `PythonReactBuildStrategy` | V1-Python-React | `pip install` + `flake8` + `pytest --collect-only` (backend) → `npm install` + `eslint` + `tsc` (frontend) → Python/TypeScript/ESLint error extraction |

Both strategies share a common `BuildStrategyBase` and plug into the existing `CompileWorkerService` retry loop unchanged.

### ProjectType Enum
- `DotNetNextJs` (default) and `PythonReact`
- Carried through the entire pipeline: `GenerateRequest` → `GenerationContext` → template resolution → build strategy selection
- Persisted in `generations.project_type` column (migration `20260405000004`)
- Stripe checkout metadata includes `projectType` for webhook recovery

### Template Expansion
- `V1-Python-React/` template set added under `src/StackAlchemist.Templates/`
- FastAPI backend with SQLAlchemy + Alembic migrations + Pydantic schemas
- React frontend with Vite + TypeScript + Tailwind
- Shared injection zone naming (`Controllers`, `Models`, `Repositories`, `TypeDefinitions`) for reconstruction compatibility
- (Historical) `validate.mjs` updated to render-check both template sets — script retired 2026-05; replaced by C# template tests.

---

<!-- Date-titled ADRs below were consolidated in from the legacy root DECISIONS.md
     (2026-05-07) so this file is the single canonical decisions log. Phase
     narratives above and date-titled ADRs below coexist by design. -->

## ADR Format

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

> **Note (2026-05-07):** This entry inherited from a template; StackAlchemist's actual engine is `Microsoft.NET.Sdk.Web` net10.0 without `PublishAot=true`. Phase 1 above captures the real stack decision. Kept here for historical fidelity during the consolidation pass.

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
  - **Update 2026-05-07:** v4 emits `x-amz-checksum-crc32` + `STREAMING-UNSIGNED-PAYLOAD-TRAILER` by default and Cloudflare R2 returns 501. See 2026-05-06 Anthropic-bump entry below + the issue #92 fix; both checksum settings now pinned to `WHEN_REQUIRED` on the R2 client.
- **eslint 9.39.4 → 10.2.1** (PR #59):
  - **ESLint 10:** drops Node 18, requires Node 20.10+. CI on Node 24 → fine.
  - **Default config still flat config (`eslint.config.*`).** Legacy `.eslintrc` is officially gone.
  - Several deprecated rules removed; `typescript-eslint` already on 8.x is compatible.
  - Risk: low — most pain came at 9.x with flat-config migration; 10.x is incremental.
  - **Update 2026-04-29:** turned out plugin ecosystem peer caps blocked eslint 10 entirely; partial revert in next ADR.
- **lucide-react 0.454.0 → 1.11.0** (PR #55):
  - See steveackleyorg DECISIONS for the same bump. Pre-1.0 → 1.0 is mostly a rename. ESM-only now. Risk: low.
**Why no review:** private/solo repo, deploy workflows are the real build, revert is cheap.

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

---

## 2026-05-30 — Fix prod generation pipeline (build-time env, status persistence, stale reconcile)

**Status:** accepted
**Context:** Prod symptom — a user submits a generation, the engine runs, but the
UI never updates and no download appears ("nothing happens"). Investigation found
two root causes plus a latent third:
- **RC#1 — `NEXT_PUBLIC_*` missing from the web image.** Next inlines
  `NEXT_PUBLIC_*` at `next build`, not at runtime. The Dockerfile web-builder
  stage never received `NEXT_PUBLIC_SUPABASE_URL` / `NEXT_PUBLIC_SUPABASE_ANON_KEY`,
  so the browser Supabase client constructed against `undefined` and its
  `postgres_changes` Realtime subscription silently no-op'd — the page never saw
  any status transition. `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` /
  `NEXT_PUBLIC_PLAUSIBLE_DOMAIN` had the same gap.
- **RC#2 — orchestrator never persisted in-progress / terminal status.**
  `GenerationOrchestrator.RunAsync` left the row at `pending` through codegen and,
  on exception, set only the in-memory `context.State = Failed` without writing it
  back. A row that failed (or was still mid-flight) never moved off `pending`.
- **Latent — DB CHECK rejected three live statuses.**
  `GenerationState.{Generating,Packing,Uploading}.ToString().ToLowerInvariant()`
  emits `generating` / `packing` / `uploading`; the original
  `generations_status_check` (migration 20260404000003) allowed only
  `pending,extracting_schema,generating_code,building,success,failed`. PostgREST
  returned 400 and `SupabaseDeliveryService` swallowed it, so those progress
  transitions never reached the row. The pipeline still reached terminal
  `success`/`failed` (both valid), so this degraded the progress UI rather than
  breaking delivery.
**Decision:**
- Thread the four `NEXT_PUBLIC_*` values (Supabase URL + anon key, Stripe
  publishable key, Plausible domain) as Docker build `ARG`/`ENV` in the
  web-builder stage and pass them via `sa-web.build.args` in
  `docker-compose.prod.yml` and `docker/docker-compose.test.yml`. These are all
  public-by-design keys — `SUPABASE_SERVICE_ROLE_KEY` / `STRIPE_SECRET_KEY` stay
  runtime-only and are never build args.
- Persist status in the orchestrator: `generating_code` on entry (the DB enum is
  `generating_code`, not the `generating` build-retry alias) and `Failed` in the
  catch (with `CancellationToken.None`, so a cancelled request still records its
  failure).
- **Option A** for the enum↔CHECK mismatch (chosen over Option B = mapping the
  enum to existing DB strings in code): widen the CHECK via migration
  `20260530000009_widen_generations_status_check.sql` to add `generating`,
  `packing`, `uploading`, and add matching labels across the three frontend
  surfaces (`GenerateClientPage`, dashboard `StatusBadge`, `SimpleModePage`).
  Keeps the DB as the single source of truth and makes the full lifecycle
  observable end-to-end.
- Hardening: `PatchGenerationAsync` retries critical (`success`/`failed`)
  transitions and no longer swallows non-2xx silently; new
  `StartupReconciliationService` + `IDeliveryService.FailStaleNonTerminalAsync`
  mark jobs orphaned by an engine restart as `failed` (the in-memory `Channel`
  drops in-flight work on restart); `/api/extract-schema` gained a catch-all that
  persists `failed`; the generate page polls the `getGeneration` server action
  every 5s as a Realtime fallback (works even if the browser client is null).
**Consequences:**
- Generation status now streams to the browser in prod, and the full lifecycle
  (`pending → extracting_schema → generating_code → building → packing →
  uploading → success`, or `failed` at any point) is both persisted and shown.
- `NEXT_PUBLIC_*` are baked into the image at build time — changing any of them
  requires an image rebuild, not just a container restart. Test and prod compose
  each supply their own values.
- The stale-sweep covers every non-terminal status; window is configurable via
  `Generation:StaleReconcileMinutes` (default 15, `≤0` disables).
- Migration `20260530000009` must be applied to every environment (CI, test,
  prod) before the new web/engine images are deployed.

---

## 2026-06-10 — Dependabot major: Stripe.net 51.2.0 → 52.0.0

**Context:** Out-of-band major from Stripe. Pinned API version unchanged
(`2026-05-27.dahlia`). Sole breaking change: `tax_rate.tax_details` became
expandable (stripe/stripe-dotnet#3396).
**Decision:** Merged via Dependabot PR #134 without code changes.
**Consequences:** Zero usage of `tax_details`/`TaxDetails` in this repo
(verified by grep at merge time) — no-op upgrade. If tax-rate detail handling
is ever added, account for the expandable type.

---

## 2026-07-06 — Deferred: Microsoft.OpenApi 2.x → 3.x (major); pin advanced to 2.9.0

**Context:** Dependabot #206 proposed Microsoft.OpenApi 2.7.5 → 3.7.0. The build
fails in *generated* code: the Microsoft.AspNetCore.OpenApi 10.0.9 XML-comment
source generator assigns `IOpenApiMediaType.Example`, which 3.x made read-only
(CS0200 in OpenApiXmlCommentSupport.generated.cs). No first-party code uses
Microsoft.OpenApi directly — the reference exists only as a security floor
(GHSA-v5pm-xwqc-g5wc, patched in 2.7.5).
**Decision:** Stay on the 2.x line until Microsoft.AspNetCore.OpenApi ships
OpenApi 3.x support. Advance the pin 2.7.5 → 2.9.0 (latest 2.x). Add a
Dependabot ignore for Microsoft.OpenApi majors; close #206 via
`@dependabot ignore this major version`.
**Consequences:** Minor/patch updates within 2.x continue to flow. Remove the
ignore once AspNetCore.OpenApi is 3.x-compatible — check on .NET servicing
releases.

---
## 2026-08-15 — V1 block routing: zone name or real path, never both

**Context:** `Prompts/V1-generation.md` asked the model for `src/types/index.ts`,
`src/lib/api.ts`, `src/app/page.tsx` and `src/Models/*.cs`, but the rendered tree
keeps its halves under `dotnet/` and `nextjs/`. `Reconstruct` matched zones by
directory substring and then wrote every remaining block verbatim, so those paths
landed at the archive root as an orphan `src/` directory while `nextjs/src/` kept
its stubs. `dotnet build` and `npm run build` both passed — the stubs are valid —
so Tier-2/3 customers received a "Compile Verified" archive whose frontend had no
types, no API client and an empty page. The same substring matching also copied
each backend file into `_placeholder.cs` *and* the archive root, concatenating
several files' `using` directives into one file (CS1529).

**Decision:** One routing rule, no heuristics.
- A block whose path is `__zone__/<ZoneName>` fills that zone and is never written
  to disk. This is the only way to address a zone.
- Every other block is a real file and must sit under a top-level directory the
  rendered template actually produced (or be a bare root-level file name).
- Anything else throws `UnmappedLlmFileException` (a `MalformedLlmOutputException`,
  so `error_category` = `schema`) naming the offending paths and the valid roots.
- The V1 .NET half therefore emits one real file per entity under
  `dotnet/Models|Repositories|Controllers/`, each with its own namespace and usings.
  The `_placeholder.cs` files are now namespace anchors so `Program.cs` can `using`
  those namespaces even when the model contributes nothing.
- `BuildGenerationPrompt` takes the rendered project name: the tree's RootNamespace
  is schema-derived, and a prompt hardcoding `GeneratedApp` produced code whose
  namespaces do not exist in the project it is merged into.

**Consequences:** A misrouted generation now fails loudly instead of shipping a
half-empty archive — a visible, refundable failure beats a plausible-looking one.
The zone→file table under *Phase 2* above still describes where each zone lives,
but for V1 only `RepositoryRegistrations` and `RouteRegistrations` are still filled
by zone — they are the only two the prompt names and the only two the model is asked
to emit as `__zone__/…` blocks. Every other V1 zone is now dead: `Models`,
`Repositories` and `Controllers` are retired in favour of real per-entity files, and
`HomePageContent`, `ApiRouteHandlers`, `TypeDefinitions` and `SqlSchema` are retired
because the prompt asks for `nextjs/src/app/page.tsx`, `nextjs/src/lib/api.ts`,
`nextjs/src/types/index.ts` and `dotnet/Migrations/001_initial_schema.sql` as whole
files instead. The markers still sit in those four template files and collapse to
nothing when unfilled, which is exactly what keeps the bare template compiling; they
are harmless, not load-bearing, and should not be read as a live contract.
Guarded by `V1TemplateCompileTests.GoldenLlmResponse_*`, which reconstructs a
recorded response through the real services and builds both halves in CI.

---

## 2026-08-19 — Dependabot sweep: template-stack majors merged in bulk

**Status:** accepted (awareness-only stub per saved sweep policy)
**Decision:** merged the long-parked template-directory majors on green CI, plus Tier3 terraform.
- **terraform hashicorp/aws ~>5.84 → ~>6.60** (Tier3-Infrastructure, #301): template-only; consumers pick it up at scaffold time. Same provider-v6 plan-diff caution as live infra.
- **typescript 5.x → 7.0.2** across the template frontends and the Tier3 CDK dir; **next 15.5.x → 16.3.x** across the NextJs templates; **@types/node 22 → 26**; **docker node 24 → 26-alpine** and **python 3.12 → 3.14-slim** in the Python-React infra templates.
- **Post-merge incident (fixed same night):** V1-DotNet-NextJs is the only template carrying a package-lock.json (added by #294). The crossing squash-merges left that lock desynced from its package.json (lock said typescript ^5.8.3, manifest ^7.0.2), so `npm ci` inside Engine''s `V1TemplateCompileTests.RenderedTemplate_DockerTargetBuilds` failed — CI red on every push AFTER the wave while each PR''s own run was green. Fixed by regenerating the lock (fix/template-lockfile-desync). Lesson: dep PRs sharing one manifest+lock pair must land strictly sequentially with rebases between, or regenerate the lock after the wave.
- **Coverage note:** Engine compile-tests gate V1 (render → docker build). V0/V2 templates carry no lockfile and no compile test — bumps there are semver-trust. Follow-up: extend TemplateCompileTests (+ lockfiles) to V0/V2 so every template bump gets the same gate.
- App-level next-react group (#283) held during the wave. **Correction (same night):** src/StackAlchemist.Web was already on Next 16 (^16.2.12) — the "still on Next 15" claim in the first version of this entry was stale memory. #283 is only 16.2.12 → 16.3.1; its pre-rebase red came from the (since-fixed) template lock desync, and it merges on a clean rebase. The follow-on truth fix: product copy still said "Next.js 15" in 49 places in the app source (src/StackAlchemist.Web) plus another 47 rendered lines under content/ and docs/, while every NextJs template (V0 #268, V1 #298, V2 #276) and the app itself run Next 16 — corrected in fix/nextjs-16-truth-strings. The one deliberate survivor: the dated 2026-04-25 walkthrough post describes a run performed on Next 15 and keeps saying so.

**Why no review:** sweep policy — CI gates, deploy watch, revert cheap.
---

## 2026-09-22 — Dependabot sweep: vitest 4 → 5 (Web) merged; template framework majors deferred

**Status:** accepted (awareness-only stub per saved sweep policy)
- **vitest 4.1.11 → 5.0.1 + @vitest/coverage-v8 5.0.1** (src/StackAlchemist.Web, #390, which supersedes Dependabot #387/#388). The `testing` group split the major across two PRs, and neither half could install alone. vitest 5 also stopped bundling `vite`, which is now a peer, so `vite ^8` is an explicit devDependency. `vitest.config.ts` needed no changes. 308/308 tests pass and the coverage floors hold.
- The `next-react` and `testing` groups (root + Web) now take only minor and patch updates, so majors arrive as single PRs (#391).
- **Deferred to the #291 templates wave:** tailwindcss 3 → 4, tailwind-merge 2 → 3, eslint 9 → 10 and lucide-react 0.x → 1.x across V1/V2 templates (Dependabot #324-#335, closed). They are blocked with major-only ignores in the templates npm block (#391). Reason: templates still ship v3 `tailwind.config.ts` (tailwind 4 is a CSS-first migration), tailwind-merge 3 targets Tailwind 4, eslint 10 breaks eslint-plugin-react via eslint-config-next, and V2 has no compile gate (#313).
- **Addendum (same night):** merging #391 triggered a fresh Dependabot run. **eslint-config-next 15 → 16** in V2-DotNet-NextJs (#401) was merged: V2 already runs next ^16.3, and V1-DotNet-NextJs has run the same eslint-config-next 16 + eslint 9 pairing under its compile gate. V2 has no gate (#313), and its `"lint": "next lint"` script was already broken because Next 16 removed `next lint`; that fix belongs to the #291 wave. Also deferred to #291 with major-only ignores (#400): @eslint/js 10, eslint-plugin-react-hooks 7, globals 17, @vitejs/plugin-react 6 and vite (Dependabot #396-#399, closed). The stale npm entry for `/src/StackAlchemist.Templates`, which has no manifest, was removed (#393).

---

## 2026-09-30 — Compile gates + lockfiles for V2-DotNet-NextJs and the Python-React sets (#313)

**Status:** accepted
**Decision:** every template set that takes dependency bumps now has a gate that renders it through its production path and builds it; V0 keeps its deliberate no-lockfile stance (#315).
- **V2-DotNet-NextJs** (`V2DotNetNextJsCompileTests`): rendered through the real Swiss-Cheese path (`InjectionEngine` with a stand-in model that answers each zone with the template's own placeholder body, since V2 rejects an empty fill), then built by the real `DotNetBuildStrategy`. New `nextjs/package-lock.json`; any failed step, including a superseded `npm ci`, fails the gate.
- **V1-Python-React** (`V1PythonReactCompileTests`): `npm ci` against a new lockfile, then the real `PythonReactBuildStrategy` with its `python` redirected into a throwaway venv, then `npm run build` and the archive's own pytest suite.
- **V2-Python-React** (`V2PythonReactCompileTests`): frontend `npm ci` + eslint + `tsc --noEmit` + `npm run build`; backend `pip install -r requirements.txt` into a venv. The backend compile/test gate is skipped on #450: `InjectionEngine` drops the first line's indentation of every zone fill, so no Swiss-Cheese Python output can compile.
- **What the gates found on first run (all fixed here):** both Python-React frontends were uninstallable (typescript 7 vs typescript-eslint 8's `<6.1.0` peer → ERESOLVE; pinned to `^6.0.3`); they did not typecheck (no `src/vite-env.d.ts`); `main.py` ran `create_all` at import, so `pytest --collect-only` needed a live database (moved into a FastAPI lifespan); V2-DotNet's per-entity pages failed `next build` prerendering against an API that is not running (`force-dynamic`); and `BuildStrategyBase.RunProcessAsync` read stdout to EOF before stderr, which deadlocks once a child fills the stderr pipe (npm's ~10 KB of peer warnings did it on Windows).
- **Not fixed here (#451):** in production `PythonReactBuildStrategy` still fails at its first step. The engine image's apt Python is PEP 668-managed and refuses `pip install -r requirements.txt`. The strategy also hands generated code the engine's environment, including `DATABASE_URL`. The gates sidestep both with a per-build venv; production needs the same.
- **CI:** the backend job installs Python 3.14 (`actions/setup-python`) to match `python:3.14-slim` in the templates' Dockerfile.backend. Dependabot already listed every new lock directory; its #291 major-only ignores are unchanged.

**Addendum 2026-10-01 — psycopg 3 for the Python-React templates.** Dependabot #448 moved both Python-React backends to `sqlalchemy==2.1.1`. SQLAlchemy 2.1 maps a bare `postgresql://` URL to the psycopg 3 driver, while the templates shipped `psycopg2-binary`, so every generated FastAPI app failed at import (`ModuleNotFoundError: No module named 'psycopg'`), in uvicorn and in its own tests. The new `V1PythonReactCompileTests` gate caught it on this PR's merge ref. Both templates now ship `psycopg[binary]==3.3.6` and keep the plain `postgresql://` URLs (config.py, alembic.ini, docker-compose.yml, .env.example), which is the 2.1-native form. Same change: `BuildStrategyBase.RunProcessAsync` kills the child process tree when its token is cancelled, because pipe reads do not observe cancellation and a hung toolchain otherwise held the compile worker indefinitely.

---

## 2026-10-01 — Templates wave (#291): Tailwind 4, lucide 1, vite 8, ESLint 10 (Python-React), Tier3 CDK on TS 7

**Status:** accepted
**Decision:** the rest of the #291 majors land, each under the compile gate for its template set. Two stay blocked upstream and move to #456.
- **Tailwind 3 → 4, tailwind-merge 2 → 3** (V1/V2-DotNet-NextJs, V1/V2-Python-React). Stylesheets use `@import "tailwindcss"`. The Next templates run `@tailwindcss/postcss` and the Vite templates run `@tailwindcss/vite`. autoprefixer and the Vite `postcss.config.js` files are gone.
  - **`tailwind.config.ts` stays, loaded with `@config`.** The personalization palette is not a Handlebars token. `PromptBuilderService`'s Color Theme section tells the model to write it into `tailwind.config.ts`, and Tailwind 4 ignores that file unless a stylesheet names it. Keeping the file kept the prompt contract.
  - **The Color Theme prompt now says Tailwind CSS v4.** It tells the model to put the palette under `theme.extend.colors`, to leave the stylesheet's `@import`/`@config` lines alone, not to add v3 `@tailwind` directives, and not to use `safelist`/`corePlugins`. A stylesheet rewritten with `@tailwind` directives still builds, but ships CSS with no theme, no preflight and no palette, which is silent. `safelist`/`corePlugins` at least fail the typecheck.
  - The stale "ESLint runs during `next build`" line is gone from the prompt and from `V1-generation.md`. Next 16 does not lint on build.
  - **Compat base layer.** The official v4 upgrade-guide styles restore three v3 defaults: gray-200 borders, gray-400 placeholders, and a pointer cursor on enabled buttons. The ring-width change is not restored.
- **V2-DotNet-NextJs never ran Tailwind.** It had no PostCSS config, so every utility class in that template was a no-op. It has one now. Customers see V2 pages styled for the first time.
- **lucide-react 0.x → 1.x** in both Next templates. No template imports an icon.
- **vite 6 → 8, @vitejs/plugin-react 4 → 6** in both Python-React frontends. `vite.config.ts` uses `import.meta.dirname`.
- **ESLint 9 → 10** in the Python-React frontends, with @eslint/js 10, eslint-plugin-react-hooks 7, globals 17 and typescript-eslint ^8.71. The flat config moves to `defineConfig`.
  - The hooks rules are pinned to react-hooks 5's pair (`rules-of-hooks`, `exhaustive-deps`). v7's `recommended` adds the React Compiler rules, and `npm run lint -- --max-warnings=0` is a Compile Guarantee step over model-written code, so turning them on is a product call (#456).
  - ESLint 10's own new recommended rules (`no-unassigned-vars`, `no-useless-assignment`, `preserve-caught-error`) are taken.
- **ESLint 10 deferred for the Next templates (#456).** `eslint-config-next` 16.3.8 pulls eslint-plugin-react 7.37.5 (which calls the removed `context.getFilename()`), eslint-plugin-import 2.32 and jsx-a11y 6.10, and all three peer on eslint <=9.
- **`npm run lint` was broken in both Next templates. Fixed.**
  - V2 ran `next lint`, which Next 16 removed. It now uses `eslint .` with V1's flat config.
  - V1, and V2 after that fix, crashed on load with "typescript-eslint does not support TS 7.0", a leftover of the 2026-08-19 TS 7 merge. **Both Next templates are back on `typescript ^6.0.3`**, matching the Python-React frontends (#452).
  - The TS 7 release notes' side-by-side setup was tried and dropped in review: `@typescript/native` for `tsc`, with `typescript` aliased to `@typescript/typescript6`. `next build` already type-checks through the `typescript` package (TS 6 API), so TS 7 only added a second checker. Two packages competed for `.bin/tsc`. dependabot-core skips `npm:`-aliased requirements, which would have frozen both silently. Editors could not use the workspace TS.
  - Every template that lints with typescript-eslint moves to TS 7 once it supports the TS 7.1 API (#456).
- **Tier3 CDK on TypeScript 7.** ts-node cannot host TS 7, which has no compiler API (#267 was closed for this). `cdk.json` runs `npx tsc && node bin/app.js`. With `noEmitOnError`, a type error still fails `cdk synth`. Tier 3 overlays every project type, so the CDK app now ships its own `infra/cdk/.gitignore` for the emitted `.js`/`.d.ts`.
- **Gates added.**
  - `TailwindStylesheet.AssertCompiled` checks the built CSS of V1/V2-DotNet and both Python-React sets: no Tailwind directive survives, and the utilities the template's markup uses exist.
  - `PersonalizedPalette_ReachesTheBuiltStylesheet` (V1-DotNet and V1-Python-React) routes a v3-shaped palette config through reconstruction, builds, and finds the hex values in the CSS. It fails when `@config` is removed.
  - Both Next gates now run `npm run lint`.
- **Dependabot:** the templates npm block keeps major-only ignores for `typescript` and `eslint` only (#456).
- **Customer-visible:**
  - Tailwind 4 rendering, including v4's scale shifts for v3 names that model-written markup may use (`shadow-sm`, `rounded-sm`, `blur-sm`, and a bare `ring` is now 1px).
  - V2-DotNet pages are styled for the first time.
  - `npm run lint` runs in both Next templates, and those templates' `tsc` is TS 6.0.
  - The Tier-3 CDK app compiles with `tsc` instead of running through ts-node.

---

## 2026-10-03 — Default model → Claude Sonnet 5.5; BYOK list refreshed

**Status:** accepted
**Context:** `claude-sonnet-4-6` ($3/$15 per MTok) has two newer, cheaper successors in the Sonnet line. `claude-sonnet-5-5` costs $2/$10 per MTok. Under the standing rule ("at equal price use the latest; a cheaper Sonnet may be revisited"), the cheaper and newer model wins. The BYOK list still offered two retired Anthropic ids and `gpt-4o-mini`.
**Decision:**
- **Default:** engine, web and DB default → `claude-sonnet-5-5`. `vars.ANTHROPIC_MODEL` is flipped in the same change.
- **Request shape.**
  - No `thinking` field: adaptive thinking is the default, and `disabled` is a 400 on this model.
  - No sampling parameters: non-default values are a 400.
  - `output_config.effort` defaults to `medium`, Anthropic's starting point for code generation on 5.5. The levels were recalibrated, so it is configurable through `ANTHROPIC_EFFORT`. It is sent only to models that accept it; Haiku 4.5 rejects it.
  - `fallbacks: "default"` (beta `server-side-fallback-2026-07-01`) goes to the models that accept it, so a safety-classifier decline is retried server-side.
- **Response parsing.**
  - Content is read by block type.
  - Text before a `fallback` block is the declining model's partial output and is dropped.
  - `model_used` records the model that actually served the turn.
  - `stop_reason: "refusal"` throws `LlmRefusalException`, which records the category; the error category is `internal`.
- **Budget:**
  - `Anthropic:MaxTokens` goes 8,192 → 20,000. The Sonnet 5.x tokenizer uses about 30% more tokens than 4.6 for the same text, and thinking counts toward the limit.
  - The Anthropic HTTP timeout goes 5 → 10 minutes for these non-streaming calls.
- **Global-key allowlist:** the Engine allowlist is `claude-sonnet-5-5` and `claude-haiku-4-5`. Opus 5.5 is offered only with the user's own Anthropic key, as Opus was before.
- **BYOK options:**
  - Claude Sonnet 5.5 (default)
  - Claude Opus 5.5
  - Claude Haiku 4.5
  - OpenAI `gpt-6.1-sol`. OpenAI requests now send `max_completion_tokens`; GPT-5-era models reject `max_tokens`.
  - OpenRouter `anthropic/claude-sonnet-5.5`
- **Stored choices:** Drizzle migration `0002_model_defaults` remaps each retired stored id to the current model from the same provider, so a BYOK user keeps routing to the key they stored.
**Consequences:**
- Per-token cost falls about 33%, while the same text costs about 30% more tokens. Medium-effort thinking adds output tokens. Expected cost per generation is roughly flat against 4.6 and stays far under the $0.50 target. Re-baseline from `generations.input_tokens/output_tokens` after a week of traffic before touching effort.
- Sonnet 5.5 has its own rate-limit pool. Check the tier's limits before raising Swiss Cheese concurrency.
- Prompts were not retuned. The migration guide says Sonnet-era prompts carry over. An effort sweep against real generations is the open tuning item.

---

## 2026-10-03 — Live status is polling only; test mirror retired; docs truth-up

**Status:** accepted
**Polling (PR #465).** Generation rows have lived in qavren-db, which has no change feed, since the 2026-10-01 flip. The browser bundle still opened Supabase Realtime channels to the old project. A probe showed that an anonymous `postgres_changes` join there succeeds ("Subscribed to PostgreSQL"). The old hook stopped polling on `SUBSCRIBED`, so any status page whose build outlasted the WebSocket handshake froze until reloaded. The dashboard never live-refreshed. Now:
- `useGenerationStatus` polls the owner-scoped `getGeneration` action every 3 s while the tab is visible. It pauses when the tab is hidden and never stacks requests.
- `GenerationsLiveRefresher` calls `router.refresh()` every 10 s while any build is in progress.
- No WebSocket transport remains. Revisit (SSE) only if poll load ever shows up.

**Test mirror (PR #468, #211).** `deploy-test.yml` targeted a `[self-hosted, Linux, X64]` runner the fleet no longer has. It had not deployed since 2026-06-01. It was deleted rather than revived. There is now **no staging environment**: every push to `main` outside `paths-ignore` deploys prod, and CI's E2E lane (Postgres + Keycloak containers) is the pre-merge integration check. `docker/docker-compose.test.yml` remains only as the base file of that lane.

**Docs truth-up (this PR).**
- Architecture, advanced, product, user and runbook docs now describe the post-cutover system: qavren-db, Qavren Auth, polling, Claude Sonnet 5.5, no staging. Supabase is described as rollback-only until phase F.
- Dated records (plans, execution records, audits) were left as history, with status notes at most.
- `conductor/` was deleted: eight executed or obsolete April plans, with history in git.
- The local `docker-compose.yml` now reads the repo-root `.env` (the file `scripts/setup-env.mjs` writes). It used to read a `.env.development` nothing created, and its web healthcheck now uses `/api/healthz`.
- The Dockerfile's `NEXT_PUBLIC_APP_URL` default is the prod origin instead of the retired test site.

---

## 2026-10-03 — Money-path hardening; no paid checkout in the cutover proof

**Status:** accepted
**Context:** The owner will not run a real paid checkout to prove phase E §3.4. The webhook path was instead reviewed against the code and probed live. Probing showed:
- prod's `/api/webhooks/stripe` answers an unsigned request with the Engine's signature 401;
- prod has every function and trigger the path needs;
- `stripe_events` was empty, because no live event had arrived since the cutover.

The review found six defects, all fixed in this change:
- **Checkout failed for long prompts.** The Engine copied the prompt into Stripe metadata, which Stripe caps at 500 characters, while the web allows 2,000. The prompt is now clipped (`StripeMetadata.Clip`), and the webhook builds from the full prompt on the generations row.
- **#421: unpaid paid-tier rows were built for free.** The reconciler re-fired stale `pending` rows without a payment check. Paid tiers now need a completed transaction. Unpaid rows are left alone while their checkout can complete, then failed after 25 h.
- **Delayed payment methods were built before the money cleared.** A `checkout.session.completed` with `payment_status = unpaid` is now deferred. `checkout.session.async_payment_succeeded` is handled as the paid moment.
- **#419: a checkout for a missing generation caused a retry storm.** Drizzle `0003` records the payment (NULL `generation_id`) and the event instead of failing the FK. The Engine logs MANUAL RECOVERY and does not enqueue.
- **#423: disputes never matched a transaction.** They are now matched by payment intent; `stripe_charge_id` is never written.
- **#424: an ambiguous refund claim could strand a row.** The claim is now reverted (guarded on `refund_pending`) and a MANUAL RECOVERY line is logged.

**Not proven:** that Stripe delivers to the endpoint with the signing secret the Engine holds. Only a real delivery shows that. `docs/runbooks/stripe-webhooks.md` lists two ways to check it without paying:
- the dashboard's recent deliveries;
- an owner-approved zero-charge probe workflow, which creates and immediately expires a $1 session.

Phase F's gate G2(2) becomes "this hardening merged, plus one of those two checks green" instead of "a paid checkout and refund".

**Addendum 2026-10-04 — the live account had no webhook endpoint.** The approved wiring check (`.github/workflows/stripe-wiring-check.yml`) found **zero** webhook endpoints in the live Stripe account. Every live checkout so far would have charged the customer and built nothing. The Prod `STRIPE_WEBHOOK_SECRET` (2026-04-02) matched no live endpoint.
- The workflow created `we_1UMfsUCF5Q50oI5Iyz6LUolM` with the six handled events, `api_version` pinned to Stripe.net 53's.
- Stripe returns the signing secret only once. The workflow printed it only RSA-OAEP-encrypted to a one-time operator key, and it was decrypted locally into the Prod secret.
- Prod was redeployed. The zero-charge probe then proved Stripe → Engine → qavren-db end to end.
- **Lesson:** a paid path that has never processed a payment is unverified, whatever its tests say. Run the wiring check after any Stripe account, key or URL change.

---

## 2026-10-04 — Zone fills keep their indentation (#450)

**Status:** accepted
**Context:** `InjectionEngine.CleanZoneContent` called `Trim()` on every zone fill, and `TemplateProvider.InjectIntoZone` re-inserted the fill at column 0. Line 1 of every zone therefore lost its indentation. C# and TSX still compiled. Python did not: every V2-Python-React zone sits inside a `def` or `class`, so the Swiss-Cheese path could never produce valid Python, and the V2-Python-React backend compile gate was skipped.
**Decision:** one indentation rule for every zone fill, in `ZoneIndentation`:
- The fill keeps its **relative** indentation: its common leading whitespace is removed, so nested blocks stay nested.
- Its **absolute** indentation is the START marker line's whitespace, used verbatim. A tab-indented template gets tabs, a space-indented one spaces. The fill's own nesting is never re-tabbed, because there is no reliable tab width to convert with.
- Whitespace-only lines become empty (flake8 W293). The END marker keeps its indentation.
- `CleanZoneContent` strips only fence lines, stray `[[FILE:]]` markers and surrounding blank lines. Both old regexes used `\s*`, which also ate the next line's indentation.
- The rule lives in `InjectIntoZone`, so the V1 `Reconstruct` path gets it too. V1's Python zones are all at column 0, where only the dedent can change anything.
- The fill is now inserted with a match evaluator. It used to be spliced into a regex replacement string, where `$1`, `$_` or `$&` in generated code were substitution tokens.

**Consequences:** `V2PythonReactCompileTests.RenderedBackend_CompilesAndPassesItsOwnTests` is unskipped and passes (flake8 plus the archive's pytest). The template defects it then found are fixed: `pass` in the comment-only `BaseFields` placeholder, `# noqa: F401` on the registration imports in `models/__init__.py` and on the speculative column-type imports, and a placeholder comment over 100 columns (E501).

---

## 2026-10-04 — Compile Guarantee builds run sandboxed (#454)

**Status:** accepted
**Context:** Compile Guarantee builds execute LLM-generated code. A `.csproj` can declare MSBuild `Exec` targets, `npm install` runs package scripts, and a FastAPI app runs under pytest. The input is a customer prompt, so this was a prompt-injection-to-code-execution path. #453 already stopped build children from inheriting the Engine's secrets. They still ran as root inside the Engine's container:
- the Engine's `/proc/<pid>/environ`, which holds every secret, was one `cat` away;
- every other job's tree and archive was readable;
- the network was open, including the instance-metadata endpoint.

**Decision:** option 1 from the issue: a separate unprivileged user plus an egress firewall. The in-process worker stays.
- **Another uid.** Every build command runs through `docker/engine/sa-sandbox-exec`: `setpriv` to `sa-builder` (uid 10001), no supplementary groups, no-new-privs, empty capability sets, and `prlimit` against fork bombs.
- **A copy, never the source tree.** `BuildSandbox` copies the generated tree to `/var/lib/stackalchemist/build/<job>` for each attempt and runs the build there.
  - The Engine keeps writing repair files, the build report and the archive in its own tree, under `/var/lib/stackalchemist/tmp` (0700 root). A symlink planted by a build can never redirect a root write or read.
  - Only `package-lock.json` is copied back, because `npm install` legitimately rewrites it.
  - The archiver also skips symlinks as a backstop.
- **A private HOME per job.** HOME is kept across one job's attempts for warm caches and deleted when the next job starts. One job's build cannot poison another's NuGet or npm cache.
- **Nothing outlives a build.** After every build, every uid-10001 process is killed (`kill -KILL -1` as that uid). A daemonized leftover cannot watch later jobs.
- **Egress firewall for that uid only.** The entrypoint installs it, using cap NET_ADMIN.
  - It rejects loopback, RFC 1918, CGNAT, link-local (metadata), multicast and reserved ranges. The resolvers in `/etc/resolv.conf` stay open on port 53.
  - NET_ADMIN is then dropped from the Engine's bounding set.
  - `SA_BUILD_EGRESS=required` (prod and CI compose) refuses to start the container without the firewall.
- **Production refuses to start without the sandbox.**

**Consequences:**
- Proven in the container by `docker/engine/sandbox-selftest.sh`, which runs in the E2E Integration lane:
  - the uid is 10001, with no capabilities and no-new-privs;
  - the Engine's environment and other jobs' trees are unreadable;
  - metadata, loopback and the compose Postgres are unreachable, while the npm registry stays reachable;
  - dotnet, npm and pip work as the build user;
  - the purge leaves no process behind.
- Builds now start with cold package caches for every job, because sharing a cache across jobs is exactly the poisoning risk. That adds roughly a cold `npm ci` per job. Revisit with a root-owned, read-only pre-warmed cache (NuGet fallback folders, an npm offline mirror) if build time matters.
- Accepted risks:
  - Public egress stays open, because builds need the registries, and DNS to the configured resolver is open. A build can still send its own job's code out, but that is the customer's own data.
  - There is no memory cap per build: the container's limit applies.

---

## 2026-10-04 — Phase F: Supabase retired; one store, one identity provider

**Status:** accepted and executed 2026-10-04 (#483, #482, #480). The owner brought the merge forward from the 2026-10-08 gate.
**Context:** Prod has run on qavren-db and Qavren Auth since the phase E flip (2026-10-01). Supabase mode stayed compiled in only as a rollback path. G2 sets the conditions for removing it: seven clean days after the flip, and the money path verified. The money path was met on 2026-10-04 (#471, the live webhook endpoint, and the zero-charge probe). Keeping two modes doubled every auth, data and deploy path, and the browser bundle still carried the old project's URL and anon key.

**Decision:**
- **One shape in production.** sa-web refuses to boot without `DATABASE_URL`, `QAVREN_AUTH_URL` and `AUTH_SECRET`. The deploy preflight requires those plus `DATABASE_URL_MIGRATE` and shape-checks the three URLs before the build, while the old stack still serves. The end-of-run mode check knows only Qavren Auth.
- **Demo mode is explicit or local.** Outside production the web is in demo mode unless `NEXT_PUBLIC_DEMO_MODE=false`. It no longer infers demo mode from a missing Supabase URL. The Engine outside production runs on the no-op store when `DATABASE_URL` is unset.
- **Polling replaces Realtime.** Status pages poll every 3 s while visible and pause while hidden. The dashboard refreshes every 10 s while a build is active. This shipped early as Task 1 (#465), because the Realtime channel had nothing writing to it after the flip.
- **Isolation is enforced in code, not RLS.** Every query is scoped to the session user, and the two-user integration suite proves it.
- **Deleted:** the Supabase Auth pages and branches, `@supabase/*`, the Supabase stores in web and Engine, `supabase/` (migrations), the Supabase CSP entries, the Supabase deploy step and its secrets wiring, and the two Supabase runbooks.

**Consequences:**
- There is no rollback to Supabase. Recovery is a forward fix, or a revert of the F PRs plus re-adding the deleted secrets, against a project the owner deletes after 2026-10-08.
- Existing Supabase users did not carry over (parent plan decision 3).
- Owner steps (Task 7): delete the Prod `SUPABASE_*` secrets, delete projects `ctqhwykryoglhdwatljt` and `cdlefpvsvyepofsboepc`, then rescan the prod bundle for `supabase.co`.
- The CSP can be enforced once a monitoring window passes without the Supabase entries (`docs/architecture/CSP Rollout Plan.md`).
