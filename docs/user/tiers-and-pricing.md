# Tiers and Pricing

StackAlchemist uses a three-tier model. Each tier builds on the one before it. All prices are **one-time payments** — there are no subscriptions, seat licenses, or recurring fees.

---

## Tier Overview

| | **Tier 1 — Blueprint** | **Tier 2 — Boilerplate** | **Tier 3 — Infrastructure** |
|---|---|---|---|
| **Price** | $299 | $599 | $999 |
| **Best For** | Planning, RFPs, architecture review | Developers starting a new product | Teams ready to deploy to production |
| **Compile Guarantee** | — | ✅ Yes | ✅ Yes |
| **Source Code** | — | ✅ Yes | ✅ Yes |
| **Cloud IaC** | — | — | ✅ Yes |

---

## Tier 1 — Blueprint

**$299 · One-time**

The Blueprint tier delivers the architecture and planning artifacts for your system — without generating any code. This is useful when you need to communicate the design to stakeholders, prepare an RFP, or validate the approach before committing to implementation.

### What's included

- **Entity-Relationship Schema** (JSON) — The normalized data model derived from your description
- **OpenAPI 3.0 Specification** — Full API surface in machine-readable format, importable into Postman, Insomnia, or any API tooling
- **SQL Migration Scripts** — PostgreSQL DDL to create all tables, indexes, foreign keys, and constraints
- **Data Flow Diagram** — Visual representation of how data moves through the system
- **Architecture Decision Records (ADRs)** — Documented reasoning behind key technical choices

### Who it's for

- Technical leads validating a design before handing off to engineering
- Developers who want to scaffold manually but need the model done first
- Agencies preparing proposals or SOWs for clients
- Solo developers who want to think through the schema before writing code

---

## Tier 2 — Boilerplate

**$599 · One-time**

The Boilerplate tier delivers a complete, compilable source repository. This is the core product — a real codebase shaped exactly around your schema, with the Compile Guarantee ensuring it builds before delivery.

### What's included

**Everything in Blueprint, plus:**

- **.NET 10 Web API** — Controllers, repositories, models, and service layer with Dapper ORM and PostgreSQL. Clean architecture with proper project separation.
- **Next.js 15 Frontend** — App Router, TypeScript strict mode, Tailwind CSS, and a type-safe API client generated from your schema. Authentication UI wired up.
- **PostgreSQL Schema + Migrations** — Ready to run via Docker or against any Postgres instance
- **Supabase Auth Integration** — Row Level Security policies scaffolded for your entities. Auth flows wired into the frontend (sign up, sign in, session management).
- **Docker Compose Dev Environment** — One command spins up the database, API, and frontend together
- **Compile Guarantee** — The 3-retry auto-correction loop. If the code doesn't compile, it doesn't ship. If it still fails after 3 retries, you get a full refund.

### Who it's for

- Developers starting a new product who want to skip the scaffolding sprint
- CTOs who need a production-quality starting point without spending 2–4 weeks on setup
- Agencies delivering initial builds to clients on tight timelines
- Entrepreneurs who want to start building features on day one

---

## Tier 3 — Infrastructure

**$999 · One-time**

The Infrastructure tier is everything in Boilerplate plus a complete cloud deployment package. A junior engineer could take this handoff and deploy it to production without needing a senior to architect the cloud setup.

### What's included

**Everything in Boilerplate, plus:**

- **AWS CDK Stack (TypeScript)** — Lambda functions, RDS Aurora (PostgreSQL-compatible), S3, CloudFront distribution, IAM roles and policies — all as code.
- **Helm Charts** — Kubernetes deployment manifests for teams running container orchestration.
- **GitHub Actions CI/CD Pipeline** — Test, build, and deploy workflow that runs on every push to `main`.
- **Deployment Runbook** — Step-by-step instructions: account setup, secrets configuration, first deploy, monitoring setup, rollback procedure.
- **Environment Configuration Guide** — Every environment variable documented, with instructions for dev, staging, and production environments.
- **Cost Estimation Report** — AWS cost projections at 1k, 10k, and 100k monthly active users.

### Who it's for

- Teams who want to go straight from generated code to production
- Organizations with no dedicated DevOps that still need a cloud-ready setup
- Technical founders who want the cloud infrastructure done right from the start
- Agencies handing off a complete project to a client

---

## The Compile Guarantee

For Tier 2 and Tier 3, every generated package goes through the following before delivery:

1. Generated code is written to a temporary container
2. `dotnet build` is executed against the API project
3. `npm run build` is executed against the frontend
4. If either build fails, the LLM receives the error output and regenerates the failing files
5. Steps 2–4 repeat up to **3 times**
6. If the build is still failing after 3 attempts: **full refund, no questions asked**

This is a hard technical guarantee — not a marketing claim. Code that doesn't compile doesn't get delivered.

---

## Pricing FAQ

### Is this a subscription?
No. Every tier is a one-time payment. You pay once per generated project. You keep the output forever.

### What if I need to make changes after download?
The generated code is standard .NET and Next.js. You own it completely. Modify it, extend it, refactor it — it's your codebase.

### Can I use the generated code commercially?
Yes. The generated output has no licensing restrictions. Build your product, sell it, scale it.

### What if the Compile Guarantee fails?
If the generated code doesn't compile after 3 auto-correction attempts, you receive a full refund automatically. No dispute process required.

### Are there bulk or agency discounts?
Contact us at [stackalchemist.app](https://stackalchemist.app) to discuss volume pricing for agencies or teams.

---

## Related Docs

- [Getting Started →](./getting-started)
- [Understanding your output →](./your-output)
- [The Compile Guarantee (advanced) →](./compile-guarantee)
