# Generate a full AI E-commerce Platform from a prompt

You describe the kind of storefront you want. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI e-commerce platform with:

- **Product catalog** with variants, inventory, pricing tiers, and bulk-import tooling
- **Cart and checkout** wired to Stripe Checkout with webhook handling
- **Customer accounts** backed by Supabase auth — login, register, password reset
- **Order management** with order history, fulfillment status, and admin dashboard
- **Reviews and ratings** with moderation tools
- **Promo codes** and discount engines (percentage, fixed, BOGO)
- **Admin dashboard** for inventory, orders, customers, reporting
- **Search and filters** over the catalog, indexed for fast retrieval
- **Email receipts** and transactional emails (send-grid or resend)
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of buying a template

**Templates lock you into someone else's opinions.** A generated codebase starts from your prompt, fits your domain, uses your naming conventions, and has no upstream maintainer forcing updates on you. You own it outright.

**Subscription platforms own your customers.** Shopify, BigCommerce, Squarespace — they all host your customers and your data. A StackAlchemist-generated e-commerce platform runs on your infrastructure. Your customers are yours. Your data is yours.

**Headless commerce is expensive to wire.** Connecting a headless storefront to a headless backend and a headless payment processor is days of glue code. StackAlchemist wires it in one pass.

## Who this is for

- **Indie founders** who want to own the code for their storefront rather than rent a platform.
- **Agencies** delivering bespoke e-commerce sites to clients — generate in 12 minutes, customize for the client, ship in a day.
- **Developers** who need a starting point for a specialized e-commerce product (niche verticals, B2B commerce, subscription boxes, DTC brands with custom fulfillment).
- **Engineering teams** who want to evaluate a compile-verified starter before building on top of it.

## Example entities generated

A typical AI e-commerce platform generation produces entities like:

- `Product` / `Variant` / `InventoryRecord`
- `Cart` / `CartItem`
- `Order` / `LineItem` / `Fulfillment`
- `Customer` / `Address`
- `Review` / `ModerationFlag`
- `PromoCode` / `Discount`
- `Category` / `Tag`

The exact shape depends on your prompt. A DTC apparel brand generates different entities than a wholesale beverage catalog.

## What is not included

StackAlchemist is not Shopify. We do not host your storefront, do not provide payment processing directly (you plug in Stripe or similar), and do not provide ongoing platform operations. We generate you the code. You deploy and operate it.

For 90% of e-commerce businesses, this is what you actually want — ownership of the stack, no platform fees, no vendor risk. For the 10% that want everything managed, use Shopify.

## Pricing

One-time, per generation:

- **Simple-mode e-commerce** — $299. Single storefront, standard catalog, Stripe billing, admin dashboard.
- **Blueprint-tier** — $599. Includes subscription products, gift cards, multi-currency, deeper analytics.
- **Boilerplate-tier** — $999. Multi-storefront, wholesale pricing, advanced fulfillment, custom reports.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your e-commerce platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
