# Generate a full AI Marketplace Platform from a prompt

You describe the marketplace you want to run. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase — listings, vendors, buyers, payouts, the works — verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped two-sided marketplace with:

- **Vendor onboarding** with verification fields, tax info, and Stripe Connect express accounts
- **Listings** with images, variants, categories, and search-friendly fields
- **Buyer accounts** with order history, saved listings, and reviews
- **Cart and checkout** wired to Stripe Connect — buyers pay you, you pay vendors, take your cut automatically
- **Payouts** with configurable schedules (instant, daily, weekly) and clear vendor-side reporting
- **Reviews** in both directions (buyers review vendors, vendors review buyers if your model needs it)
- **Disputes** with a moderation queue for the platform operator
- **Admin panel** for category management, fee tiers, vendor approvals, dispute resolution
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of using Sharetribe or building from a template

**Marketplace platforms gatekeep the operator.** Sharetribe, Mirakl, and the rest charge platform fees, gatekeep custom logic behind their managed runtime, and own your relationship with vendors and buyers. The day your fee structure or commission logic needs to be different from theirs, you are stuck.

**Templates skip the hard part.** A two-sided marketplace template gets you to the home page. The hard part — splitting payments cleanly, handling refunds when a vendor disputes a chargeback, calculating multi-rate commissions, surviving the first vendor onboarding flow — is the part templates wave at and skip.

**Generated marketplace code handles the hard part.** The compile guarantee fires on the parts that are easy to ship broken — Stripe Connect webhook handlers, payout calculations, refund routing — exactly the surface area where most homemade marketplaces silently corrupt money.

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
- `Buyer` (podcaster) entity with profile and saved payment methods
- `Job` entity (the listing variant for this model) with brief, budget, deadline, status
- `Application` entity tying editors to jobs they have applied for
- `Engagement` entity for the active editor-podcaster pairing on a job
- `EscrowHold` entity tracking funds held against a Stripe payment intent
- `Payout` entity with editor_id, gross_amount, platform_fee_amount, net_amount, status
- `Dispute` entity with engagement_id, raised_by, reason, resolution_state
- API endpoints: `POST /jobs`, `POST /jobs/:id/apply`, `POST /engagements/:id/release`, `POST /disputes`
- Stripe Connect integration with platform-fee handling correctly applied at payment-intent capture time
- Admin views for the dispute queue and per-vendor payout reconciliation

All wired into a Next.js frontend (separate vendor and buyer flows) and a .NET backend with audit logging on every escrow state change. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, the Next.js frontend, and a Stripe webhook listener in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your Stripe Connect platform account.** The generated repo includes the platform-account flow, the express-account onboarding link, and the webhook handlers for `account.updated` and `payment_intent.succeeded`. Drop in your platform secret, point the webhook at your domain, and you can onboard a real vendor and run a test transaction within an hour.

2. **Tune your commission model.** Generated code ships with a flat-percentage fee tier. If your real model is "10% on small orders, 7% on orders over $1,000, 5% for verified high-volume vendors" — that is one method change in `CalculateCommission()`. The compile guarantee will catch you if your branching forgets a case. This is the kind of customization that costs months on a managed marketplace platform.

## What is not included

StackAlchemist is not Sharetribe. We do not host your marketplace, do not provide a managed admin runtime, and do not handle ongoing platform operations like fraud monitoring or vendor support. We generate you the code. You deploy and operate it.

We do not ship native mobile marketplace apps out of the box — adding them is a separate generation or hand-built later. KYC and AML compliance are your responsibility — Stripe Connect handles a lot but the operator still owns the policy decisions. Tax handling for cross-border marketplace sales is not generated by default and should be added once you understand your jurisdictions.

## Pricing

One-time, per generation:

- **Simple-mode marketplace** — $299. Single category, basic listings, buyer/vendor flows, Stripe Connect with flat commission.
- **Blueprint-tier** — $599. Multi-category, search and filters, dispute workflow, configurable commission tiers, vendor verification flow.
- **Boilerplate-tier** — $999. Multi-region payouts, advanced fee structures, escrow workflow, dispute SLA tracking, admin reporting suite.

No monthly fee. No platform tax. You own what you generate.

## Get started

Describe your marketplace in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
