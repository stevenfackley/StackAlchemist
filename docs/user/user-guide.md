# StackAlchemist User Guide

> **Transmute your idea into a production-ready codebase in minutes.**

---

## Table of Contents

1. [Overview](#overview)
2. [Getting Started](#getting-started)
3. [Simple Mode](#simple-mode)
4. [Advanced Mode](#advanced-mode)
5. [Generation Tiers](#generation-tiers)
6. [The Generation Pipeline](#the-generation-pipeline)
7. [Downloading Your Project](#downloading-your-project)
8. [Running Your Generated Project Locally](#running-your-generated-project-locally)
9. [Frequently Asked Questions](#frequently-asked-questions)

---

## Overview

StackAlchemist generates a complete, compilable source repository from a description of your SaaS idea. You describe what you're building — your entities, relationships, and API surface — and receive a full project archive with a .NET 10 Web API backend, Next.js 15 frontend, PostgreSQL migrations, Supabase auth integration, and Docker Compose dev environment.

There are two ways to describe your project:

| Mode | Best For |
|------|----------|
| **Simple Mode** | Natural language descriptions. You type what you're building and the engine extracts the schema. |
| **Advanced Mode** | Precise control. You model entities, relationships, and API endpoints using the visual wizard. |

---

## Getting Started

1. Navigate to the StackAlchemist homepage (`/`).
2. Choose your input mode using the **SIMPLE / ADVANCED** toggle in the hero section.
3. Describe or model your project.
4. Select a generation tier (Blueprint, Boilerplate, or Infrastructure).
5. Complete checkout — your generation begins immediately.
6. Watch live status updates as your project is synthesized.
7. Download your ZIP archive when complete.

---

## Simple Mode

Simple Mode accepts a plain-language description of your SaaS product. No technical knowledge required — just describe what you're building the way you'd explain it to a colleague.

### How to Use Simple Mode

1. On the homepage, ensure the toggle is set to **SIMPLE**.
2. In the **AlchemyInput** terminal box, type a description of your project. Include:
   - What the product does
   - Key entities (e.g., Users, Products, Orders, Projects, Tasks)
   - Relationships between entities (e.g., "Users have many Projects", "Orders belong to a Customer")
   - Any important API behaviors (e.g., "Admins can approve orders", "Projects have status fields")
3. Click **SYNTHESIZE** (or press `Enter`).

### Example Prompts

**E-Commerce Platform:**
```
A multi-vendor e-commerce platform where Sellers can list Products with categories and inventory.
Customers can place Orders containing multiple Products. Orders have statuses: pending, paid,
shipped, delivered. Sellers see a dashboard of their own orders and revenue.
```

**Project Management Tool:**
```
A project management SaaS with Workspaces, Projects, and Tasks. Each Task has an assignee,
due date, priority level, and status. Users belong to Workspaces and can be assigned to
multiple Projects. Comments can be left on Tasks.
```

**Healthcare Scheduling:**
```
An appointment scheduling system for clinics. Patients can book Appointments with Doctors
who have Availability slots. Appointments have types (consultation, follow-up) and statuses.
Doctors belong to Departments.
```

### What Happens Next

After submission, StackAlchemist will:

1. Display the **extracted schema** (entities and endpoints the engine identified from your description)
2. Ask you to confirm or adjust before proceeding
3. Prompt you to select a [generation tier](#generation-tiers)
4. Begin synthesis after checkout

---

## Advanced Mode

Advanced Mode provides a 3-step visual wizard for precise schema control. Use this when you know exactly what you want to generate.

### Step 1: Define Entities

Add your data entities using the **+ Add Entity** button. For each entity:

- **Name** — The entity name (e.g., `User`, `Product`, `Order`). Use PascalCase.
- **Fields** — Add fields with names, types, and optional/required flags.
  - Supported types: `string`, `number`, `boolean`, `date`, `uuid`
- **Relationships** — Link entities together. Specify the target entity and relationship type:
  - `one-to-one` — Each record maps to exactly one record in the related entity
  - `one-to-many` — One record has many related records (e.g., User → Orders)
  - `many-to-many` — Both sides can have many records (e.g., Product ↔ Category)

The **Entity Diagram** panel on the right (visible on desktop) shows your schema visually as a ReactFlow graph. Nodes represent entities; edges represent relationships.

#### Example Entity: `Product`
| Field | Type | Required |
|-------|------|----------|
| `id` | `uuid` | ✓ |
| `name` | `string` | ✓ |
| `price` | `number` | ✓ |
| `description` | `string` | |
| `inStock` | `boolean` | ✓ |

### Step 2: Configure API

Define the REST endpoints your API should expose. For each endpoint:

- **Path** — The route path (e.g., `/products`, `/orders/{id}`)
- **Method** — HTTP method: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`
- **Description** — What this endpoint does (used by the LLM for implementation)

StackAlchemist will generate a fully implemented controller action for each endpoint, wired to the appropriate repository method.

#### Common Endpoint Patterns

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/products` | List all products (paginated) |
| `GET` | `/products/{id}` | Get a single product by ID |
| `POST` | `/products` | Create a new product |
| `PUT` | `/products/{id}` | Update an existing product |
| `DELETE` | `/products/{id}` | Delete a product |

### Step 3: Select Tier

Choose your [generation tier](#generation-tiers) and complete the checkout flow.

---

## Generation Tiers

StackAlchemist offers three tiers of output, each building on the previous:

### 🔵 Blueprint — Architecture Documentation

**Best for:** Validation, planning, technical review before committing to implementation.

**What you receive:**
- `ARCHITECTURE.md` — Full system design document with entity descriptions and relationships
- `DATABASE_SCHEMA.md` — ER diagram in Mermaid syntax + field definitions
- `API_SPEC.md` — OpenAPI-style endpoint documentation
- `README.md` — Setup overview

**Use this when:** You want to validate your schema with stakeholders or teammates before building.

---

### ⚡ Boilerplate — Full Source Code

**Best for:** Developers who want a running codebase to build from.

**What you receive (ZIP archive):**
```
your-project/
├── backend/
│   ├── YourProject.csproj         # .NET 10 Web API
│   ├── Program.cs                  # Minimal API + DI setup
│   ├── Controllers/                # Generated REST controllers
│   ├── Models/                     # Entity classes
│   ├── Repositories/               # Data access layer
│   ├── Migrations/                 # SQL migration files
│   └── appsettings.json
├── frontend/
│   ├── package.json                # Next.js 15 + TypeScript
│   ├── src/app/                    # App Router pages
│   ├── src/lib/api.ts              # Type-safe API client
│   └── src/types/index.ts          # Generated TypeScript types
├── docker-compose.yml              # Full stack orchestration
├── .env.example                    # Environment variable template
└── README.md
```

**Compile guarantee:** The generated backend is compiled with `dotnet build` before delivery. If compilation fails, the engine auto-corrects (up to 3 attempts) before reporting success.

---

### 🚀 Infrastructure — Cloud Deployment Ready

**Best for:** Teams ready to deploy to production on AWS.

**Everything in Boilerplate, plus:**
```
infra/
├── cdk/
│   ├── bin/app.ts                  # CDK app entry
│   ├── lib/
│   │   ├── database-stack.ts       # RDS PostgreSQL
│   │   ├── api-stack.ts            # Lambda + API Gateway
│   │   ├── frontend-stack.ts       # S3 + CloudFront
│   │   └── auth-stack.ts           # Cognito / Supabase wiring
│   └── package.json
├── .github/
│   └── workflows/
│       ├── ci.yml                  # Build + test pipeline
│       └── deploy.yml              # CDK deploy on merge to main
└── DEPLOYMENT.md
```

---

## The Generation Pipeline

Understanding how StackAlchemist generates code helps you write better prompts and debug unexpected output.

### The Swiss Cheese Method

StackAlchemist uses a hybrid template + LLM approach called the **Swiss Cheese Method**:

```
┌─────────────────────────────────────────────────────┐
│  Handlebars Templates (Static Structure)             │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │ Controllers  │  │   Models     │  │  Repos    │ │
│  │  skeleton    │  │   skeleton   │  │  skeleton │ │
│  └──────┬───────┘  └──────┬───────┘  └─────┬─────┘ │
│         │  [hole]          │  [hole]         │[hole] │
│         ▼                  ▼                 ▼       │
│        LLM fills business logic into the holes       │
└─────────────────────────────────────────────────────┘
```

1. **Static layer** — Handlebars templates define the file structure, class skeletons, import paths, constructor signatures. This ensures consistent, predictable scaffolding.

2. **Intelligence layer** — The LLM fills in the "holes": business logic, validation rules, query implementations, domain-specific behavior. Your schema drives what gets injected.

3. **Compile validation** — The generated `.NET` project is compiled with `dotnet build`. If it fails, the compiler error is fed back to the LLM as context for correction. Up to 3 correction attempts are made.

4. **Delivery** — Only green builds are packaged into the downloadable ZIP archive.

### Status Phases

During generation, you'll see real-time status updates:

| Status | Meaning |
|--------|---------|
| `queued` | Job accepted, waiting for a worker |
| `processing` | Engine is generating your code |
| `compiling` | Running `dotnet build` on the output |
| `correcting` | Compilation failed; LLM is correcting (retry N/3) |
| `packaging` | Building the ZIP archive |
| `completed` | Ready for download |
| `failed` | Generation failed after all retry attempts |

---

## Downloading Your Project

When generation completes:

1. A **Download** button appears on the generation status page.
2. Click it to download `your-project-name.zip`.
3. The download link is valid for **7 days**.

If you need to re-download, log into your account and visit your generation history.

---

## Running Your Generated Project Locally

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [Node.js 20+](https://nodejs.org/) (for frontend development)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for backend development without Docker)

### Quick Start (Docker Compose)

```bash
# 1. Extract the archive
unzip your-project.zip -d my-saas
cd my-saas

# 2. Copy environment config
cp .env.example .env
# Edit .env with your Supabase URL and anon key (or leave defaults for local dev)

# 3. Start everything
docker compose up

# Services will start on:
# Backend API:  http://localhost:5000
# Frontend:     http://localhost:3000
# PostgreSQL:   localhost:5432
```

### Running Services Individually

**Backend (.NET Web API):**
```bash
cd backend
dotnet restore
dotnet run
# API available at https://localhost:5001
```

**Frontend (Next.js):**
```bash
cd frontend
npm install
npm run dev
# Available at http://localhost:3000
```

**Database migrations:**
```bash
# Migrations are in backend/Migrations/
# They run automatically when the API starts (or via Docker Compose)
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | `postgres://postgres:postgres@localhost:5432/mydb` |
| `SUPABASE_URL` | Your Supabase project URL | Required for auth |
| `SUPABASE_ANON_KEY` | Supabase public anon key | Required for auth |
| `NEXT_PUBLIC_API_URL` | Backend API URL for frontend | `http://localhost:5000` |

---

## Frequently Asked Questions

### Does the generated code actually compile?

Yes. StackAlchemist includes a **Compile Guarantee**: the generated .NET backend is compiled with `dotnet build` before the archive is assembled. If compilation fails, the engine automatically feeds the error output back to the LLM for correction — up to 3 times. Your archive is only delivered after a successful build.

### Is the generated code production-ready?

The generated code is a solid, production-structured foundation. It follows clean architecture patterns: controllers, repositories, models, proper separation of concerns, typed API client, SQL migrations with foreign keys and constraints. You should review and extend it with your specific business logic, add authentication policies suited to your security requirements, and write tests before deploying to production.

### Can I regenerate with a different tier?

Yes. Return to the homepage and submit a new generation. Each generation is independent.

### How long does generation take?

Typically **30–90 seconds** for Boilerplate, depending on schema complexity. Infrastructure tier adds cloud infrastructure generation and may take up to 3 minutes.

### What if my generation fails?

If generation fails after all retry attempts, you will not be charged. You can retry the same schema or refine your prompt and submit a new generation. Check the [Troubleshooting guide](./troubleshooting.md) for common issues.

### Can I use the output commercially?

Yes. Generated code is yours. StackAlchemist retains no rights to the code you generate.

### What is the V1 stack? Can I request different technologies?

V1 generates a specific opinionated stack: .NET 10 API + Next.js 15 + PostgreSQL + Supabase + Docker. Additional stacks (Python/FastAPI, Node/Express, Laravel, etc.) are planned for future versions.

### How are relationships handled in the generated database?

Each `one-to-many` relationship generates a foreign key constraint in the SQL migration. `many-to-many` relationships generate a join table with appropriate indexes. Relationship names follow your entity naming conventions.

### Does Advanced Mode support complex nested schemas?

Yes. You can define multiple entities with multiple relationships between them. The entity diagram panel shows the visual graph of your schema as you build it. The LLM uses the full relationship graph when generating query implementations.

---

*For issues and troubleshooting, see [troubleshooting.md](./troubleshooting.md).*  
*For architectural decisions and system design, see [docs/architecture/](../architecture/).*
