# Architecture Overview

This document describes the high-level architecture of the StackAlchemist platform. It's intended for contributors, technical evaluators, and users who want to understand what's happening under the hood.

> **Source Available:** StackAlchemist is source-available. You can browse the full codebase on GitHub to verify these claims. This document reflects the current V1 architecture.

---

## System Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                        StackAlchemist Platform                       │
│                                                                      │
│  ┌──────────────────────────────────────┐                           │
│  │         Next.js 15 Frontend          │  ← User interface          │
│  │    (App Router + TypeScript)         │     Simple/Advanced Mode   │
│  │    src/StackAlchemist.Web            │     Progress tracking      │
│  └─────────────────┬────────────────────┘     Download              │
│                    │ HTTP / WebSocket                                 │
│  ┌─────────────────▼────────────────────┐                           │
│  │      .NET 10 Web API / Gateway       │  ← Auth, routing           │
│  │    src/StackAlchemist.Placeholder    │     Request validation      │
│  └──────┬───────────────────┬───────────┘     Job dispatch           │
│         │                   │                                         │
│  ┌──────▼──────┐   ┌───────▼──────────┐                             │
│  │  Supabase   │   │  Generation Worker│  ← Async generation         │
│  │  (Auth +    │   │  StackAlchemist   │     Template rendering       │
│  │   DB)       │   │  .Worker         │     LLM injection            │
│  └─────────────┘   └───────┬──────────┘     Compile check           │
│                            │                                          │
│                   ┌────────▼─────────┐                               │
│                   │  Engine Library  │  ← Core orchestration         │
│                   │  StackAlchemist  │     Swiss Cheese Method        │
│                   │  .Engine         │     State machine              │
│                   └────────┬─────────┘                               │
│                            │                                          │
│                   ┌────────▼─────────┐                               │
│                   │  Template Library│  ← Handlebars templates        │
│                   │  StackAlchemist  │     Per language/framework     │
│                   │  .Templates      │                                │
│                   └──────────────────┘                               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

The solution is organized into focused projects under `src/`:

| Project | Type | Purpose |
|---------|------|---------|
| `StackAlchemist.Web` | Next.js 15 | Frontend: user interface, auth, download |
| `StackAlchemist.Placeholder` | ASP.NET Core | API gateway, routing, auth middleware |
| `StackAlchemist.Engine` | .NET Class Library | Core generation orchestration logic |
| `StackAlchemist.Worker` | .NET Worker Service | Background job processor |
| `StackAlchemist.Templates` | .NET Class Library | Handlebars template library |
| `StackAlchemist.Engine.Tests` | xUnit | Engine unit tests |
| `StackAlchemist.Worker.Tests` | xUnit | Worker integration tests |

---

## Request Flow: Simple Mode Generation

```
User Types Prompt → Clicks "Synthesize"
    │
    ▼
Next.js Frontend
    │ POST /api/generate {prompt, tier}
    │
    ▼
API Gateway (Placeholder)
    ├── Authenticate (Supabase JWT)
    ├── Validate request
    ├── Create generation job record in DB
    ├── Enqueue job to Worker queue
    └── Return {jobId}
    │
    ▼
Worker Service picks up job
    │
    ▼
Engine.GenerationOrchestrator
    │
    ├── Phase 1: Schema Extraction
    │   └── LLM call: "Extract entities from: {prompt}"
    │   └── Returns: EntitySchema (JSON)
    │
    ├── Phase 2: Template Rendering
    │   └── For each entity in schema:
    │       ├── Render controller template
    │       ├── Render repository template
    │       ├── Render model template
    │       ├── Render migration template
    │       └── Render TypeScript interface template
    │
    ├── Phase 3: LLM Injection
    │   └── For each injection point in rendered files:
    │       └── LLM call: "Implement: {method signature + context}"
    │
    ├── Phase 4: Compile Check
    │   ├── Write files to temp container
    │   ├── Run dotnet build
    │   ├── Run npm run build
    │   └── If fail: auto-correction loop (max 3 retries)
    │
    └── Phase 5: Package
        ├── Assemble ZIP archive
        ├── Upload to Supabase Storage
        └── Notify user via WebSocket
    │
    ▼
Frontend receives WebSocket event
User downloads ZIP archive
```

---

## State Machine

The generation process is managed by a formal state machine in `StackAlchemist.Engine`. States:

```
PENDING → EXTRACTING_SCHEMA → RENDERING_TEMPLATES → INJECTING_LOGIC
    → COMPILING → CORRECTING (0–3 times) → PACKAGING → COMPLETE
                                                       → FAILED → REFUND_ISSUED
```

Every state transition is persisted to the database. If the Worker crashes mid-generation, the job can be resumed from the last successful state.

See [Generation State Machine](../architecture/Generation%20State%20Machine.md) for the full state diagram.

---

## Key Design Decisions

### Why .NET for the backend?
The generated output is .NET. The generation engine needed to closely match the conventions and tooling of the output — using .NET to generate .NET means the templating, validation, and compilation all happen in the same ecosystem. See `docs/architecture/DECISIONS.md` for the full ADR.

### Why Supabase?
Supabase provides auth, row-level security, and storage without requiring a separate identity service. The generated code also uses Supabase — using the same stack for the platform itself keeps the architecture coherent and lets us dogfood our own output.

### Why Dapper over Entity Framework?
EF Core adds significant complexity to the generated code and makes templates harder to reason about. Dapper keeps the data layer explicit — the generated SQL is readable, portable, and easy for developers to modify. See [The Swiss Cheese Method](./swiss-cheese-method) for how Dapper fits into the generation model.

### Why async workers?
Generation is not a fast operation. Schema extraction, template rendering, LLM injection, and compilation can take 30–90 seconds. Running this synchronously in an HTTP request would require long-polling with unreliable connection handling. The Worker queue decouples the HTTP layer from the generation pipeline and enables real-time progress reporting via WebSocket.

---

## Real-Time Progress

The frontend subscribes to generation events via Supabase Realtime (WebSocket). As the Worker progresses through each phase, it writes status updates to the `generation_jobs` table. Supabase Realtime pushes these updates to the connected frontend client.

Progress events:
```
{ phase: "extracting_schema", progress: 10 }
{ phase: "rendering_templates", progress: 30 }
{ phase: "injecting_logic", progress: 55 }
{ phase: "compiling", progress: 75 }
{ phase: "packaging", progress: 90 }
{ phase: "complete", progress: 100, downloadUrl: "..." }
```

---

## Infrastructure

### Development
- Docker Compose: API, frontend, PostgreSQL, and the Worker run locally via `docker compose up`
- The `.env.example` file documents all required environment variables

### Production
- The API and Worker run as containerized services
- Supabase (managed cloud) handles auth and database
- Generated packages are stored in Supabase Storage (S3-compatible)
- Download links are signed URLs with expiry

---

## Testing

| Layer | Framework | Coverage Target |
|-------|-----------|-----------------|
| Engine unit tests | xUnit | Core orchestration logic, state machine |
| Worker integration tests | xUnit + TestContainers | End-to-end generation flow |
| Frontend unit tests | Vitest + Testing Library | Components, form validation |
| E2E tests | Playwright | Full generation flows |

See `docs/architecture/Testing Strategy.md` for the full testing strategy.

---

## Related Docs

- [The Swiss Cheese Method →](./swiss-cheese-method)
- [The Compile Guarantee →](./compile-guarantee)
- [Self-Hosting →](./self-hosting)
- [Generation State Machine →](../architecture/Generation%20State%20Machine.md)
