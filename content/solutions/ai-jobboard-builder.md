# Generate a full AI Job Board from a prompt

You describe the niche you want to serve. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase for a niche job board, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI job board with:

- **Job listings** with rich descriptions, salary bands, location, remote/hybrid/onsite tagging, and category routing
- **Employer accounts** with company profiles, verified badges, and posting credits
- **Applicant flow** with resume upload, structured questionnaires, and one-click apply
- **Search and filters** indexed for fast retrieval — keyword, category, salary, location, remote
- **Featured / sponsored slots** with timed promotion and clear inventory management
- **Employer billing** via Stripe — pay-per-post, subscription, or credit-pack pricing models
- **Email alerts** for applicants subscribed to keyword or category searches
- **Admin panel** for moderating listings, handling abuse reports, and adjusting featured-slot inventory
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of using a job-board template

**Job-board templates underbuild the parts that monetize.** The home page and listing template are the easy 10%. The parts that make a job board actually a business — billing, featured-slot inventory, employer-account self-serve, abuse moderation — are skipped or hand-waved. You end up rewriting half the code anyway.

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
- `FeaturedSlot` entity with rotation logic, slot_count_global cap, and per-employer fairness for the subscription tier
- `Applicant` entity with email, resume_url, cover_letter_url
- `Application` entity tying applicants to listings with status (submitted, reviewed, archived)
- `SearchAlert` entity with applicant_id, keyword, category_filter, location_filter, frequency
- `AbuseReport` entity with listing_id, reporter, reason, resolution_state
- API endpoints: `POST /listings` (employer auth), `POST /applications`, `GET /search` (with filters), plus admin endpoints for moderation
- A Next.js public site (search + listing pages), an employer dashboard, and an admin panel
- Stripe integration for both pay-per-post and subscription flows, with webhook handlers reconciling credit/subscription state

All wired into a Next.js frontend and a .NET backend with audit logging on listing edits and moderation actions. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, the Next.js frontend, and a Stripe webhook listener in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Seed real listings before you launch.** A job board with no listings is dead on arrival. Use the generated admin panel to manually create 30–100 quality seed listings (with permission from the originating sources or by surfacing existing public listings as discovery). The generator includes a CSV import path on the admin panel that does not exist on most templates.

2. **Wire the search-alert email job.** The generated repo includes a scheduled background job for matching new listings against saved alerts and sending notification emails. Drop in your transactional-email provider's API key (Resend, SendGrid, Postmark) and the loop runs end to end. Search alerts are the single feature that drives applicant retention on a niche board, and most templates ship them broken or not at all.

## What is not included

StackAlchemist is not LinkedIn. We do not host your job board, do not provide a managed applicant-tracking system for employers, and do not run the moderation operations for you. We generate you the code. You deploy and operate it.

We do not include AI resume parsing or skill-matching out of the box — those features are best added once you have real applicant data and know what shape they need to take. Cross-board listing aggregation is not generated by default — building a scraping/syndication layer is a different product.

## Pricing

One-time, per generation:

- **Simple-mode job board** — $299. Single category, pay-per-post, basic search, applicant flow, admin panel.
- **Blueprint-tier** — $599. Multi-category, search alerts, employer subscriptions, featured slots, abuse moderation queue.
- **Boilerplate-tier** — $999. Multi-region, advanced featured-slot rotation, employer-side reporting, applicant-side saved-search bundles, custom listing fields.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your job board niche in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
