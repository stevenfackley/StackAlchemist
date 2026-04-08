# Architecture Overview

This document describes the high-level architecture of the StackAlchemist platform. It's intended for contributors, technical evaluators, and users who want to understand what's happening under the hood.

> **Source Available:** StackAlchemist is source-available. You can browse the full codebase on GitHub to verify these claims. This document reflects the current V1 architecture.

---

## System Components

```
┌──────────────────────────────────────────────────────────────────────────┐
│                       StackAlchemist Platform                            │
│                                                                          │
│  ┌────────────────────────────────────────────┐                         │
│  │      Next.js 15 Frontend (App Router)      │ ← User interface        │
│  │   TypeScript + Tailwind + shadcn/ui        │   Simple/Advanced Mode  │
│  │   Server Actions + Supabase SSR            │   Progress tracking     │
│  │   src/StackAlchemist.Web                   │   Download              │
│  └────────────────┬─────────────────────────┘                          │
│                   │ HTTP / WebSocket                                     │
│  ┌────────────────▼─────────────────────────┐                          │
│  │    .NET 10 Web API (Engine)               │ ← Orchestrator          │
│  │    Request handling + in-process          │   Auth + validation     │
│  │    BackgroundService compile worker       │   Real-time status      │
│  │    src/StackAlchemist.Engine              │   Stripe webhooks       │
│  └──┬──────────────────────────┬────────────┘                          │
│     │                          │                                         │
│     │                          └────┬─────────────────┐                 │
│     │                              │                 │                  │
│  ┌──▼──────────────┐   ┌──────────▼──────┐  ┌───────▼──────┐          │
│  │   Supabase      │   │ Cloudflare R2   │  │    Stripe    │          │
│  │ (Auth +         │   │ (ZIP Archives   │  │  (Payments)  │          │
│  │  PostgreSQL +   │   │  S3-compatible) │  │              │          │
│  │  Realtime)      │   │                 │  │              │          │
│  └────────────────┘   └─────────────────┘  └──────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │         Anthropic Claude 3.5 Sonnet API                 │          │
│  │    (Schema extraction + Code generation)                │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │    Engine Library + Template Library                    │          │
│  │    Handlebars templates (V1-DotNet-NextJs,             │          │
│  │    V1-Python-React) + Build strategies                 │          │
│  │    src/StackAlchemist.Engine + src/StackAlchemist      │          │
│  │    .Templates                                          │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │    Worker Service (Standalone, preserved for scale-out) │          │
│  │    Not currently used in production deployment         │          │
│  │    src/StackAlchemist.Worker                           │          │
│  └──────────────────────────────────────────────────────────┘          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

The solution is organized into focused, single-responsibility projects under `src/`:

| Project | Type | Purpose |
|---------|------|---------|
| `StackAlchemist.Web` | Next.js 15 (App Router) | Frontend UI, authentication pages, server actions, Supabase SSR integration |
| `StackAlchemist.Engine` | .NET 10 Web API | API host, generation orchestrator, in-process compile worker (BackgroundService) |
| `StackAlchemist.Worker` | .NET 10 Worker Service | Standalone worker host for future scale-out; preserved but not used in current deployment |
| `StackAlchemist.Templates` | Handlebars template library | V1-DotNet-NextJs and V1-Python-React template sets; injected into generation pipeline |
| `StackAlchemist.Engine.Tests` | xUnit | Unit and integration tests for Engine, state machine, and orchestration logic |
| `StackAlchemist.Worker.Tests` | xUnit | Unit and integration tests for Worker service |

---

## Request Flow: Simple Mode Generation

The typical flow for a free-tier (Spark) user generating a project:

1. User enters a prompt and clicks "Synthesize"
2. Next.js frontend calls `POST /api/extract-schema` with the prompt
3. Engine calls Claude 3.5 Sonnet to extract entity/schema information; returns JSON schema
4. Schema renders on React Flow canvas for user confirmation
5. User optionally walks through the personalization wizard (business description, project name, color scheme, feature flags)
6. For **free tier (Spark)**: Next.js directly calls `POST /api/generate` with the schema and personalization payload
7. For **paid tiers**: Next.js calls `POST /api/stripe/create-session`, redirects to Stripe Checkout
   - On successful payment, Stripe webhook (`checkout.session.completed`) fires
   - Engine reloads generation payload from Supabase
   - Generation is enqueued for processing

Once generation is enqueued (free or paid):

1. **GenerationOrchestrator** loads the appropriate template set (V1-DotNet-NextJs or V1-Python-React) based on `ProjectType`
2. **Handlebars rendering** with personalization context (business description, project name, color scheme, feature flags)
3. **LLM injection**: For each method placeholder in rendered files, call Claude 3.5 to generate business logic
4. **Compile check**: Push rendered files to in-process Channel to CompileWorkerService
5. **Build execution** via `IBuildStrategy` interface:
   - `DotNetBuildStrategy`: runs `dotnet build`
   - `PythonReactBuildStrategy`: runs `pip install`, `flake8`, `pytest`, `npm install`, `eslint`, `tsc`
6. **On failure**: Extract build errors, call LLM for repair suggestions, retry (max 3 attempts)
7. **On success**: Zip artifact, upload to Cloudflare R2, update Supabase `generations` row with status
8. Frontend Realtime subscription fires, user can download the generated archive

---

## Multi-Ecosystem Support

StackAlchemist supports multiple technology stacks via template selection and build strategy pluggability:

**Template Sets:**
- **V1-DotNet-NextJs**: Generated projects use .NET 10 backend (Dapper data layer, Controllers) and Next.js 15 frontend
- **V1-Python-React**: Generated projects use Python backend (FastAPI, SQLAlchemy) and React frontend

**Platform Selection:**
In Advanced Mode, Step 2 is "Platform Selection," which sets the `ProjectType` enum and selects the corresponding:
1. Template root directory
2. Build strategy (`IBuildStrategy` implementation)
3. Personalization options (language-specific feature flags)

**Build Strategies:**
The `IBuildStrategy` interface allows plugging in language-specific build logic:
- **DotNetBuildStrategy**: `dotnet build`, targeting .NET 10
- **PythonReactBuildStrategy**: `pip install`, `flake8` lint check, `pytest` unit tests, `npm install`, `eslint`, `tsc` type check

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

Every state transition is persisted to the `generations` table in Supabase. If the Engine crashes mid-generation, the job can be resumed from the last successful state on next service restart.

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

## Real-Time Progress Reporting

The frontend maintains a Supabase Realtime subscription on the `generations` table. As the Engine processes a generation:

1. **CompileWorkerService** (BackgroundService in Engine) patches `status` and `build_log` columns
2. **SupabaseDeliveryService** persists updates to the database
3. **Supabase Realtime** (WebSocket) pushes changes to connected frontend clients
4. **Frontend subscription** receives events and renders:
   - Status steps (Schema Extraction → Building → Packing → Uploading → Success)
   - Terminal-style build log output
   - Real-time progress percentage

Example event sequence:
```
{ status: "Generating", progress: 10, buildLog: "" }
{ status: "Building", progress: 30, buildLog: "Building project...\n" }
{ status: "Building", progress: 40, buildLog: "...dotnet build output...\n" }
{ status: "Packing", progress: 85, buildLog: "...complete\n" }
{ status: "Uploading", progress: 95, buildLog: "...uploading...\n" }
{ status: "Success", progress: 100, buildLog: "...complete\n", downloadUrl: "https://..." }
```

---

## Storage: Cloudflare R2

Generated ZIP archives are stored in Cloudflare R2, an S3-compatible object storage service. The integration uses the AWS SDK for .NET (`AWSSDK.S3`) with:

- **S3 endpoint**: Cloudflare R2 bucket
- **Force Path Style**: Enabled (to work with R2's path-style URLs)
- **AuthenticationRegion**: `"auto"` (R2-specific setting)
- **Signed URL expiry**:
  - **Production**: 168 hours (7 days)
  - **Development**: 24 hours

The Engine generates a presigned URL upon successful upload, which is stored in the `generations.download_url` column and returned to the frontend. Users can download the archive directly from R2 without backend proxying.

---

## Security & Rate Limiting

All API endpoints implement security controls:

**Rate Limiting:**
- `/api/generate`: 5 requests per minute (per user)
- `/api/extract-schema`: 15 requests per minute (per user)
- `/api/stripe/create-session`: 3 requests per minute (per user)

**Authentication:**
- Most endpoints (`/api/*`) require `X-Engine-Key` header with a valid shared secret
- Webhook endpoints (`/api/webhooks`) are exempt (Stripe signature verification used instead)
- Frontend uses Supabase JWT for session authentication

**CORS:**
- Locked to configured frontend origin(s) in Engine configuration
- Prevents cross-origin abuse

**Payload Validation:**
- Input prompts and personalization fields are sanitized before LLM calls
- Stripe webhook signatures are verified before processing payment events

---

## Key Design Decisions

### Why .NET for the backend?
The generated output is .NET. The generation engine needed to closely match the conventions and tooling of the output — using .NET to generate .NET means the templating, validation, and compilation all happen in the same ecosystem. This eliminates friction when debugging template rendering, running test builds, or adjusting language conventions. See `docs/architecture/DECISIONS.md` for the full architectural decision record.

### Why Supabase?
Supabase provides authentication, row-level security (RLS), PostgreSQL, and Realtime subscriptions without requiring a separate identity provider. The generated code also targets Supabase — using the same stack for both the platform and the generated output lets us dogfood our own architecture and keep the stack coherent.

### Why Cloudflare R2 for storage?
R2 is S3-compatible but more cost-effective than Amazon S3 and has no egress charges, which is critical for a platform distributing generated software to users globally. The AWS SDK integration makes R2 drop-in compatible with the existing storage abstraction.

### Why Dapper over Entity Framework?
EF Core adds significant complexity to the generated code and makes templates harder to reason about. Dapper keeps the data layer explicit — generated SQL is readable, portable, and easy for developers to modify post-generation. This aligns with the Swiss Cheese Method philosophy: leave explicit, reviewable code. See [The Swiss Cheese Method](./swiss-cheese-method) for how Dapper fits into the generation model.

### Why in-process compilation instead of a separate Worker queue?
The current deployment runs the compile worker as a BackgroundService inside the Engine process. This simplifies operational overhead (one service to deploy) and provides fast inter-process communication via .NET Channels. The Worker service project is preserved in the codebase for future scale-out if needed, but for the current request load, in-process processing is more efficient than queue-based decoupling.

---

## Testing

| Layer | Framework | Coverage Target |
|-------|-----------|-----------------|
| Engine logic | xUnit | Core orchestration, state machine, build strategy logic, error recovery |
| Worker service | xUnit + TestContainers | End-to-end generation flows, schema extraction, template rendering, build output parsing |
| Frontend components | Vitest + React Testing Library | UI components, form validation, Realtime subscription handling |
| Full flows | Playwright | User onboarding, generation submission, progress tracking, download |

See `docs/architecture/Testing Strategy.md` for the full testing strategy.

---

## Related Docs

- [The Swiss Cheese Method →](./swiss-cheese-method) — Why we render static templates + inject business logic
- [The Compile Guarantee →](./compile-guarantee) — How we ensure generated code compiles every time
- [Self-Hosting →](./self-hosting) — Running StackAlchemist on your own infrastructure
- [Generation State Machine →](../architecture/Generation%20State%20Machine.md) — Detailed state diagram and persistence model
