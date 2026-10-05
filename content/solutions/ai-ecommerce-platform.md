# Generate a full AI E-commerce Platform from a prompt

You describe the kind of storefront you want. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI e-commerce platform with:

- **Product catalog** — `Product`, `Variant`, `InventoryRecord`, `Category` and `Tag` entities with pricing, SKUs and stock fields
- **Carts and orders** — `Cart`, `CartItem`, `Order`, `LineItem` and `Fulfillment` entities with order status and tracking numbers
- **Customers** — `Customer` and `Address` entities
- **Reviews** — `Review` and `ModerationFlag` entities
- **Promo codes** — `PromoCode` and `Discount` entities with code, amount or percentage, and usage limits
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Stripe Checkout and its webhook, customer accounts (the Supabase client is preinstalled; login, register and password reset are yours to write), discount and inventory rules, catalog search, receipt and transactional emails, the admin dashboard, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of buying a template

**Templates lock you into someone else's opinions.** A generated codebase starts from your prompt, fits your domain, uses your naming conventions, and has no upstream maintainer forcing updates on you. You own it outright.

**Subscription platforms own your customers.** Shopify, BigCommerce, Squarespace — they all host your customers and your data. A StackAlchemist-generated e-commerce platform runs on your infrastructure. Your customers are yours. Your data is yours.

**Headless commerce is expensive to wire.** Connecting a headless storefront to a headless backend and a headless payment processor is days of glue code. StackAlchemist writes the storefront-to-backend half in one pass — the schema, the CRUD API and a typed client the storefront calls — so your glue time goes into payments, not plumbing.

## Who this is for

- **Indie founders** who want to own the code for their storefront rather than rent a platform.
- **Agencies** delivering bespoke e-commerce sites to clients — generate the foundation, then spend the build budget on the client's checkout and customization.
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

### Real example: Outdoor gear micro-brand

Imagine you submit this spec:

> "We sell outdoor equipment. Products are tents, backpacks, and climbing gear. Each product has colors and sizes as variants. We track inventory per variant. Customers can add items to cart, apply a discount code, and check out through Stripe. After purchase, we email them a receipt and an invoice link. We need an admin panel to manage products, see orders, and adjust inventory."

StackAlchemist generates:

- `Product` entity with name, description, images, base price, and status (draft, active, archived)
- `Variant` entity with product_id, color, size, SKU, and pricing override (for size-based markups)
- `InventoryRecord` entity with variant_id, quantity_on_hand, quantity_reserved
- `Cart` and `CartItem` entities tied to a session or customer id
- `Order` entity with customer_id, total_amount, status (pending, paid, fulfilled), and created_at
- `LineItem` entity with order_id, variant_id, quantity, unit_price (locked at purchase time)
- `Fulfillment` entity with order_id, status (pending, shipped, delivered), tracking_number
- `Discount` entity with code, amount_or_percentage, usage_count, max_uses
- `Review` entity with product_id, customer_id, rating, text, verified_purchase flag
- CRUD endpoints for every entity (`/api/v1/products`, `/api/v1/carts`, `/api/v1/orders`, …). A `POST /checkout` endpoint can be declared in Advanced Mode; the Stripe call behind it is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is your store's data model and CRUD layer, compile-verified. Stripe Checkout, the webhook that marks an order paid when the Checkout session completes, the receipt email and the admin panel are code you add on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe in test mode.** The generated repo does not include Stripe; it gives you `Order`, `LineItem` and `Discount` with the fields a checkout needs. Add the Stripe SDK, a `POST /checkout` endpoint that creates a Checkout Session from the cart, and a webhook endpoint that marks the order paid on `checkout.session.completed`. Set `STRIPE_API_KEY` and `STRIPE_WEBHOOK_SECRET` in your `.env.local`, start the stack with `docker compose up` (the generated migration runs on first start), and test a checkout. Stripe Checkout keeps card data off your servers, so there is no compliance burden yet.

2. **Add your first custom business logic.** Maybe you have a rule: bulk orders (5+ items) get a 10% discount. Or you need to reserve inventory for 10 minutes at checkout before the customer hits Stripe, so oversells don't happen. The generated code is not a black box — it is yours to modify. You add a `CalculateDiscount()` method on top of the generated order repository, or you add an inventory-hold entity with a TTL background job. This is not a template hack — it is the product working as designed.

## What is not included

StackAlchemist is not Shopify. We do not host your storefront, do not provide payment processing directly (you plug in Stripe or similar), and do not provide ongoing platform operations. We generate you the code. You deploy and operate it.

Payment compliance (PCI DSS) is your responsibility — but using Stripe Checkout offloads the hard part. We do not handle taxes, duties, or shipping integrations natively — you wire those in once you own the code. We do not include multi-currency support; most vendors don't need it out of the gate, and it is yours to add when you do.

For 90% of e-commerce businesses, this is what you actually want — ownership of the stack, no platform fees, no vendor risk. For the 10% that want everything managed, use Shopify.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your e-commerce platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
