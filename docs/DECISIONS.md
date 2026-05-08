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
