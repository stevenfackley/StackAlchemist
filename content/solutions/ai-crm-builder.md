# Generate a full AI CRM from a prompt

You describe the kind of CRM your team actually needs. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI CRM with:

- **Contacts and accounts** — `Contact` and `Account` entities with owner, industry and the fields you describe, plus `CustomField` and `Tag`
- **Deal pipelines** — `Deal`, `Pipeline` and `Stage` entities with amounts, stage probabilities and expected close dates
- **Activities and tasks** — `Activity` and `Task` entities with type, assignee, due date and completion
- **Notes and logs** — `Note`, `EmailLog` and `CallLog` entities attached to contacts, accounts, or deals
- **Users, roles and teams** — `User`, `Role` and `Team` entities, the data your access rules are built on
- **Audit events** — an `AuditEvent` entity to record contact, deal, and stage changes
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Gmail and Outlook sync (OAuth), sign-in and server-side role enforcement (the Supabase client is preinstalled; the auth flows are yours to write), writing an audit event on every change, the kanban board and forecast reports, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of buying a CRM

**Salesforce, HubSpot, and Pipedrive are rented infrastructure for your sales process.** Every contact, deal, and pipeline event lives in their database. Every customization costs admin time or a paid app. Every API integration is gated behind their tier. You are paying per seat, per month, forever, for the right to access your own customer data.

**Generated CRM code is yours.** Your data lives in your Postgres. Your custom fields are columns you own. Your integrations are direct connections to the APIs you actually need, not paid add-ons in a vendor's marketplace.

**Generic CRMs assume a generic sales process.** Your pipeline, your stage definitions, your activity types, your reporting needs — they are all bent to fit Salesforce's model. A generated CRM bends to fit your prompt instead.

## Who this is for

- **Sales-led startups** who want their CRM to be a real internal asset, not a $150/seat/month line item.
- **Vertical SaaS founders** building CRMs for a specific industry (real estate, insurance, healthcare staffing) where the generic CRMs don't fit cleanly.
- **Agencies** delivering custom internal CRMs to clients with specialized sales motions.
- **Engineering teams** who already know their sales workflow needs custom code and want the scaffold generated instead of hand-built.

## Example entities generated

A typical AI CRM generation produces entities like:

- `Contact` / `Account`
- `Deal` / `Pipeline` / `Stage`
- `Activity` / `Task`
- `Note` / `EmailLog` / `CallLog`
- `User` / `Role` / `Team`
- `CustomField` / `Tag`
- `AuditEvent`

The exact shape depends on your prompt. A B2B SaaS sales team generates different entities than a real-estate brokerage.

### Real example: B2B SaaS sales team

Imagine you submit this spec:

> "We sell mid-market SaaS. Reps work deals through these stages: discovery, demo, proposal, negotiation, closed-won, closed-lost. Each rep has a quota. Deals have a contact and an account. We log activities — calls, emails, meetings — against deals. Managers need a dashboard with pipeline coverage and forecasted close. Admins assign accounts to reps."

StackAlchemist generates:

- `Contact` entity with name, email, phone, title, account_id, owner_id
- `Account` entity with company name, industry, size_band, owner_id, created_at
- `Deal` entity with title, account_id, contact_id, owner_id, amount, stage_id, expected_close_date
- `Pipeline` and `Stage` entities — Pipeline owns ordered Stages with probabilities
- `Activity` entity polymorphic over Deal / Contact / Account — type (call, email, meeting), notes, completed_at
- `Quota` entity per User per period (monthly or quarterly)
- `User` and `Role` entities — the data you enforce access with, server-side, once you write the checks
- CRUD endpoints for every entity (`/api/v1/deals`, `/api/v1/contacts`, `/api/v1/activities`, …). Endpoints like `PATCH /deals/:id/stage` or a pipeline-coverage report can be declared in Advanced Mode; the logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages to build the kanban and forecast views on

That is your CRM's data model and CRUD layer, compile-verified. Audit logging on state changes, the kanban pipeline view, the deal timeline and the forecast math are code you add on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire the email integration.** The repo gives you an `EmailLog` entity with provider-agnostic fields; the Gmail and Outlook OAuth flow and the sync job are yours to write. Register an OAuth app with Google or Microsoft, point the callback at your domain, and write the sync that stores messages as `EmailLog` rows against deals. Reps logging emails directly against deals is the single highest-impact feature you can build first.

2. **Add your first custom field type.** Maybe your reps need a "champion strength" rating on every deal — 1-5 scale, custom UI affordance. The generated `CustomField` entity is a plain table you own: add a rating type, render it in the deal-detail React page, and you are doing the kind of customization that costs $500/seat/month on enterprise CRMs.

## What is not included

StackAlchemist is not Salesforce. We do not host your CRM, do not provide an enterprise app marketplace, and do not ship a managed mobile app. We generate you the code. You deploy and operate it.

We do not include forecasting AI, sales-call transcription, or chatbots out of the box — adding them is a feature for a later generation or hand-coded after delivery. The CRM you get is the data layer and the CRUD surface. The dashboards and the fancy AI sales-coach features are yours to bolt on once you own the foundation.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your CRM in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
