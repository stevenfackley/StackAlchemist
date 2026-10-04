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

StackAlchemist generates a complete, compilable source repository from a description of your SaaS idea. You describe what you're building — your entities, relationships, and API surface — and receive a full project archive with a .NET 10 Web API backend, Next.js 16 frontend, a PostgreSQL migration, the Supabase client preinstalled and env-wired (no auth flow is generated), and a Docker Compose dev environment. A free Spark build lets you watch the workflow run first. Code is written by Claude Sonnet 5.5 by default, or by a model of your choice if you bring your own key.

There are two ways to describe your project:

| Mode | Best For |
|------|----------|
| **Simple Mode** | Natural language descriptions. You type what you're building and the model works out the entities when your paid build runs. |
| **Advanced Mode** | Precise control. You model entities, relationships, and API endpoints using the visual wizard. |

---

## Getting Started

1. Create an account or sign in (email and password, or Google, on `auth.stackalchemist.app`). Both input modes require it.
2. On the homepage, choose your input mode using the **SIMPLE / ADVANCED** toggle.
3. Describe or model your project.
4. Click **Synthesize**. A free Spark build runs first (5 per calendar month per account): a fixed demo app in your browser, not built from your description.
5. Select a paid tier (Blueprint, Boilerplate, or Infrastructure) on the delivery page and complete checkout. Your generation begins right after payment.
6. Watch status updates as your project is synthesized. The page refreshes itself every few seconds.
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
3. Click **SYNTHESIZE** (or press `Ctrl + Enter`).

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

