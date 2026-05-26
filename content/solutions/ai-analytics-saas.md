# Generate a full AI Analytics SaaS from a prompt

You describe the kind of analytics product you want to ship. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI analytics SaaS with:

- **Event ingestion** API with batching, deduplication, and a strict event schema you control
- **Dashboards** with configurable widgets — timeseries, tables, funnels, retention cohorts
- **Custom report builder** with saved queries, share links, and CSV / JSON export
- **Multi-tenant workspaces** with role-based access — viewer, editor, admin
- **Scheduled email digests** for the saved reports your team relies on daily
- **Alerting** on threshold conditions (KPI dropped X%, value above Y) with email + webhook delivery
- **Billing scaffolding** via Stripe — usage-based metered or per-seat
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- `EventSchema` entity with workspace_id, event_name, required_properties — used to reject malformed events at ingest
- `Workspace` entity with name, owner_id, plan_tier, monthly_event_quota
- `User` + `WorkspaceMembership` with role enum (viewer, editor, admin)
- `Dashboard` entity with workspace_id, name, layout JSON
- `Widget` entity with dashboard_id, type (timeseries, funnel, table), query_id, position
- `SavedQuery` entity with workspace_id, name, query_spec (typed JSON describing event names, filters, group-by, aggregation)
- `Alert` entity with saved_query_id, threshold_operator, threshold_value, notification_channels
- `UsageRecord` entity tracking monthly event volume per workspace for the billing engine
- API endpoints: `POST /events`, `POST /events/batch`, `GET /dashboards/:id`, `POST /queries/run`, plus admin endpoints
- A Next.js dashboard UI with the widget renderer and query builder, and a server-side query executor backed by Postgres window functions and CTEs

All wired into a Next.js frontend and a .NET backend with rate limiting on the ingest endpoint and async batching for high-volume customers. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Decide if you need a columnar store before you scale past 10M events.** PostgreSQL handles up to ~10M events well with proper indexing. Past that, the right move is to add ClickHouse or BigQuery as the analytical store and keep Postgres for metadata. The generated repo's query layer is abstracted enough to swap the analytical backend without rewriting widgets. Do this only when you actually need it — premature columnar adds operational cost.

2. **Add your first custom widget type.** Maybe your customers need a "weekly active users by signup cohort" view that isn't one of the four widget types we ship. Add a new `WidgetType` enum value, write the widget React component, write the corresponding query generator, and your customers have a custom widget that didn't exist on day one. The generator's widget system is open for extension, which is how analytics SaaS earn loyalty — through the specific widgets only you ship.

## What is not included

StackAlchemist is not Amplitude. We don't host your analytics, don't operate the ingestion pipeline for you, and don't provide a managed columnar warehouse out of the box. We generate you the code. You deploy and operate it.

We don't include session replay, heatmaps, or feature flags in the default generation — those are separate products with their own engineering surface, and bundling them into the generator would force tokens spent on features most analytics buyers don't need. Add them when you actually need them; the generated foundation is the data layer they would sit on top of.

## Pricing

One-time, per generation:

- **Simple-mode analytics SaaS** — $299. Single workspace, basic dashboards, event ingestion, manual report exports.
- **Blueprint-tier** — $599. Multi-tenant workspaces, alerts, scheduled email digests, RBAC, usage-based billing scaffolding.
- **Boilerplate-tier** — $999. Custom widget extensibility, columnar-store integration scaffolding, embedded-analytics SDK starter, per-customer branded dashboards.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your analytics SaaS in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
