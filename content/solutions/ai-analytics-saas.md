# Generate a full AI Analytics SaaS from a prompt

You describe the kind of analytics product you want to ship. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI analytics SaaS with:

- **Event model** — `Event`, `EventSchema` and `EventBatch` entities with the fields you describe (name, user, timestamp, a JSONB properties bag) and CRUD endpoints
- **Dashboards and widgets** — `Dashboard` and `Widget` entities with layout, widget type and query references
- **Saved queries and reports** — `SavedQuery`, `Report` and `ReportSchedule` entities to store the report definitions your team relies on daily
- **Workspaces and members** — `Workspace` and membership entities with a role field (viewer, editor, admin)
- **Alerts** — `Alert`, `AlertCondition` and `AlertChannel` entities holding thresholds and notification channels
- **Usage records** — a `UsageRecord` per workspace, the data a billing integration reads
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** ingest beyond plain CRUD (batching, deduplication, schema validation), the query executor and the widget renderers, sign-in and role enforcement (the Supabase client is preinstalled; the auth flows are yours to write), scheduled email digests, alert delivery, Stripe billing, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of buying an analytics product

**Hosted analytics products turn your data into their data.** Amplitude, Mixpanel, Heap — they all ingest your events into their warehouse, then charge you per event or per MAU forever. Your customers' behavior data lives in someone else's database. A generated analytics SaaS keeps the ingestion path, the warehouse, and the dashboards under your control.

**Embedded analytics in your own product are 10x more valuable than external dashboards.** If you sell a SaaS that needs in-app reporting for your customers (think "show my customers their own usage analytics"), shoving them into a third-party dashboard breaks the product experience. A generated analytics layer lives inside your app, branded as your product.

**Generic analytics products assume a generic event model.** Your real metrics are domain-specific — retention curves for a fitness app are different from retention curves for a B2B SaaS, and the dashboards Amplitude ships are tuned for whatever Amplitude's biggest customer wants. A generated analytics SaaS is tuned for your domain from the prompt up.

## Who this is for

- **SaaS founders** who need internal analytics for their team — KPIs, growth, retention — without paying $1000+/month to Amplitude.
- **Product teams at vertical SaaS** building customer-facing analytics inside their own product (workspace metrics, usage charts, custom report exports for end users).
- **Agencies** delivering branded analytics dashboards to clients who need owned reporting infrastructure.
- **Data teams** at companies that need a self-serve query and dashboarding layer over their own warehouse without committing to a $30k/year BI license.

## Example entities generated

A typical AI Analytics SaaS generation produces entities like:

- `Event` / `EventSchema` / `EventBatch`
- `Workspace` / `User` / `Role`
- `Dashboard` / `Widget` / `WidgetConfig`
- `SavedQuery` / `Report` / `ReportSchedule`
- `Alert` / `AlertCondition` / `AlertChannel`
- `Subscription` (billing) / `UsageRecord`

The exact shape depends on your prompt. A product analytics SaaS generates different entities than a marketing-analytics one.

### Real example: Self-serve product analytics for SaaS teams

Imagine you submit this spec:

> "We build a product analytics SaaS for SaaS founders. Customers send events from their app via a JS snippet or server SDK. Each event has a name, user_id, timestamp, and a JSON properties bag. Customers build dashboards with widgets — line charts, funnels, retention. They invite teammates with viewer / editor roles. We bill on monthly event volume, with a free tier up to 10k events."

StackAlchemist generates:

- `Event` entity with event_name, user_id, workspace_id, occurred_at, properties (JSONB), ingested_at
- `EventSchema` entity with workspace_id, event_name, required_properties — the data your ingest validation checks against
- `Workspace` entity with name, owner_id, plan_tier, monthly_event_quota
- `User` + `WorkspaceMembership` with role enum (viewer, editor, admin)
- `Dashboard` entity with workspace_id, name, layout JSON
- `Widget` entity with dashboard_id, type (timeseries, funnel, table), query_id, position
- `SavedQuery` entity with workspace_id, name, query_spec (typed JSON describing event names, filters, group-by, aggregation)
- `Alert` entity with saved_query_id, threshold_operator, threshold_value, notification_channels
- `UsageRecord` entity tracking monthly event volume per workspace, ready for billing
- CRUD endpoints for every entity (`/api/v1/events`, `/api/v1/dashboards`, `/api/v1/widgets`, …). Endpoints like `POST /events/batch` or `POST /queries/run` can be declared in Advanced Mode; the batching and query execution behind them are yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the data layer and the CRUD surface, compile-verified. Rate limiting on ingest, async batching for high-volume customers, the widget renderer, the query builder and a query executor built on Postgres window functions and CTEs are code you write on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Build the query executor, and decide later if you need a columnar store.** The repo gives you `SavedQuery` with a typed `query_spec` and Dapper repositories over Postgres; the executor that turns a query spec into SQL is yours to write, and it is the heart of the product. Write it against Postgres first: PostgreSQL handles up to ~10M events well with proper indexing. Past that, the right move is to add ClickHouse or BigQuery as the analytical store and keep Postgres for metadata. Keep the executor behind one interface so that swap does not mean rewriting widgets. Do this only when you actually need it — premature columnar adds operational cost.

2. **Build the widgets your customers actually need.** The `Widget` entity carries a type field; the React components that render each type are yours to write. Start with a timeseries and a table, then add the view only you ship — say "weekly active users by signup cohort": a new widget type value, the React component, and the corresponding query generator. That is how analytics SaaS earn loyalty — through the specific widgets only you ship.

## What is not included

StackAlchemist is not Amplitude. We don't host your analytics, don't operate the ingestion pipeline for you, and don't provide a managed columnar warehouse out of the box. We generate you the code. You deploy and operate it.

We don't include session replay, heatmaps, or feature flags in the default generation — those are separate products with their own engineering surface, and bundling them into the generator would force tokens spent on features most analytics buyers don't need. Add them when you actually need them; the generated foundation is the data layer they would sit on top of.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your analytics SaaS in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
