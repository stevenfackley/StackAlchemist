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
- `/advanced?step=<1|2|3>` — Advanced Mode: 3-step stepper wizard

### Pricing placement
- Full pricing grid removed from landing page per owner feedback.
- Pricing details live exclusively in `/advanced?step=3`.
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
- Validation script: `src/StackAlchemist.Templates/validate.mjs` — run with `node validate.mjs`

### Handlebars Variables
| Variable | Usage |
|---|---|
| `{{ProjectName}}` | PascalCase project name (e.g. GymManager) |
| `{{ProjectNameKebab}}` | kebab-case (e.g. gym-manager) for npm name |
| `{{ProjectNameLower}}` | lowercase (e.g. gymmanager) for DB name, Docker |
| `{{DbConnectionString}}` | Full Npgsql connection string |
| `{{FrontendUrl}}` | CORS allowed origin |

### LLM Injection Zones
All zones use `{{!-- LLM_INJECTION_START: ZoneName --}}` / `{{!-- LLM_INJECTION_END: ZoneName --}}` comments.

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
