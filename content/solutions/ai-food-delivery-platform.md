# Generate a full AI Food Delivery Platform from a prompt

You describe the kind of food delivery operation you want — single restaurant, multi-restaurant marketplace, ghost-kitchen network, regional cooperative. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI food delivery platform with:

- **Restaurants and menus** — `Restaurant`, `Menu`, `MenuCategory`, `MenuItem`, `ItemModifierGroup` and `ItemModifier` entities with prices, modifiers (size, toppings, options) and per-item availability windows
- **Carts and orders** — `Cart`, `CartItem`, `Order`, `OrderItem` and `OrderStatusEvent` entities with special instructions and the status lifecycle (placed, accepted, preparing, ready, in-transit, delivered)
- **Customers** — `Customer` and `DeliveryAddress` entities with saved addresses and delivery instructions
- **Drivers** — `Driver`, `DriverAssignment` and `DriverLocation` entities with status and current location
- **Delivery zones** — a `DeliveryZone` entity with zone geometry, delivery fee and minimum order amount
- **Payouts** — a `Payout` entity with gross sales, platform fee, net payout and a `stripe_transfer_id` column
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Stripe Checkout and Stripe Connect payouts, customer, driver and operator sign-in (the Supabase client is preinstalled; the auth flows are yours to write), the dispatch logic, zone pricing rules, the restaurant operator and admin dashboards, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of paying DoorDash 30% commission

**DoorDash, Uber Eats, and Grubhub take 15-30% of every order and own your customer.** When a customer orders through DoorDash, DoorDash has the relationship. They have the email, the order history, the marketing channel. You are a faceless kitchen fulfilling a request. A generated platform flips that. You own the customer, the data, and the margin.

**Restaurant SaaS platforms still lock you into their stack.** Toast, Square for Restaurants, ChowNow — they all charge monthly fees, gate features behind tiers, and own your customer data on their servers. A StackAlchemist-generated delivery platform runs on your infrastructure. Your customers are yours. Your payouts are yours. No platform fee.

**Hiring an agency to build this is $80k-$200k and six months.** A multi-restaurant marketplace with driver dispatch, Stripe Connect payouts, and a real-time order queue is not a weekend project for an agency. StackAlchemist generates the compile-verified bones — the data model, the CRUD API and the typed client — in 12 minutes for $599-$999. You spend the saved budget on dispatch, payments, marketing, and actual restaurants.

## Who this is for

- **Single-restaurant owners** tired of paying 30% commission and wanting their own ordering site with delivery — a generated platform pays for itself after the first month.
- **Ghost-kitchen operators** running multiple brands out of one kitchen who need a marketplace storefront for their own concepts without renting one from DoorDash.
- **Regional delivery cooperatives** — a group of local restaurants in a small city pooling resources to fight back against the national platforms with a shared, locally-owned delivery network.
- **Developers and agencies** building bespoke food-ordering products for restaurant clients who want ownership instead of a SaaS subscription.

## Example entities generated

A typical AI food delivery platform generation produces entities like:

- `Restaurant` / `RestaurantHours` / `RestaurantSettings`
- `Menu` / `MenuCategory` / `MenuItem` / `ItemModifierGroup` / `ItemModifier`
- `Cart` / `CartItem` / `CartItemModifierSelection`
- `Order` / `OrderItem` / `OrderStatusEvent`
- `Customer` / `DeliveryAddress`
- `Driver` / `DriverAssignment` / `DriverLocation`
- `DeliveryZone` / `Payout`

The exact shape depends on your prompt. A single-restaurant ordering site generates a flatter schema than a 50-restaurant marketplace with driver dispatch.

### Real example: Regional delivery cooperative competing with DoorDash

Imagine you submit this spec:

> "We are a co-op of 12 local restaurants in a mid-sized city building our own delivery platform to compete with DoorDash. Customers browse restaurants, build a cart from one restaurant per order, customize items with modifiers, and check out through Stripe. We dispatch our own pool of drivers. Drivers see assigned orders, accept or decline, and update status as they pick up and deliver. Restaurants get a dashboard to manage menus and see their order queue. We payout restaurants weekly through Stripe Connect after taking a flat 8% fee to cover platform costs."

