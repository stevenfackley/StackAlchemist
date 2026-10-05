# Generate a full AI Marketplace Platform from a prompt

You describe the marketplace you want to run. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase — listings, vendors, buyers, orders, and payout records — verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped two-sided marketplace with:

- **Vendors** — `Vendor`, `VendorProfile` and `StripeConnectAccount` entities with verification and tax-info fields
- **Listings** — `Listing`, `ListingVariant`, `ListingImage`, `Category` and `Tag` entities with search-friendly fields
- **Buyers** — `Buyer` and `BuyerProfile` entities
- **Orders** — `Order` and `OrderLineItem` entities
- **Payouts and commissions** — `Payout`, `PayoutSchedule`, `FeeTier` and `Commission` entities holding gross, fee and net amounts and payout schedules
- **Reviews and disputes** — `Review` and `Dispute` entities with resolution state
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Stripe Connect (vendor onboarding, checkout, the platform-fee split, payouts and webhooks), buyer and vendor sign-in (the Supabase client is preinstalled; the auth flows are yours to write), commission calculation, the dispute queue and admin panel, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of using Sharetribe or building from a template

**Marketplace platforms gatekeep the operator.** Sharetribe, Mirakl, and the rest charge platform fees, gatekeep custom logic behind their managed runtime, and own your relationship with vendors and buyers. The day your fee structure or commission logic needs to be different from theirs, you are stuck.

**Templates skip the hard part.** A two-sided marketplace template gets you to the home page. The hard part — splitting payments cleanly, handling refunds when a vendor disputes a chargeback, calculating multi-rate commissions, surviving the first vendor onboarding flow — is the part templates wave at and skip.

**Generated marketplace code gets you to the hard part faster.** StackAlchemist generates the data model and CRUD layer — vendors, listings, orders, payouts, disputes — and the Compile Guarantee means it builds. The money movement (Stripe Connect webhook handlers, payout calculations, refund routing) is still yours to write. That is exactly where most homemade marketplaces silently corrupt money, so that is where your time should go.

## Who this is for

- **Indie operators** building niche marketplaces (vintage cameras, audio gear, custom services, specialized crafts) who do not want to pay a managed-marketplace platform tax forever.
- **B2B marketplace founders** where the buyer/vendor relationship is more complex than Sharetribe handles cleanly.
- **Agencies** delivering custom marketplace builds to operators who want owned code and a real launch path.
- **Engineering teams** who have decided to build a marketplace and want the boring scaffolding generated so they can focus on the unique mechanics.

## Example entities generated

A typical AI Marketplace Platform generation produces entities like:

- `Vendor` / `VendorProfile` / `StripeConnectAccount`
- `Buyer` / `BuyerProfile`
- `Listing` / `ListingVariant` / `ListingImage`
- `Category` / `Tag`
- `Order` / `OrderLineItem`
- `Payout` / `PayoutSchedule`
- `Review` / `Dispute`
- `FeeTier` / `Commission`

The exact shape depends on your prompt. A vintage-goods marketplace generates different entities than a B2B services platform.

### Real example: Niche services marketplace

Imagine you submit this spec:

> "We connect freelance video editors with podcasters. Editors create profiles with portfolio samples, hourly rate, and turnaround time. Podcasters post jobs with budget and brief. Editors apply. Podcasters select. Money is held in escrow until podcaster confirms delivery, then released to the editor minus our 12% fee. We need dispute resolution if the podcaster claims work wasn't delivered."

StackAlchemist generates:

- `Vendor` (editor) entity with profile, portfolio_links, hourly_rate, turnaround_days, stripe_connect_id
- `Buyer` (podcaster) entity with profile fields
- `Job` entity (the listing variant for this model) with brief, budget, deadline, status
- `Application` entity tying editors to jobs they have applied for
- `Engagement` entity for the active editor-podcaster pairing on a job
- `EscrowHold` entity tracking funds held against a Stripe payment intent
- `Payout` entity with editor_id, gross_amount, platform_fee_amount, net_amount, status
- `Dispute` entity with engagement_id, raised_by, reason, resolution_state
- CRUD endpoints for every entity (`/api/v1/jobs`, `/api/v1/applications`, `/api/v1/disputes`, …). Endpoints like `POST /jobs/:id/apply` or `POST /engagements/:id/release` can be declared in Advanced Mode; the escrow release logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the marketplace's data model and CRUD layer, compile-verified. The separate vendor and buyer flows, Stripe Connect with the platform fee applied at payment-intent capture, escrow state changes with audit logging, and the admin views for disputes and payout reconciliation are code you write on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your Stripe Connect platform account.** The generated repo does not include Stripe; `Vendor` has a `stripe_connect_id` column, and `EscrowHold` and `Payout` hold the money state. Add the express-account onboarding link, a payment intent that applies the 12% platform fee, and webhook handlers for `account.updated` and `payment_intent.succeeded`. Drop in your platform secret, point the webhook at your domain, and onboard a test vendor before you touch real money.

2. **Write your commission model.** The `FeeTier` and `Commission` entities hold the rates; the calculation is yours. Start with a flat percentage. When your real model is "10% on small orders, 7% on orders over $1,000, 5% for verified high-volume vendors", it lives in one `CalculateCommission()` method you own. This is the kind of customization that costs months on a managed marketplace platform.

## What is not included

StackAlchemist is not Sharetribe. We do not host your marketplace, do not provide a managed admin runtime, and do not handle ongoing platform operations like fraud monitoring or vendor support. We generate you the code. You deploy and operate it.

We do not ship native mobile marketplace apps out of the box — you build them on the generated API later. KYC and AML compliance are your responsibility — Stripe Connect handles a lot but the operator still owns the policy decisions. Tax handling for cross-border marketplace sales is not generated and should be added once you understand your jurisdictions.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No platform tax. You own what you generate.

## Get started

Describe your marketplace in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
