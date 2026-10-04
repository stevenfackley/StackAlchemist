# Architecture Overview

This document describes the high-level architecture of the StackAlchemist platform. It's intended for contributors, technical evaluators, and users who want to understand what's happening under the hood.

> **Source Available:** StackAlchemist is source-available. You can browse the full codebase on GitHub to verify these claims. This document reflects the architecture as of 2026-10-03.

---

## System Components

```
┌──────────────────────────────────────────────────────────────────────────┐
│                       StackAlchemist Platform                            │
│                                                                          │
│  ┌────────────────────────────────────────────┐                         │
│  │      Next.js 16 Frontend (App Router)      │ ← User interface        │
│  │   TypeScript + Tailwind CSS 4 + React 19   │   Simple/Advanced Mode  │
│  │   Server Actions + Auth.js (Qavren Auth)   │   Status polling        │
│  │   src/StackAlchemist.Web                   │   Download              │
│  └────────────────┬───────────────────────────┘                         │
│                   │ HTTP (X-Engine-Key)                                  │
│  ┌────────────────▼─────────────────────────┐                          │
│  │    .NET 10 Web API (Engine)               │ ← Orchestrator          │
│  │    Request handling + in-process          │   Service auth          │
│  │    BackgroundService compile worker       │   Validation            │
│  │    src/StackAlchemist.Engine              │   Stripe webhooks       │
│  └──┬──────────────────────────┬────────────┘                          │
│     │                          │                                         │
│     │                          └────┬─────────────────┐                 │
│     │                               │                 │                 │
│  ┌──▼──────────────┐   ┌───────────▼─────┐  ┌────────▼─────┐          │
│  │   qavren-db     │   │ Cloudflare R2   │  │    Stripe    │          │
│  │ (PostgreSQL,    │   │ (ZIP Archives   │  │  (Payments)  │          │
│  │  schema         │   │  S3-compatible) │  │              │          │
│  │  stackalchemist)│   │                 │  │              │          │
│  └─────────────────┘   └─────────────────┘  └──────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │  Keycloak realm (Qavren Auth): sign-in, registration,   │          │
│  │  email verification, password reset. Used by the web    │          │
│  │  app through Auth.js; the Engine does not see users     │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │         Anthropic Claude Sonnet 5.5 API                 │          │
│  │    (Schema extraction + Code generation)                │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │    Engine Library + Template Library                    │          │
│  │    Handlebars templates (V0-Spark, V1, V2, Tier3) +     │          │
│  │    Build strategies                                     │          │
│  │    src/StackAlchemist.Engine + src/StackAlchemist       │          │
│  │    .Templates                                           │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │    Worker Service (Standalone, preserved for scale-out) │          │
│  │    Not deployed                                         │          │
│  │    src/StackAlchemist.Worker                           │          │
│  └──────────────────────────────────────────────────────────┘          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

The solution is organized into focused, single-responsibility projects under `src/`:

| Project | Type | Purpose |
|---------|------|---------|
| `StackAlchemist.Web` | Next.js 16 (App Router) | Frontend UI, sign-in hand-off pages, server actions, Auth.js session, Drizzle data access |
| `StackAlchemist.Engine` | .NET 10 Web API | API host, generation orchestrator, in-process compile worker (BackgroundService) |
| `StackAlchemist.Worker` | .NET 10 Worker Service | Standalone worker host for scale-out; preserved but not deployed |
| `StackAlchemist.Templates` | Handlebars template library | `V0-Spark-NextJs`, `V1-DotNet-NextJs`, `V1-Python-React`, `V2-DotNet-NextJs`, `V2-Python-React` and `Tier3-Infrastructure` template sets |
| `StackAlchemist.Engine.Tests` | xUnit | Unit and integration tests for Engine, state machine, and orchestration logic |
| `StackAlchemist.Worker.Tests` | xUnit | Unit and integration tests for Worker service |

---

## Request Flow: Simple Mode Generation

The typical flow for a free-tier (Spark) user generating a project:

1. User enters a prompt and clicks "Synthesize"
2. Next.js frontend calls `POST /api/extract-schema` with the prompt
3. Engine calls Claude (Sonnet 5.5 default, configurable via `ANTHROPIC_MODEL`) to extract entity/schema information; returns JSON schema
4. Schema renders on React Flow canvas for user confirmation
5. User optionally walks through the personalization wizard (business description, project name, color scheme, feature flags)
6. For **free tier (Spark)**: Next.js directly calls `POST /api/generate` with the schema and personalization payload
7. For **paid tiers**: Next.js calls `POST /api/stripe/create-session`, redirects to Stripe Checkout
   - On successful payment, Stripe webhook (`checkout.session.completed`) fires
   - Engine reloads the generation payload from Postgres, marks the checkout paid through the atomic `process_checkout_completed` function (idempotent on `stripe_events`)
   - Generation is enqueued for processing

Once generation is enqueued (free or paid):

1. **GenerationOrchestrator** loads the appropriate template set based on `ProjectType`
2. **Handlebars rendering** with sanitized personalization context (business description, project name, color scheme, feature flags)
3. **LLM generation**: Claude (Sonnet 5.5 default) produces the business logic and token usage is persisted. Prod runs the V1 path: one whole-codebase call whose `[[FILE:…]]` blocks are reconstructed into the templates. The V2 "Swiss Cheese" path, which fills each injection zone with its own parallel call, is switched on by `Generation:UseSwissCheese` and is enabled in Development only
4. **Compile check**: Push rendered files to in-process Channel to CompileWorkerService
5. **Build execution** via `IBuildStrategy` interface:
   - `DotNetBuildStrategy`: `dotnet restore`, `dotnet build --no-restore`, then `npm ci` (falling back to `npm install`), `npm run typecheck` when the project defines it, and `npm run build`
   - `PythonReactBuildStrategy`: a per-build venv, `pip install -r requirements.txt`, `flake8`, `pytest --collect-only`, then `npm install`, `npm run lint`, `tsc --noEmit`
6. **On failure**: Extract build errors, call LLM for repair suggestions, retry (max 3 attempts)
7. **On success**: Write `build-report.json`, zip the artifact, upload to Cloudflare R2, update the `generations` row in Postgres
8. The status page picks up the change on its next poll and the user can download the generated archive
9. **On final failure of a paid tier**: the Engine refunds the Stripe charge in full and sends an email

---

## Multi-Ecosystem Support

StackAlchemist supports multiple technology stacks via template selection and build strategy pluggability:

**Template Sets:**
- **V1-DotNet-NextJs** / **V2-DotNet-NextJs**: Generated projects use a .NET 10 backend (Dapper data layer) and a Next.js 16 frontend
- **V1-Python-React** / **V2-Python-React**: Generated projects use a Python backend (FastAPI, SQLAlchemy) and a React frontend
- **V0-Spark-NextJs**: The free Spark tier's runnable Node demo, previewed in StackBlitz
- **Tier3-Infrastructure**: AWS CDK (TypeScript), Helm chart and Terraform added for the Infrastructure tier

**Platform Selection:**
In Advanced Mode, Step 2 is "Platform Selection," which sets the `ProjectType` enum and selects the corresponding:
1. Template root directory
2. Build strategy (`IBuildStrategy` implementation)
3. Personalization options (language-specific feature flags)

**Build Strategies:**
The `IBuildStrategy` interface allows plugging in language-specific build logic:
- **DotNetBuildStrategy**: `dotnet restore` + `dotnet build` for .NET 10, then the Next.js build (`npm ci`, typecheck, `npm run build`)
- **PythonReactBuildStrategy**: per-build venv + `pip install`, `flake8`, `pytest --collect-only`, then `npm install`, `npm run lint`, `tsc --noEmit`

Each step is recorded (command, exit code, duration, error and warning counts) and ends up in the archive's `build-report.json`. See [The Compile Guarantee](./compile-guarantee).

---

## State Machine

Generation lifecycle is managed by a formal state machine defined in `GenerationStateMachine.cs`.

**States:**
- `Pending` – Job created, waiting for pickup
- `Generating` – Schema extracted, templates loaded, LLM injection in progress
- `Building` – Build phase active
- `Packing` – Compilation successful, creating ZIP archive
- `Uploading` – Archive being written to Cloudflare R2
- `Success` – Generation complete, download link available
- `Failed` – Unrecoverable failure (retries exhausted)

**Events:**
- `EnginePickedUp` – Transition from Pending to Generating
- `CodeReconstructed` – LLM injection and template rendering complete; transition to Building
- `BuildPassed` – Compile check succeeded; transition to Packing
- `BuildFailed` – Compile check failed (retry if `retryCount < 3`; fail permanently if retries exhausted)
- `ZipCreated` – Archive assembled; transition to Uploading
- `UploadedToR2` – Archive uploaded to Cloudflare R2; transition to Success

**State Transitions:**
```
Pending
  → Generating (EnginePickedUp)
    → Building (CodeReconstructed)
       → Packing (BuildPassed)
          → Uploading (ZipCreated)
             → Success (UploadedToR2)
       → Generating (BuildFailed, if retryCount < 3)
       → Failed (BuildFailed, if retryCount >= 3)
