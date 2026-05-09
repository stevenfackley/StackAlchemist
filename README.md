<div align="center">
  <img src="src/StackAlchemist.Web/public/logo_nebula.png" alt="StackAlchemist" width="480"/>

  **Transmute natural language into production-ready software.**

  [![License: Proprietary](https://img.shields.io/badge/License-Proprietary-red.svg)](LICENSE)
  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
  [![Next.js](https://img.shields.io/badge/Next.js-15-black?logo=next.js)](https://nextjs.org)
  [![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL-3ECF8E?logo=supabase)](https://supabase.com)
  [![Claude](https://img.shields.io/badge/Claude-3.5%20Sonnet-D97757?logo=anthropic)](https://anthropic.com)
  [![Tests](https://img.shields.io/badge/Tests-passing-success)](#-running-tests)
</div>

---

StackAlchemist converts a plain-English brief into a **fully compilable, deployable SaaS repository** — complete with PostgreSQL schema, .NET Web API, Next.js frontend, and optional IaC scripts. The engine guarantees compilable output by running `dotnet build` on every generation and retrying with error context through the LLM repair loop if it fails.

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
| **Frontend** | Next.js 15 (App Router), React 19, Tailwind CSS 3, `@xyflow/react` |
| **Generation Engine** | .NET 10 Web API, Handlebars.Net, `System.IO.Abstractions` |
| **LLM** | Anthropic Claude Sonnet 4.6 (configurable via `ANTHROPIC_MODEL`; mock fallback when `ANTHROPIC_API_KEY` is unset) |
| **Compile Worker** | In-process `BackgroundService` — `dotnet build` + LLM retry loop |
| **Database & Auth** | Supabase PostgreSQL, Row Level Security, Supabase Auth |
| **Realtime** | Supabase Realtime (WebSockets — live generation progress) |
| **Object Storage** | Cloudflare R2 (S3-compatible, zero egress) — AWSSDK.S3 |
| **Payments** | Stripe Checkout + Webhooks |
| **Testing** | xUnit, NSubstitute, FluentAssertions, Testcontainers |

---

## 🚀 Quick Start — Local Development

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0+ |
| Node.js | 20+ |
| npm | 10+ |
| Docker | Any recent version (optional — for Supabase local) |

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
#      SUPABASE_SERVICE_ROLE_KEY — Supabase service-role key
#      STRIPE_SECRET_KEY / STRIPE_WEBHOOK_SECRET
#
#    Leave ANTHROPIC_API_KEY blank → MockLlmClient (safe for UI dev, no API cost)

# 4. Restore .NET packages
dotnet restore StackAlchemist.slnx

# 5. Run the backend engine (Terminal A — listens on :5000)
dotnet run --project src/StackAlchemist.Engine

# 6. Run the frontend (Terminal B — listens on :3000)
npm run dev --prefix src/StackAlchemist.Web
```

Open [http://localhost:3000](http://localhost:3000).

> **Note:** The Engine and the Compile Worker run in the same process (in-process `Channel<T>`). You only need to start `StackAlchemist.Engine` — no separate Worker process is required for local development.

### Docker Compose (alternative)

```bash
# Fill in .env first, then:
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

The .NET suite includes Engine.Tests and Worker.Tests (unit + integration). Frontend verification remains separate via the workspace `npm` scripts.

---

## 🏗️ How It Works

```
User brief (natural language or entity wizard)
        │
        ▼
  [ Next.js Frontend ]
  Simple Mode: LLM extracts JSON schema from prompt
  Advanced Mode: user builds schema manually in canvas
        │
        ▼ POST /api/generate
  [ .NET Engine API ]
  1. Load V1 Handlebars templates (dotnet/ + nextjs/ + infra/)
  2. Render project-level variables (ProjectName, DbConnectionString …)
  3. Call Claude Sonnet 4.6 → get [[FILE:path]]…[[END_FILE]] blocks
  4. Reconstruct: merge LLM output into template injection zones
  5. Write to temp directory → push to in-process Channel
        │
        ▼
  [ Compile Worker (BackgroundService) ]
  6. dotnet build in temp dir
  7. On failure: extract CS errors → retry prompt → LLM repair (max 3×)
  8. On success: zip directory → upload to Cloudflare R2
  9. Generate presigned download URL (168h expiry)
 10. PATCH Supabase generations row → frontend Realtime fires
        │
        ▼
  [ User downloads project.zip ]
```

---

## 📦 Delivery Tiers

| Tier | Name | Price | Deliverables |
|---|---|---|---|
| **Tier 0** | Spark | Free | Guided schema capture and prompt planning for evaluation use |
| **Tier 1** | Blueprint | $299 | Schema JSON, OpenAPI spec, SQL migration scripts, Markdown docs |
| **Tier 2** | Boilerplate | $599 | Everything in Blueprint + full compilable codebase (zip) |
| **Tier 3** | Infrastructure | $999 | Everything in Boilerplate + AWS CDK, Terraform, Helm Charts, deployment runbook |

---

## 📂 Repository Structure

```
StackAlchemist/
├── .env.example                    # Template — cp to .env and fill in secrets
├── scripts/
│   └── setup-env.mjs              # Auto-creates .env on install
├── src/
│   ├── StackAlchemist.Web/        # Next.js 15 frontend (App Router)
│   ├── StackAlchemist.Engine/     # .NET 10 Web API + BackgroundService
│   │   ├── Services/              # Orchestrator, LLM client, R2, Supabase delivery
│   │   ├── Models/                # Generation state machine, schema models
│   │   └── Prompts/               # V1-generation.md system prompt
│   ├── StackAlchemist.Worker/     # Standalone worker host (future scale-out)
│   ├── StackAlchemist.Templates/  # V1-DotNet-NextJs Handlebars templates
│   │   ├── V1-Python-React/
│   │   │   ├── backend/
│   │   │   ├── frontend/
│   │   │   └── infra/
│   │   └── V1-DotNet-NextJs/
│   │       ├── dotnet/            # .NET Web API scaffold (Dapper, Minimal API)
│   │       ├── nextjs/            # Next.js 15 scaffold
│   │       └── infra/             # Docker Compose, Dockerfile
│   ├── StackAlchemist.Engine.Tests/
│   └── StackAlchemist.Worker.Tests/
├── conductor/
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
