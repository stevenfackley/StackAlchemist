# Tiers and Pricing

StackAlchemist has three paid tiers plus a free one. All prices are **one-time payments** — there are no subscriptions, seat licenses, or recurring fees.

---

## Tier Overview

| | **Tier 0 — Spark** | **Tier 1 — Blueprint** | **Tier 2 — Boilerplate** | **Tier 3 — Infrastructure** |
|---|---|---|---|---|
| **Price** | Free | $299 | $599 | $999 |
| **Best For** | Seeing the workflow run | Planning, RFPs, architecture review | Developers starting a new product | Teams ready to deploy to production |
| **Built from your schema** | — | ✅ Yes | ✅ Yes | ✅ Yes |
| **Compile Guarantee** | — | — | ✅ Yes | ✅ Yes |
| **Source Code** | — | — | ✅ Yes | ✅ Yes |
| **Cloud IaC** | — | — | — | ✅ Yes |

---

## Tier 0 — Spark

**Free · 5 builds per calendar month**

Spark runs the whole workflow so you can watch it work before paying. It renders one fixed
template — a small task tracker — with your project name substituted in, and makes **no AI
call**. That is why it is instant, costs nothing, and always boots.

Be clear about what it is not: the app you see is **not generated from your description**, it
has no .NET half, and it cannot be downloaded. Code built from your own schema starts at
Blueprint.

### What's included

- A working Next.js 15 app running in your browser via StackBlitz WebContainers (Chromium-based browsers only)
- Every file open in the embedded editor — read it, edit it, re-run it
- The same delivery page and flow that a paid run uses
- In Advanced Mode: the entity wizard and live ER canvas, with your schema saved on the build
  so you can return and buy a paid run against it

---

## Tier 1 — Blueprint

**$299 · One-time**

The Blueprint tier delivers the architecture and planning artifacts for your system — without generating any code. This is useful when you need to communicate the design to stakeholders, prepare an RFP, or validate the approach before committing to implementation.

### What's included

Two files, and they are the whole deliverable:

- **`schema.json`** — the normalized entity-relationship model: every entity, every field with
  its type, primary key, nullability and default, and the relationships between them
- **`api-docs.md`** — the CRUD contract in Markdown: a field table per entity and the five REST
  endpoints it implies (`GET` list, `GET` by id, `POST`, `PUT`, `DELETE`), plus the
  relationship list

No code is generated at this tier, and no SQL — the migration is produced at Boilerplate. What
you get is the model, in a form you can hand to a person or paste into a design doc.

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

- **.NET 10 minimal API** — a single ASP.NET Core project: a record and DTO per entity,
  an interface plus Dapper implementation per entity, and a CRUD endpoint group per entity,
  all wired into `Program.cs`. Not a multi-project clean-architecture solution — one project,
  organized by folder.
- **Next.js 15 frontend** — App Router, TypeScript strict mode, Tailwind CSS, a typed API
  client and generated interfaces. A home page linking your entities; the screens are yours
  to build.
- **PostgreSQL migration** — `001_initial_schema.sql` with UUID primary keys, foreign keys,
  and row-level security enabled per table. Runs automatically on first `docker compose up`.
- **Docker Compose dev environment** — one command spins up Postgres, the API, and the
  frontend, from a multi-stage Dockerfile with `web` and `engine` targets.
- **Compile Guarantee** — both halves are put through their real toolchains before the archive
  is packed, with up to 3 correction attempts; if it still fails, the charge is refunded
  automatically. The archive carries `build-report.json`, the record of every command.

Not included, so you can plan for it: no authentication flow, no RLS policies (RLS is enabled
but unpolicied), no payments integration, and no README. See
[Understanding your output](./your-output) for the exact file tree.

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

**Everything in Boilerplate, plus an `infra/` tree and a runbook:**

- **AWS CDK stack (TypeScript)** — VPC, ECS Fargate service behind an Application Load
  Balancer, and an RDS PostgreSQL instance, deployed with an image URI you supply.
- **Terraform AWS baseline** — the same shape as HCL for teams that live in Terraform: ECS,
  ALB, RDS, networking, and service logs.
- **Helm chart** — deployment, service, ingress, HPA, ConfigMap and Secret templates for
  teams running Kubernetes.
- **`DEPLOYMENT.md` runbook** — preflight checklist, the exact CDK / Terraform / Helm command
  sequences, secret handling, migration ordering, and rollback.

### Who it's for

- Teams who want to go straight from generated code to production
- Organizations with no dedicated DevOps that still need a cloud-ready setup
- Technical founders who want the cloud infrastructure done right from the start
- Agencies handing off a complete project to a client

---

## The Compile Guarantee

For Tier 2 and Tier 3, every generated package goes through the following before delivery:

1. Generated code is written to a temporary container
2. `dotnet restore` then `dotnet build --no-restore` run against the API half
3. `npm ci`, `npm run typecheck` (`tsc --noEmit`) then `next build` run against the frontend half
4. If either half fails, the LLM receives the error output and regenerates the failing files
5. Steps 2–4 repeat up to **3 times**
6. If the build is still failing after 3 attempts: **a full refund is initiated automatically, no questions asked**
7. On success, `build-report.json` — every command, exit code, and per-half verdict — is written
   into the archive, and the same verdict is shown on your delivery page

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