```

State and build log are persisted to the `generations` table in Postgres (`append_build_log` appends log text atomically).

See [Generation State Machine](../architecture/Generation%20State%20Machine.md) for the detailed state diagram.

---

## Personalization System

Generated projects are personalized via the `GenerationPersonalization` model, which includes:

- **ProjectName**: User-provided name for the generated project
- **BusinessDescription**: Context about the business domain
- **Tagline**: Short descriptive tagline
- **ColorScheme**: One of six preset palettes, or custom hex color definition
- **DomainContext**: Per-entity custom instructions (e.g., "User entity should support soft deletes")
- **FeatureFlags**: Language-specific toggles (authentication method, soft-delete support, audit timestamps, Swagger docs, Docker Compose generation)

Personalization is stored as JSON in the `generations.personalization_json` column and injected into both:
1. Handlebars template rendering context (affecting generated file structure and imports)
2. LLM prompt context (affecting generated business logic implementation)

---

## Live Progress: Polling

There are no WebSockets and no Realtime subscription. The Engine writes progress to Postgres and the browser asks for it.

1. **CompileWorkerService** (BackgroundService in the Engine) updates `status` and appends to `build_log` on the `generations` row
2. **PostgresDeliveryService** (Npgsql) persists each update
3. The generation status page calls the `getGeneration` server action every 3 seconds while the tab is visible and pauses while it is hidden
4. `getGeneration` is owner-scoped: it only returns a row whose `user_id` matches the signed-in user
5. The dashboard refreshes every 10 seconds while a build is in progress

The page renders the status steps (Schema Extraction → Building → Packing → Uploading → Success), the terminal-style build log and, when finished, the download link. Polling replaced a Realtime channel that nothing wrote to, which left the page waiting forever.

---

## Storage: Cloudflare R2

Generated ZIP archives are stored in Cloudflare R2, an S3-compatible object storage service. The integration uses the AWS SDK for .NET (`AWSSDK.S3`) with:

- **S3 endpoint**: Cloudflare R2 bucket
- **Force Path Style**: Enabled (to work with R2's path-style URLs)
- **AuthenticationRegion**: `"auto"` (R2-specific setting)
- **Signed URL expiry**: 168 hours (7 days) by default (`CloudflareR2:PresignedUrlExpiryHours`)

The Engine generates a presigned URL upon successful upload, which is stored in the `generations.download_url` column and returned to the frontend. Users can download the archive directly from R2 without backend proxying.

Known issue (#444): generated ZIPs are also reachable through a public R2 custom domain, which sidesteps the presigned URL. Treat the presigned link as the intended path, not the only one.

---

## Security & Rate Limiting

All API endpoints implement security controls:

**Rate Limiting:**
- `/api/generate`: 5 requests per minute (per IP)
- `/api/extract-schema`: 15 requests per minute (per IP)
- `/api/stripe/create-session`: 3 requests per minute (per IP)

**Authentication:**
- Most endpoints (`/api/*`) require `X-Engine-Key` header with a valid shared secret
- Webhook endpoints (`/api/webhooks`) are exempt (Stripe signature verification used instead)
- Users sign in through Qavren Auth (Keycloak via Auth.js v5; email/password and Google). The session is a JWT cookie with a fixed 7-day lifetime. The Engine never authenticates users: the web app resolves the session and calls the Engine with the service key
- Every database query is scoped to the session user in code (`user_id = <session user>`). There is no row-level security; a two-user integration suite against real Postgres checks the isolation
- User BYOK API keys are encrypted with AES-256-GCM (`BYOK_ENCRYPTION_KEY`) in the web app and decrypted only in the Engine

**CORS:**
- Locked to configured frontend origin(s) in Engine configuration
- Prevents cross-origin abuse

**Payload Validation:**
- Input prompts and personalization fields are sanitized before LLM calls (in the web app and again in the Engine)
- Stripe webhook signatures are verified before processing payment events, and `stripe_events` makes processing idempotent

**Headers and tracing:**
- A Content-Security-Policy is sent in Report-Only mode; enforcement is pending
- Requests carry an `X-Correlation-Id`; the Engine emits OpenTelemetry generation meters
- Health probes: `/api/healthz` (web) and `/healthz` (Engine)

---

## Key Design Decisions

### Why .NET for the backend?
The generated output is .NET. The generation engine needed to closely match the conventions and tooling of the output — using .NET to generate .NET means the templating, validation, and compilation all happen in the same ecosystem. This eliminates friction when debugging template rendering, running test builds, or adjusting language conventions. See `docs/DECISIONS.md` for the full architectural decision record.

### Why qavren-db and Keycloak?
The platform started on Supabase (auth, RLS, Realtime). It moved to a shared Postgres (qavren-db, schema `stackalchemist`, its own owning role) and a Keycloak realm (Qavren Auth) on 2026-10-01, so one identity provider and one database operation serve several products. Data access is plain Postgres with Drizzle migrations, and authorization is explicit in code instead of RLS policies. Supabase is no longer the platform; a legacy "Supabase mode" remains compiled in only as a rollback path and is being deleted. Generated projects are a separate matter: their output still ships with a preinstalled Supabase client because that is what many customers want.

### Why Cloudflare R2 for storage?
R2 is S3-compatible but more cost-effective than Amazon S3 and has no egress charges, which is critical for a platform distributing generated software to users globally. The AWS SDK integration makes R2 drop-in compatible with the existing storage abstraction.

### Why Dapper over Entity Framework?
EF Core adds significant complexity to the generated code and makes templates harder to reason about. Dapper keeps the data layer explicit — generated SQL is readable, portable, and easy for developers to modify post-generation. This aligns with the Swiss Cheese Method philosophy: leave explicit, reviewable code. See [The Swiss Cheese Method](./swiss-cheese-method) for how Dapper fits into the generation model.

### Why in-process compilation instead of a separate Worker queue?
The compile worker (`CompileWorkerService`) runs as a BackgroundService inside the Engine process, fed by a .NET `Channel`. That is one service to deploy and no queue to operate. The Worker project is preserved for scale-out but is not deployed.

---

## Testing

| Layer | Framework | What it covers |
|-------|-----------|----------------|
| Engine logic | xUnit | Orchestration, state machine, build strategies, error recovery, build report |
| Template compile gates | xUnit (Engine.Tests integration) | Every template set with dependencies is built for real in CI |
| Worker service | xUnit + TestContainers | Standalone worker flows |
| Frontend | Vitest + React Testing Library | Components, server actions, polling; coverage floors enforced in CI |
| Data isolation | Vitest against real Postgres | Two-user suite proving every query is owner-scoped |
| Smoke E2E | Playwright (demo mode) | Core pages and flows |
| Integration E2E | Playwright against Postgres 17 + Keycloak containers | Real sign-in and data path, on main and nightly |

CI also runs a dependency audit gate and a nightly qavren-db pooler smoke test. There is no staging environment: changes that pass CI and merge to `main` deploy to production.

See `docs/architecture/Testing Strategy.md` for the full testing strategy.

---

## Related Docs

- [The Swiss Cheese Method →](./swiss-cheese-method) — Why we render static templates + inject business logic
- [The Compile Guarantee →](./compile-guarantee) — How we ensure generated code compiles every time
- [Self-Hosting →](./self-hosting) — Running StackAlchemist on your own infrastructure
- [Generation State Machine →](../architecture/Generation%20State%20Machine.md) — Detailed state diagram and persistence model
