# Generate a full AI Customer Support Platform from a prompt

You describe the kind of support operation you run. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. Your tickets contain your customer voice — the most valuable IP in your company — and you should not be renting access to it. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI customer support platform with:

- **Tickets and messages** — `Ticket`, `Message` and `Attachment` entities with status, priority, channel, tags and an internal-note flag
- **Customers and organizations** — `Customer`, `Organization` and `Contact` entities with tier and account-manager fields, so every reply can show account context
- **Agents and teams** — `Agent`, `Team` and `Role` entities for assignment
- **SLA policies** — `SlaPolicy`, `SlaClock` and `BusinessHours` entities holding first-response and resolution targets and due times
- **Knowledge base** — `KbArticle` and `KbCategory` entities with slug, markdown body and a published flag
- **Macros and CSAT** — `Macro`, `CannedResponse`, `CsatSurvey` and `CsatResponse` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** email-to-ticket, chat and web-form intake, routing rules, the SLA clock evaluator and breach alerts, CSAT sending, macro variable substitution, the public help-center search, agent and customer sign-in (the Supabase client is preinstalled; the auth flows are yours to write), and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of paying Zendesk per agent forever

**Per-agent pricing punishes you for growing.** Zendesk is $55-115 per agent per month. Freshdesk is per-agent. Help Scout is per-mailbox plus per-user. Twenty agents on Zendesk Suite is over $25,000 a year, forever, and the price goes up every renewal. A StackAlchemist-generated support platform is a one-time generation fee. Hire ten more agents next quarter and your software cost is zero.

**Intercom's AI pricing is deliberately opaque.** Per-resolution AI charges, hidden seat tiers, surprise overages. You cannot model your support cost six months out. A generated platform runs on your infrastructure, any AI you add runs on your own LLM key, and you can see every line item.

**Your tickets are your customer voice.** Every support conversation is product feedback, churn signal, and roadmap input. Letting a SaaS vendor host that data — and charge you to query it — is the wrong direction. Own the database. Run the analytics. Train your own internal models on it if you want.

## Who this is for

- **SaaS companies outgrowing a shared inbox** who need real ticket tracking, SLAs, and assignment but refuse to pay $1,000/month for the privilege.
- **B2B vendors with white-glove support** where account managers need full customer history, custom fields, and internal collaboration on every ticket.
- **E-commerce ops teams** doing SLA-tracked email triage across orders, returns, and shipping issues, where Zendesk's per-agent math does not work.
- **Agencies** building bespoke support portals for clients in regulated verticals where data residency and audit trails matter.

## Example entities generated

A typical AI customer support platform generation produces entities like:

- `Ticket` / `Message` / `Attachment`
- `Customer` / `Organization` / `Contact`
- `Agent` / `Team` / `Role`
- `Macro` / `CannedResponse` / `Tag`
- `SlaPolicy` / `SlaClock` / `BusinessHours`
- `KbArticle` / `KbCategory`
- `CsatSurvey` / `CsatResponse`

The exact shape depends on your prompt. A B2B SaaS with named-account support generates different entities than a high-volume e-commerce help desk.

### Real example: B2B SaaS with account-managed support

Imagine you submit this spec:

> "We sell project management software to mid-market companies. Each customer organization has a primary account manager and tier (Standard, Pro, Enterprise). Tickets come in via support email, in-app chat, and a web form. Enterprise tickets get a 1-hour first-response SLA during business hours; Pro gets 4 hours; Standard gets 24 hours. Tickets auto-route to the org's assigned account manager when one exists, else to a queue. Agents need to see prior tickets, organization tier, and internal notes. After close, we send a CSAT survey. We need a public knowledge base with category navigation and search."

StackAlchemist generates:

- `Organization` entity with name, tier, account_manager_id, custom fields, created_at
- `Contact` entity with organization_id, email, name, role
- `Ticket` entity with contact_id, organization_id, subject, status (new, open, pending, resolved, closed), priority, assigned_agent_id, channel (email, chat, web), tags, created_at, closed_at
- `Message` entity with ticket_id, author_type (customer, agent, system), body, is_internal_note, attachments, sent_at
- `SlaPolicy` entity with tier, first_response_minutes, resolution_minutes, business_hours_only
- `SlaClock` entity with ticket_id, policy_id, first_response_due_at, resolution_due_at, breach_flag
- `Macro` entity with name, body_template, applies_tags, owner_team_id
- `KbArticle` entity with category_id, title, slug, body_markdown, published, view_count
- `CsatSurvey` entity with ticket_id, sent_at, rating (1-5), feedback_text, responded_at
- CRUD endpoints for every entity (`/api/v1/tickets`, `/api/v1/messages`, `/api/v1/organizations`, …). Endpoints like `PATCH /tickets/:id/assign`, `GET /sla/breaches` or a public `POST /csat/:token` can be declared in Advanced Mode; the routing, SLA and survey logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the support desk's data model and CRUD layer, compile-verified. The agent console, the customer portal, the email-to-ticket parser, the SLA clock evaluator (a job that runs every minute and flags tickets about to breach) and the CSAT dispatcher are code you add on top. Docker Compose spins up PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your inbound email and chat widget.** The repo has no intake channels yet; it gives you `Ticket` and `Message` with a channel field and CRUD endpoints. Pick an inbound path — Postmark inbound, SendGrid inbound parse, or a plain IMAP poller — and write the handler that turns an email into a `Ticket` and its first `Message`. Point your `support@yourcompany.com` MX or forwarder at it. The chat widget and the web form follow the same pattern: an endpoint that creates the ticket, and a script tag in your product's marketing site or app. No vendor migration plan, no agent retraining on a new UI you did not design.

2. **Add your first AI assist feature on top.** Maybe you want auto-suggested replies based on the customer's prior tickets and the closest KB articles. Or auto-tagging by topic. Or a "summarize this thread" button for an agent picking up a 30-message escalation. The generated code is yours to modify — add an `IReplyDrafter` service that calls Claude with the ticket context and KB embeddings, wire it into the agent console, and ship. This is the part Intercom charges you per-resolution for. You build it once, run it on your LLM key, and the marginal cost is pennies.

## What is not included

StackAlchemist is not a managed help desk. We do not host your support inbox, do not provide the live-chat operator UI as a SaaS, do not run your SLA timers on our infrastructure, and do not maintain ongoing platform operations for you. We generate the code. You deploy and operate it.

We do not include voice support, telephony integration, or full omnichannel social listening out of the gate — those are heavy integrations that depend on your specific provider stack (Twilio, Aircall, Sprout, etc.) and are best added once you own the code. Sentiment analysis, AI auto-classification, and reply drafting are not built in by default because they belong on your LLM key with your model choice, not baked in. Add them in step 2 above.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-agent charge. You own what you generate.

## Get started

Describe your support operation in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
