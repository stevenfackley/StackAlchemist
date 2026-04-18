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

### Real example: Outdoor gear micro-brand

Imagine you submit this spec:

> "We sell outdoor equipment. Products are tents, backpacks, and climbing gear. Each product has colors and sizes as variants. We track inventory per variant. Customers can add items to cart, apply a discount code, and check out through Stripe. After purchase, we email them a receipt and an invoice link. We need an admin panel to manage products, see orders, and adjust inventory."

StackAlchemist generates:

- `Product` entity with name, description, images, base price, and status (draft, active, archived)
- `Variant` entity with product_id, color, size, SKU, and pricing override (for size-based markups)
- `InventoryRecord` entity with variant_id, quantity_on_hand, quantity_reserved
- `Cart` and `CartItem` entities tied to session or authenticated user
- `Order` entity with customer_id, total_amount, status (pending, paid, fulfilled), and created_at
- `LineItem` entity with order_id, variant_id, quantity, unit_price (locked at purchase time)
- `Fulfillment` entity with order_id, status (pending, shipped, delivered), tracking_number
- `Discount` entity with code, amount_or_percentage, usage_count, max_uses
- `Review` entity with product_id, customer_id, rating, text, verified_purchase flag
- API endpoints: `POST /cart/items`, `DELETE /cart/items/:id`, `POST /checkout`, `GET /orders/:id`, plus admin endpoints for inventory and analytics

All wired into a Next.js storefront and a .NET backend with webhook handling for Stripe Checkout session completion. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe test mode to your environment.** The generated repo includes the Stripe integration scaffold and webhook handlers, but you need to drop in your own test API keys. Set `STRIPE_API_KEY` and `STRIPE_WEBHOOK_SECRET` in your `.env.local`, run the migrations (which StackAlchemist scaffolds), and test a checkout. You are now able to iterate on the customer checkout flow without touching payment infrastructure. No compliance burden yet.

2. **Add your first custom business logic.** Maybe you have a rule: bulk orders (5+ items) get a 10% discount. Or you need to reserve inventory for 10 minutes at checkout before the customer hits Stripe, so oversells don't happen. The generated code is not a black box — it is yours to modify. You add a `CalculateDiscount()` method to your order service, or you add an inventory-hold entity with a TTL background job. This is not a template hack — it is the product working as designed.

## What is not included

StackAlchemist is not Shopify. We do not host your storefront, do not provide payment processing directly (you plug in Stripe or similar), and do not provide ongoing platform operations. We generate you the code. You deploy and operate it.

Payment compliance (PCI DSS) is your responsibility — but using Stripe Checkout offloads the hard part. We do not handle taxes, duties, or shipping integrations natively — you wire those in once you own the code. We do not include multi-currency support unless your spec asks for it, because building it costs tokens and adds complexity most vendors don't need out of the gate.

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
