# Generate a full AI Job Board from a prompt

You describe the niche you want to serve. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase for a niche job board, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI job board with:

- **Job listings** — `JobListing`, `Category` and `Location` entities with descriptions, salary bands, remote/hybrid/onsite tags and expiry dates
- **Employers** — `Employer`, `EmployerAccount` and `EmployerBilling` entities with company profiles and verification fields
- **Applicants and applications** — `Applicant`, `Application` and `Resume` entities with application status
- **Posting credits and subscriptions** — `Subscription`, `Credit` and `Invoice` entities for pay-per-post, subscription or credit-pack models
- **Featured slots** — `FeaturedSlot` and `SponsoredPlacement` entities for timed promotion and slot inventory
- **Search alerts** — `SearchAlert` entities with keyword, filter and frequency fields
- **Moderation** — `AbuseReport` and `ModerationAction` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** employer billing through Stripe, employer and applicant sign-in (the Supabase client is preinstalled; the auth flows are yours to write), resume file storage, search and filters, featured-slot rotation, the search-alert email job, the admin panel, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of using a job-board template

**Job-board templates underbuild the parts that monetize.** The home page and listing template are the easy 10%. The parts that make a job board actually a business — billing, featured-slot inventory, employer-account self-serve, abuse moderation — are skipped or hand-waved. You end up rewriting half the code anyway. A generated board starts with those parts modeled in the schema, so you write the business logic on real tables instead.

**Generic boards do not understand your niche.** Every successful niche job board — RemoteOK, We Work Remotely, vertical-specific boards in healthcare, climate, dev tooling — won by encoding niche-specific filters and ranking signals into the product. A generic template flattens those decisions. A generated board can encode them from your prompt.

**Generated job-board code is yours.** No platform locking your employer relationships, no vendor changing their fee structure, no subscription that costs more than your top-paying customer. The board is yours, the customers are yours, the data is yours.

## Who this is for

- **Niche-board operators** building boards for specific verticals (climate, AI, security, design, regional markets) who want owned code from day one.
- **Community runners** with an existing audience (newsletter, Discord, subreddit) who want to add a job board as a real revenue line, not a $50/month rented page.
- **Agencies** delivering custom job boards for industry associations or membership communities.
- **Engineering teams** at companies running internal job boards (referrals, alumni, partner-facing) who want a real product, not a Notion page.

## Example entities generated

A typical AI Job Board generation produces entities like:

- `JobListing` / `Category` / `Location`
- `Employer` / `EmployerAccount` / `EmployerBilling`
- `Applicant` / `Application` / `Resume`
- `Subscription` (employer) / `Credit` / `Invoice`
- `FeaturedSlot` / `SponsoredPlacement`
- `SearchAlert` (applicant)
- `AbuseReport` / `ModerationAction`

The exact shape depends on your prompt. A remote-only dev jobs board generates different entities than an in-person union trades board.

### Real example: Climate-tech niche board

Imagine you submit this spec:

> "We run a niche board for climate-tech jobs. Employers post listings, paying $99 per post with a 30-day expiration. Premium employers can subscribe at $299/month for unlimited posts and featured-slot rotation. Each listing has a category (engineering, science, ops, policy), a remote/hybrid/onsite tag, and a salary band. Applicants can save searches and get email alerts when matching listings are posted. We need an admin panel to handle abuse reports and refund mistaken postings."

StackAlchemist generates:

- `JobListing` entity with title, description (markdown), employer_id, category_id, location, remote_type, salary_min, salary_max, posted_at, expires_at
- `Employer` entity with company_name, website, verified_at, billing_method
- `EmployerSubscription` entity with tier, status, current_period_end, posts_used_this_period
- `PostCredit` entity for the pay-per-post path — purchased_at, used_at, listing_id (when used)
- `FeaturedSlot` entity with position, slot_count_global cap, and the employer reference your rotation logic will use
- `Applicant` entity with email, resume_url, cover_letter_url
- `Application` entity tying applicants to listings with status (submitted, reviewed, archived)
- `SearchAlert` entity with applicant_id, keyword, category_filter, location_filter, frequency
- `AbuseReport` entity with listing_id, reporter, reason, resolution_state
- CRUD endpoints for every entity (`/api/v1/employers`, `/api/v1/applications`, `/api/v1/searchalerts`, …). A filtered `GET /search` endpoint can be declared in Advanced Mode; the search query behind it is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the job board's data model and CRUD layer, compile-verified. The public site, the employer dashboard, the admin panel, Stripe for pay-per-post and subscriptions, the webhook that reconciles credit and subscription state, and audit logging on moderation are code you add on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Seed real listings before you launch.** A job board with no listings is dead on arrival. Use the generated `JobListing` endpoints, or a short import script, to create 30–100 quality seed listings (with permission from the originating sources or by surfacing existing public listings as discovery).

2. **Build the search-alert email job.** The repo gives you `SearchAlert` with keyword, filters and frequency; the job that matches new listings against saved alerts and sends the notification email is yours to write. Add a scheduled job, drop in your transactional-email provider's API key (Resend, SendGrid, Postmark), and the loop runs end to end. Search alerts are the single feature that drives applicant retention on a niche board, so build this one early.

## What is not included

StackAlchemist is not LinkedIn. We do not host your job board, do not provide a managed applicant-tracking system for employers, and do not run the moderation operations for you. We generate you the code. You deploy and operate it.

We do not include AI resume parsing or skill-matching out of the box — those features are best added once you have real applicant data and know what shape they need to take. Cross-board listing aggregation is not generated — building a scraping/syndication layer is a different product.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your job board niche in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