StackAlchemist generates:

- `Restaurant` entity with name, cuisine, address, hours, accepts_orders flag, prep_time_default, stripe_connect_account_id
- `Menu`, `MenuCategory`, `MenuItem` entities with prices, descriptions, images, availability windows
- `ItemModifierGroup` (Size, Toppings, Sides) and `ItemModifier` (Small/Medium/Large, +$2.00, required/optional) entities
- `Cart` scoped to a single restaurant_id with `CartItem` and `CartItemModifierSelection` entities
- `Order` with status enum (placed, accepted, preparing, ready, picked_up, delivered, cancelled), `OrderItem`, and `OrderStatusEvent` audit trail
- `Customer` with `DeliveryAddress` entity supporting multiple saved addresses, default flag, delivery instructions
- `Driver` entity with status (off, available, on_delivery), current_location lat/lng, and `DriverAssignment` join entity
- `DeliveryZone` entity with polygon or radius geometry, delivery_fee, minimum_order_amount
- `Payout` entity with restaurant_id, period_start, period_end, gross_sales, platform_fee, net_payout, stripe_transfer_id
- CRUD endpoints for every entity (`/api/v1/restaurants`, `/api/v1/orders`, `/api/v1/drivers`, …). Endpoints like `PATCH /orders/:id/status` or `POST /drivers/:id/accept-assignment` can be declared in Advanced Mode; the dispatch and status rules behind them are yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the delivery platform's data model and CRUD layer, compile-verified. The customer storefront, the restaurant operator dashboard, the driver mobile-web app, Stripe Connect split payouts and the webhook handling are code you build on it. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe Connect and onboard your first three restaurants in test mode.** The generated repo does not include Stripe; `Restaurant` has a `stripe_connect_account_id` column and `Payout` holds the fee split. Add Stripe Checkout for customer payments, Connect onboarding links for restaurants, a webhook handler, and the weekly payout job that takes the 8% fee. Set `STRIPE_API_KEY`, `STRIPE_CONNECT_CLIENT_ID`, and `STRIPE_WEBHOOK_SECRET` in your `.env.local`, walk three restaurant accounts through Stripe's hosted Connect onboarding, and place a test order end-to-end. You now have a working delivery platform with a fee split, no commission to a third party, and customers who belong to you.

2. **Write dispatch logic for your actual city.** The repo gives you drivers, assignments and locations; the dispatch logic is yours. Start with a sensible default — assign to the nearest available driver within the zone, fall back to broadcast — then add your city's quirks. Maybe downtown drivers shouldn't take orders that cross the river during rush hour. Maybe you want a 60-second accept window before reassigning. Write a `ScoreDriverAssignment()` method that weights distance, current load, driver rating, and zone traffic. Run a week of real orders, measure pickup-to-delivery time, iterate. This is the work that actually beats DoorDash in your market.

## What is not included

StackAlchemist is not DoorDash. We do not provide the driver supply, the restaurant supply, the customer demand, or the marketing engine that makes a delivery marketplace actually work. Building the two-sided (or three-sided) marketplace is the hard part of this business. We generate the technical foundation that lets you build one.

Payment compliance (PCI DSS) is your responsibility, but using Stripe Checkout and Stripe Connect offloads the hard part. We do not include SMS notifications, push notifications to driver apps, or third-party logistics integrations — those are wire-ups you do once you own the code. We do not generate native iOS or Android driver apps; a mobile-web driver interface on the generated API is fine for v1 and most cooperatives. If you need a native driver app later, you build it on top of the generated API.

For a single restaurant or a regional cooperative trying to escape commission fees, this is exactly what you want — ownership, no platform fee, no vendor risk. For a national-scale marketplace with venture funding, you are going to outgrow any starter eventually, but this is still the fastest way to v1.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No commission on your orders. You own what you generate.

## Get started

Describe your food delivery platform in plain English. We generate the code. You own it, and you keep every dollar of margin that DoorDash would have taken.

**[Start generating →](/simple)**
