# Generate a full AI CRM from a prompt

You describe the kind of CRM your team actually needs. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI CRM with:

- **Contacts and accounts** with custom fields, tagging, and deduplication
- **Deal pipelines** with configurable stages, drag-and-drop kanban, and forecasting
- **Activities and tasks** with reminders, assignees, and due dates
- **Email integration scaffolding** for Gmail and Outlook (OAuth-based, two-way sync ready)
- **Notes and call logs** attached to contacts, accounts, or deals
- **Reports and dashboards** for pipeline coverage, win rate, and rep performance
- **Role-based access control** so reps see their book, managers see the team, admins see everything
- **Audit trail** on every contact, deal, and stage change — required for any serious sales org
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- `User` and `Role` entities with RBAC enforced server-side, not just hidden in the UI
- API endpoints: `POST /deals`, `PATCH /deals/:id/stage`, `POST /activities`, plus reporting endpoints for pipeline coverage and forecast
- A Next.js dashboard with kanban pipeline view, deal-detail page with activity timeline, and manager forecasting view

All wired into a Next.js frontend and a .NET backend with audit logging on every state change. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire the email integration.** The generated repo includes OAuth scaffolding for Gmail and Outlook plus a `EmailLog` entity with provider-agnostic fields. Drop in your client ID and secret, point the OAuth callback at your domain, and reps can log emails directly against deals. This is the single highest-impact thing you can finish in the first hour.

2. **Add your first custom field type.** Maybe your reps need a "champion strength" rating on every deal — 1-5 scale, custom UI affordance. The generated `CustomField` entity supports adding new types in code without DB migrations for every new field. Add a `RatingField` subtype, render it in the deal-detail React page, and you are doing the kind of customization that costs $500/seat/month on enterprise CRMs.

## What is not included

StackAlchemist is not Salesforce. We do not host your CRM, do not provide an enterprise app marketplace, and do not ship a managed mobile app. We generate you the code. You deploy and operate it.

We do not include forecasting AI, sales-call transcription, or chatbots out of the box — adding them is a feature for a later generation or hand-coded after delivery. The CRM you get is the data layer, the operations layer, and the dashboards. The fancy AI sales-coach features are yours to bolt on once you own the foundation.

## Pricing

One-time, per generation:

- **Simple-mode CRM** — $299. Single team, single pipeline, contacts/deals/activities, basic reporting.
- **Blueprint-tier** — $599. Multi-team, multiple pipelines, custom fields, email integration scaffolding, RBAC.
- **Boilerplate-tier** — $999. Forecasting, advanced reporting, audit trail UI, multi-currency, custom field types extensible in code.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your CRM in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