1. Run a free Spark build and take you to the delivery page
2. Prompt you to select a [generation tier](#generation-tiers)
3. Begin synthesis after checkout

Simple Mode has no schema-review step: your description goes to the model as written. To see and edit the entity model before buying, use Advanced Mode.

---

## Advanced Mode

Advanced Mode provides a step-by-step visual wizard for precise schema control. Use this when you know exactly what you want to generate.

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

StackAlchemist offers a free demo tier and three paid tiers of output, each building on the previous. All paid prices are one-time.

### Spark — Free

A fixed demo app (a small task tracker with your project name) running in your browser via StackBlitz, with every file editable. No AI call, not built from your description, not downloadable. 5 builds per calendar month per account.


### Blueprint — $299 — Architecture Documentation

**Best for:** Validation, planning, technical review before committing to implementation.

**What you receive:** two files and nothing else.
- `schema.json` — the normalized entity-relationship model
- `api-docs.md` — the CRUD contract per entity, plus the relationship list

**Use this when:** You want to validate your schema with stakeholders or teammates before building.

---

### Boilerplate — $599 — Full Source Code

**Best for:** Developers who want a running codebase to build from.

**What you receive (ZIP archive):**
```
your-project/
├── dotnet/
│   ├── YourProject.csproj          # .NET 10 Web API
│   ├── Program.cs                  # Minimal API + DI setup
│   ├── Controllers/                # One endpoint class per entity
│   ├── Models/                     # Entity records
│   ├── Repositories/               # Dapper data access
│   ├── Infrastructure/             # IDbConnectionFactory
│   ├── Migrations/                 # SQL migration files
│   └── appsettings.json
├── nextjs/
│   ├── package.json                # Next.js 16 + TypeScript
│   ├── src/app/                    # App Router pages
│   ├── src/lib/api.ts              # Type-safe API client
│   └── src/types/index.ts          # Generated TypeScript types
├── docker-compose.yml              # Full stack orchestration
├── .env.example                    # Environment variable template
├── build-report.json               # Every build command run against your code, and the verdict
└── Dockerfile
```

The two top-level directories are `dotnet/` and `nextjs/` — every generated file
lands inside one of them.

**Compile guarantee:** Both halves are built for real before delivery: `dotnet build` for the API, and `npm ci`, typecheck and `next build` for the frontend. If a build fails, the compiler output goes back to the model for correction (up to 3 attempts). If it still fails, you are refunded in full automatically.

---

### Infrastructure — $999 — Cloud Deployment Ready

**Best for:** Teams ready to deploy to production on AWS.

**Everything in Boilerplate, plus:**
```
infra/
├── cdk/
│   ├── lib/<project>-stack.ts      # VPC, ECS Fargate + ALB, RDS PostgreSQL
│   ├── package.json
│   └── tsconfig.json
├── terraform/
│   ├── main.tf                     # VPC, ECS, ALB, RDS, CloudWatch logs
│   ├── variables.tf
│   └── outputs.tf
└── helm/
    ├── Chart.yaml
    ├── values.yaml
    └── templates/                  # deployment, service, ingress, HPA, config, secrets
DEPLOYMENT.md                       # preflight, deploy, rollback runbook
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

3. **Compile validation** — Both halves are built for real (`dotnet build`; `npm ci`, typecheck, `next build`). If a build fails, the compiler error is fed back to the model as context for correction. Up to 3 correction attempts are made.

4. **Delivery** — Only green builds are packaged into the downloadable ZIP archive. A paid build that cannot be fixed is refunded automatically.

Note: the production engine currently uses a one-shot path (the model writes the whole codebase in one call and the files are reassembled), not the per-zone injection pictured above. The Swiss Cheese path is available in development builds.

### Status Phases

During generation the status page refreshes itself every few seconds while the tab is visible. The stages are:

| Status | Meaning |
|--------|---------|
| `pending` | Job accepted, waiting to start |
| `extracting_schema` | Reading your description into a schema |
| `generating_code` / `generating` | The model is writing your code |
| `building` | Compile Guarantee: real builds, with correction retries (up to 3) |
| `packing` | Building the ZIP archive |
| `uploading` | Storing the archive for download |
| `success` | Ready for download |
| `failed` | Generation failed after all retry attempts; paid tiers are refunded |

---

## Downloading Your Project

When generation completes:

1. A **Download** button appears on the generation status page.
2. Click it to download `your-project-name.zip`.
3. The download link is valid for **7 days**.

Your generation history on the dashboard lists past builds. After the link expires, email support@stackalchemist.app for a fresh one.

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
# Check the DATABASE_URL values in .env. The Supabase entries are placeholders
# for a preinstalled client; nothing reads them until you add auth.

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
cd dotnet
dotnet restore
dotnet run
# API listens on the port in ASPNETCORE_URLS (http://localhost:5000 in .env.example)
```

**Frontend (Next.js):**
```bash
cd nextjs
npm ci
npm run dev
# Available at http://localhost:3000
```

**Database migrations:**
```bash
# Migrations are in dotnet/Migrations/
# Docker Compose mounts them into Postgres, so they run once on the first boot
# of an empty volume (docker compose down -v to re-run)
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | `postgres://postgres:postgres@localhost:5432/mydb` |
| `NEXT_PUBLIC_SUPABASE_URL` | Placeholder for the preinstalled Supabase client | Unused until you wire auth |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | Placeholder for the preinstalled Supabase client | Unused until you wire auth |
| `NEXT_PUBLIC_API_URL` | Backend API URL for frontend | `http://localhost:5000` |

---

## Frequently Asked Questions

### Does the generated code actually compile?

Yes. StackAlchemist includes a **Compile Guarantee**: both the .NET backend and the Next.js frontend are built for real before the archive is assembled. If a build fails, the engine feeds the error output back to the model for correction — up to 3 times. Your archive is only delivered after a successful build, and a paid build that still fails is refunded automatically.

### Is the generated code production-ready?

The generated code is a solid, production-structured foundation. It is a single ASP.NET Core project organized by folder (endpoints, repositories, models), with a typed API client and a SQL migration with foreign keys. It ships with no authentication and no RLS policies. Review it, add auth suited to your requirements, and write tests before deploying to production.

### Can I regenerate with a different tier?

Yes. Return to the homepage and submit a new generation. Each generation is independent and priced separately.

### How long does generation take?

Typically **30–90 seconds** for Boilerplate, depending on schema complexity. Infrastructure adds cloud infrastructure generation and can take longer.

### What if my generation fails?

If a paid build fails after all correction attempts, you are refunded in full automatically and emailed. You can retry the same schema or refine your prompt and submit a new generation. Check the [Troubleshooting guide](./troubleshooting.md) for common issues.

### Can I use the output commercially?

Yes. Generated code is yours. StackAlchemist retains no rights to the code you generate.

### What is the V1 stack? Can I request different technologies?

The default stack is .NET 10 API + Next.js 16 + PostgreSQL + Docker, with the Supabase client preinstalled but unused. A FastAPI (Python) + React variant is also available. Other stacks (Node/Express, Laravel, etc.) are not offered yet.

### How are relationships handled in the generated database?

Each `one-to-many` relationship generates a foreign key constraint in the SQL migration. `many-to-many` relationships generate a join table with appropriate indexes. Relationship names follow your entity naming conventions.

### Does Advanced Mode support complex nested schemas?

Yes. You can define multiple entities with multiple relationships between them. The entity diagram panel shows the visual graph of your schema as you build it. The LLM uses the full relationship graph when generating query implementations.

---

*For issues and troubleshooting, see [troubleshooting.md](./troubleshooting.md).*  
*For architectural decisions and system design, see [docs/architecture/](../architecture/).*
