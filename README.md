<div align="center">
  <img src="src/StackAlchemist.Web/public/logo_nebula.png" alt="StackAlchemist" width="480"/>

  **Transmute natural language into production-ready software.**

  [![License: Proprietary](https://img.shields.io/badge/License-Proprietary-red.svg)](LICENSE)
  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
  [![Next.js](https://img.shields.io/badge/Next.js-16-black?logo=next.js)](https://nextjs.org)
  [![PostgreSQL](https://img.shields.io/badge/PostgreSQL-qavren--db-4169E1?logo=postgresql&logoColor=white)](docs/runbooks/qavren-db-migrations.md)
  [![Claude](https://img.shields.io/badge/Claude-Sonnet%205.5-D97757?logo=anthropic)](https://anthropic.com)
  [![Tests](https://img.shields.io/badge/Tests-passing-success)](#-running-tests)
</div>

---

StackAlchemist converts a plain-English brief into a **fully compilable, deployable SaaS repository** — complete with PostgreSQL schema, a .NET Web API + Next.js frontend (or FastAPI + React), and optional IaC scripts. The Compile Guarantee builds every generated project for real (`dotnet build`, or `npm ci` → typecheck → `npm run build`; pip install, lint and pytest for the Python half) and retries through an LLM repair loop (up to 3 attempts) if it fails. A paid build that still fails is refunded automatically.

---

## ⚠️ License

StackAlchemist is **Proprietary & Source Available**.

- **Personal / evaluation use:** Fork, run locally, explore freely — non-commercial.
- **Commercial production use:** Purchase a tier at [stackalchemist.app](https://stackalchemist.app).

See [LICENSE](LICENSE) for full terms.

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | Next.js 16 (App Router), React 19, Tailwind CSS 4, `@xyflow/react` |
| **Generation Engine** | .NET 10 Web API, Handlebars.Net, `System.IO.Abstractions` |
| **LLM** | Anthropic Claude Sonnet 5.5 (`claude-sonnet-5-5`, configurable via `ANTHROPIC_MODEL`; mock fallback when `ANTHROPIC_API_KEY` is unset). BYOK: Claude Opus 5.5, Haiku 4.5, OpenAI, OpenRouter |
| **Compile Worker** | In-process `BackgroundService` (`CompileWorkerService`): real build + LLM repair loop. `StackAlchemist.Worker` exists for scale-out but is not deployed |
| **Database** | qavren-db: shared Postgres, schema `stackalchemist`. Drizzle + postgres-js (web), Npgsql (Engine). No RLS; every query is owner-scoped in code |
| **Auth** | Qavren Auth: Keycloak realm via Auth.js v5 (`next-auth` + `@qavren/auth-next`), email/password + Google |
| **Live status** | Polling (`getGeneration` server action every 3 s while the tab is visible); no WebSockets |
| **Object Storage** | Cloudflare R2 (S3-compatible, zero egress) — AWSSDK.S3 |
| **Payments** | Stripe Checkout + signature-verified webhooks, automatic refunds |
| **Email** | Resend (receipt, build ready, refund issued); sign-in emails come from Keycloak |
| **Testing** | xUnit, NSubstitute, FluentAssertions, Testcontainers |

---

## 🚀 Quick Start — Local Development

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0+ |
| Node.js | 20+ |
| npm | 10+ |
| Docker | Any recent version (optional: Postgres/Keycloak for the real auth path, Docker Compose) |
| PostgreSQL + Keycloak | Optional locally; without them the web app runs in demo mode |

### Steps

```bash
# 1. Clone
git clone https://github.com/stevenfackley/StackAlchemist.git
cd StackAlchemist

# 2. Install deps
#    root postinstall bootstraps the frontend workspace and creates .env from .env.example
npm install

# 3. Configure secrets
#    Open .env at the solution root and fill in real values.
#    See .env.example for descriptions of every key.
#
#    Minimum required for full pipeline:
#      ANTHROPIC_API_KEY     — Anthropic Claude API key
#      R2_ACCOUNT_ID         — Cloudflare account ID
#      R2_ACCESS_KEY_ID      — R2 API token access key
#      R2_SECRET_ACCESS_KEY  — R2 API token secret
#      DATABASE_URL          — Postgres URL (qavren-db or any Postgres)
#      QAVREN_AUTH_URL / QAVREN_REALM / AUTH_SECRET — Keycloak realm (docs/runbooks/qavren-auth.md)
#      STRIPE_SECRET_KEY / STRIPE_WEBHOOK_SECRET
#
#    Leave ANTHROPIC_API_KEY blank → MockLlmClient (safe for UI dev, no API cost)

# 4. Restore .NET packages
dotnet restore StackAlchemist.slnx

# 5. Run the backend engine (Terminal A — listens on :5000)
dotnet run --project src/StackAlchemist.Engine

# 6. (with DATABASE_URL set) apply the schema
npm run db:migrate --prefix src/StackAlchemist.Web

# 7. Run the frontend (Terminal B — listens on :3000)
npm run dev --prefix src/StackAlchemist.Web
```

Open [http://localhost:3000](http://localhost:3000).

> **Demo mode:** with no Supabase URL set outside production, the web app auto-enables demo mode (no sign-in, no database). For the real auth path set `NEXT_PUBLIC_DEMO_MODE=false`, `DATABASE_URL`, `QAVREN_AUTH_URL`, `QAVREN_REALM` and `AUTH_SECRET` in `src/StackAlchemist.Web/.env.local`. Full local recipe: [Qavren Auth runbook](docs/runbooks/qavren-auth.md).

> **Note:** The Engine and the Compile Worker run in the same process (in-process `Channel<T>`). You only need to start `StackAlchemist.Engine` — no separate Worker process is required for local development.

### Docker Compose (alternative)

```bash
# Copy .env.example to .env and fill it in (docker-compose.yml reads it), then:
docker compose up
```

### Running Tests

```bash
# Preferred repo-owned runner
pwsh ./scripts/run-tests.ps1

# Quick summary only
pwsh ./scripts/run-tests.ps1 -Quiet
```

The PowerShell runner restores the worker host once, then runs `StackAlchemist.Engine.Tests` and `StackAlchemist.Worker.Tests` explicitly with `--no-restore` so failures are isolated to the project that actually broke.

If you want the raw command, it is still:

```bash
dotnet restore src/StackAlchemist.Worker/StackAlchemist.Worker.csproj
dotnet test src/StackAlchemist.Engine.Tests/StackAlchemist.Engine.Tests.csproj --no-restore
dotnet test src/StackAlchemist.Worker.Tests/StackAlchemist.Worker.Tests.csproj --no-restore
```

The .NET suite includes Engine.Tests and Worker.Tests (unit + integration). Frontend verification is separate, from `src/StackAlchemist.Web`: `npm run lint`, `npx tsc --noEmit`, `npx vitest run`, and Playwright via `npm run e2e:smoke` (demo mode) or `npm run e2e:integration` (needs Postgres + Keycloak, see `docker/docker-compose.test.yml`).

---

## 🏗️ How It Works

```
User brief (natural language or entity wizard)
        │
        ▼
  [ Next.js Frontend ]
  Simple Mode: LLM extracts JSON schema from prompt
  Advanced Mode: user builds schema manually in canvas
  Server Actions call the Engine with X-Engine-Key
        │
        ▼ POST /api/generate
  [ .NET Engine API ]
  1. Load the Handlebars template set (DotNet+Next.js or Python+React)
  2. Render project-level variables (ProjectName, palette, vocabulary …)
  3. Call Claude Sonnet 5.5 → get [[FILE:path]]…[[END_FILE]] blocks
     (V1 one-shot in prod; V2 "Swiss Cheese" per-zone injection is Development-only)
  4. Reconstruct: merge LLM output into template injection zones
  5. Write to temp directory → push to in-process Channel
        │
        ▼
  [ Compile Worker (BackgroundService) ]
  6. Real build in temp dir (dotnet build / npm ci + tsc + next build / pip + lint + pytest)
  7. On failure: extract errors → retry prompt → LLM repair (max 3×)
  8. On success: zip (with build-report.json) → upload to Cloudflare R2
  9. Generate presigned download URL
 10. Update the generations row in Postgres; the status page sees it on its next poll
     (final failure of a paid tier: automatic Stripe refund + email)
        │
        ▼
  [ User downloads project.zip ]
```

---

## 📦 Delivery Tiers

| Tier | Name | Price | Deliverables |
|---|---|---|---|
| **Tier 0** | Spark | Free (5 builds per calendar month) | Runnable Node demo previewed in StackBlitz; no download |
| **Tier 1** | Blueprint | $299 | Schema JSON, OpenAPI spec, SQL migration scripts, Markdown docs |
| **Tier 2** | Boilerplate | $599 | Everything in Blueprint + full compilable codebase (zip) |
| **Tier 3** | Infrastructure | $999 | Everything in Boilerplate + AWS CDK (TypeScript), Terraform, Helm chart, generated `DEPLOYMENT.md` |

---

## 📂 Repository Structure

```
StackAlchemist/
├── .env.example                    # Template — cp to .env and fill in secrets
├── scripts/
│   └── setup-env.mjs              # Auto-creates .env on install
├── src/
│   ├── StackAlchemist.Web/        # Next.js 16 frontend (App Router)
│   ├── StackAlchemist.Engine/     # .NET 10 Web API + BackgroundService
│   │   ├── Services/              # Orchestrator, LLM client, R2, Postgres delivery/billing
│   │   ├── Models/                # Generation state machine, schema models
│   │   └── Prompts/               # V1-generation.md system prompt
│   ├── StackAlchemist.Worker/     # Standalone worker host (scale-out, not deployed)
│   ├── StackAlchemist.Templates/  # Handlebars template sets
│   │   ├── V0-Spark-NextJs/       # Tier 0 runnable demo
│   │   ├── V1-DotNet-NextJs/      # dotnet/, nextjs/, infra/
│   │   ├── V1-Python-React/       # backend/, frontend/, infra/
│   │   ├── V2-DotNet-NextJs/      # Swiss Cheese zones
│   │   ├── V2-Python-React/
│   │   └── Tier3-Infrastructure/  # CDK, Helm, Terraform
│   ├── StackAlchemist.Engine.Tests/
│   └── StackAlchemist.Worker.Tests/
├── scripts/
├── docker/
```

---

## 📖 Documentation

| Document | Description |
|---|---|
| [DECISIONS.md](docs/DECISIONS.md) | Architectural decisions log (Phases 1–4) |
| [Software Design Document](docs/architecture/Software%20Design%20Document.md) | System architecture |
| [Dev Environment Setup](docs/architecture/Dev%20Environment%20Setup.md) | Infrastructure & deployment |
| [Generation State Machine](docs/architecture/Generation%20State%20Machine.md) | Pipeline state transitions |
| [PRD](docs/product/Product%20Requirements%20Document.md) | Product requirements |
| [User Guide](docs/user/user-guide.md) | End-user guide |
| [Runbook: Qavren Auth](docs/runbooks/qavren-auth.md) | Keycloak realm, env contract, local recipe |
| [Runbook: qavren-db migrations](docs/runbooks/qavren-db-migrations.md) | Drizzle migrations, `DATABASE_URL` vs `DATABASE_URL_MIGRATE` |
| [Self-hosting](docs/advanced-docs/self-hosting.md) | Run the repo yourself |
| [Legacy: CI Supabase migrations](docs/runbooks/ci-supabase-migrations.md) | Rollback-only Supabase mode, being removed |

---

## 🧪 Mascots

### Auri — Main Mascot

Auri is our resident alchemist: equal parts wise guide and chaotic builder energy. He represents what StackAlchemist is built for — turning rough ideas into shippable systems, without losing the magic of building.

<p align="center">
  <img src="docs/branding/main-mascot-alchemist.svg" alt="Auri, the StackAlchemist main mascot" width="320" />
</p>

### Reto — Swiss Cheese Method

Reto is the specialist behind the Swiss Cheese Method. He keeps structure solid, leaves room for intelligent variation, and reminds us that reliable software is equal parts discipline and craft.

<p align="center">
  <img src="docs/branding/swiss-cheese-mascot.svg" alt="Reto, the Swiss Cheese Method mascot" width="320" />
</p>

---

<div align="center">
  <sub>Built by StackAlchemist · All rights reserved</sub>
</div>
