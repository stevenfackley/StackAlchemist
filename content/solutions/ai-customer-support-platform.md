# Generate a full AI Customer Support Platform from a prompt

You describe the kind of support operation you run. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. Your tickets contain your customer voice — the most valuable IP in your company — and you should not be renting access to it. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI customer support platform with:

- **Ticket management** with status workflows, priority levels, tags, and internal notes
- **Multi-channel intake** — email-to-ticket, web contact form, embedded live chat widget, social inbox hooks
- **Assignment and routing rules** based on tags, queue, customer tier, or round-robin
- **SLA tracking** with first-response and resolution-time clocks, breach alerts, business-hours awareness
- **Knowledge base / help center** with articles, categories, search, and public-facing portal
- **Customer history** — full conversation thread, prior tickets, account context on every reply
- **CSAT survey scaffolding** sent after ticket close with rating and free-text feedback capture
- **Macros and canned responses** with variable substitution for agent productivity
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of paying Zendesk per agent forever

**Per-agent pricing punishes you for growing.** Zendesk is $55-115 per agent per month. Freshdesk is per-agent. Help Scout is per-mailbox plus per-user. Twenty agents on Zendesk Suite is over $25,000 a year, forever, and the price goes up every renewal. A StackAlchemist-generated support platform is a one-time generation fee. Hire ten more agents next quarter and your software cost is zero.

**Intercom's AI pricing is deliberately opaque.** Per-resolution AI charges, hidden seat tiers, surprise overages. You cannot model your support cost six months out. A generated platform runs on your infrastructure, with your LLM key, and you can see every line item.

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
- `Contact` entity with organization_id, email, name, role, login credentials
- `Ticket` entity with contact_id, organization_id, subject, status (new, open, pending, resolved, closed), priority, assigned_agent_id, channel (email, chat, web), tags, created_at, closed_at
- `Message` entity with ticket_id, author_type (customer, agent, system), body, is_internal_note, attachments, sent_at
- `SlaPolicy` entity with tier, first_response_minutes, resolution_minutes, business_hours_only
- `SlaClock` entity with ticket_id, policy_id, first_response_due_at, resolution_due_at, breach_flag
- `Macro` entity with name, body_template, applies_tags, owner_team_id
- `KbArticle` entity with category_id, title, slug, body_markdown, published, view_count
- `CsatSurvey` entity with ticket_id, sent_at, rating (1-5), feedback_text, responded_at
- API endpoints: `POST /tickets` (with channel dispatcher), `POST /tickets/:id/messages`, `PATCH /tickets/:id/assign`, `GET /organizations/:id/tickets`, `GET /sla/breaches`, plus public-facing `GET /kb/articles` and `POST /csat/:token`
- Background job: SLA clock evaluator that runs every minute and flags impending or breached tickets

All wired into a Next.js agent console and customer portal, with a .NET backend handling the email-to-ticket parser, the SLA evaluator, and the CSAT dispatcher. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up PostgreSQL, the .NET API, the Next.js frontend, and a MailHog instance for local email testing in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your inbound email and chat widget.** The generated repo includes the email-to-ticket parser and a chat widget endpoint, but you need to point your `support@yourcompany.com` MX or forwarder at the inbound endpoint (Postmark inbound, SendGrid inbound parse, or a plain IMAP poller — the scaffold supports all three). Drop your widget script tag into your product's marketing site or app. You are now ingesting real tickets within an afternoon. No vendor migration plan, no agent retraining on a new UI you did not design.

2. **Add your first AI assist feature on top.** Maybe you want auto-suggested replies based on the customer's prior tickets and the closest KB articles. Or auto-tagging by topic. Or a "summarize this thread" button for an agent picking up a 30-message escalation. The generated code is yours to modify — add an `IReplyDrafter` service that calls Claude with the ticket context and KB embeddings, wire it into the agent console, and ship. This is the part Intercom charges you per-resolution for. You build it once, run it on your LLM key, and the marginal cost is pennies.

## What is not included

StackAlchemist is not a managed help desk. We do not host your support inbox, do not provide the live-chat operator UI as a SaaS, do not run your SLA timers on our infrastructure, and do not maintain ongoing platform operations for you. We generate the code. You deploy and operate it.

We do not include voice support, telephony integration, or full omnichannel social listening out of the gate — those are heavy integrations that depend on your specific provider stack (Twilio, Aircall, Sprout, etc.) and are best added once you own the code. Sentiment analysis, AI auto-classification, and reply drafting are not built in by default because they belong on your LLM key with your model choice, not baked in. Add them in step 2 above.

## Pricing

One-time, per generation:

- **Simple-mode support platform** — $299. Single mailbox, web form intake, ticket workflow, agent console, basic KB, manual assignment.
- **Blueprint-tier** — $599. Multi-channel intake (email + chat + form), SLA tracking with business hours, routing rules, macros, CSAT surveys, public KB portal.
- **Boilerplate-tier** — $999. Multi-brand mailboxes, organization tiers, custom fields, advanced reporting, internal notes, role-based teams, audit log.

No monthly fee. No per-agent charge. You own what you generate.

## Get started

Describe your support operation in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
