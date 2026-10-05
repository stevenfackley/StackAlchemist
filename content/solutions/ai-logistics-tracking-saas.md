# Generate a full AI Logistics Tracking SaaS from a prompt

You describe the kind of fleet and delivery operation you run. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. Shipments, drivers, routes, and proof-of-delivery records are modeled in one pass, with CRUD endpoints for each. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI logistics tracking SaaS with:

- **Shipments** — `Shipment`, `ShipmentStatusEvent` and `ExceptionRecord` entities with addresses, weight, dimensions, service level, special-handling flags and a status history
- **Drivers and vehicles** — `Driver`, `Vehicle` and `VehicleAssignment` entities with status, capacity and inspection dates
- **Routes** — `Route`, `RouteStop` and `RouteProgress` entities with stop order and ETAs
- **Location history** — `LocationCheckIn` and `LocationHistory` entities for driver GPS check-ins
- **Proof of delivery** — `ProofOfDelivery`, `SignatureCapture` and `PhotoAttachment` entities with recipient name, timestamp and geo-stamp
- **Customers and billing** — `Customer`, `Address`, `DeliveryWindow`, `BillingRecord` and `Invoice` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** route optimization (Mapbox or Google Distance Matrix), file storage for signatures and photos, the exception workflows, the customer tracking page and dispatcher dashboard, driver and customer sign-in (the Supabase client is preinstalled; the auth flows are yours to write), the driver mobile app, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of paying Onfleet per driver

**Per-driver pricing punishes growth.** Onfleet, Routific, Bringg — they all charge per driver or per vehicle every month. A 40-driver fleet on Onfleet runs $500-$1,500/month forever. A generated StackAlchemist codebase is one $599 payment, and adding the 41st driver costs you zero.

**Logistics platforms own your operational data.** Routing patterns, customer addresses, delivery times, driver performance — that is your competitive moat as a logistics business. Bringg and Onfleet aggregate that data across their customer base. A codebase you own keeps your data in your Postgres instance, on your infrastructure, where no platform can mine it or sell insights derived from it.

**Track-POD and friends lock you into their POD format.** Every logistics ops team eventually needs a custom exception — a specific signature workflow for medical deliveries, a multi-piece scan for furniture, a temperature reading for cold-chain. With a generated codebase you add the field, run the migration, ship the change. With a vendor you file a feature request.

## Who this is for

- **Regional courier companies** running 10-200 drivers who want to stop paying per-driver platform fees and own the routing stack.
- **DTC brands with in-house last-mile fleets** who need a tracking and POD system tailored to their delivery promise (white-glove, room-of-choice, signature-required).
- **B2B parts distributors** running their own trucks for same-day delivery to dealers, shops, or job sites.
- **Logistics tech founders** who want a compile-verified starting point before building a vertical-specific delivery product.

## Example entities generated

A typical AI logistics tracking SaaS generation produces entities like:

- `Shipment` / `ShipmentStatusEvent` / `ExceptionRecord`
- `Driver` / `Vehicle` / `VehicleAssignment`
- `Route` / `RouteStop` / `RouteProgress`
- `Customer` / `Address` / `DeliveryWindow`
- `ProofOfDelivery` / `SignatureCapture` / `PhotoAttachment`
- `LocationCheckIn` / `LocationHistory`
- `BillingRecord` / `Invoice` / `ServiceLineItem`

The exact shape depends on your prompt. A white-glove furniture operation generates different entities than a same-day parts courier.

### Real example: Regional same-day courier

Imagine you submit this spec:

> "We run a same-day courier service with 35 drivers covering the metro area. Customers book pickups through our portal, we assign to a driver, the driver does pickup, then delivery, capturing signature and photo at the dropoff. We bill customers monthly based on miles and stops. We need a driver mobile API, a dispatcher dashboard, and a customer tracking page."

StackAlchemist generates:

- `Shipment` entity with pickup_address, delivery_address, requested_pickup_window, service_level (standard, rush, scheduled), and status
- `ShipmentStatusEvent` entity capturing every state transition with actor, timestamp, and notes
- `Driver` entity with name, license_number, phone, status (available, on_route, off_duty), assigned_vehicle_id
- `Vehicle` entity with type (van, cargo_bike, sprinter), capacity, license_plate, last_inspection_date
- `Route` entity grouping shipments for a driver-shift, with ordered RouteStop children
- `LocationCheckIn` entity with driver_id, lat, lng, recorded_at, accuracy, battery_level
- `ProofOfDelivery` entity with shipment_id, signature_blob_url, photo_urls (array), recipient_name, captured_at, captured_lat_lng
- `ExceptionRecord` entity with shipment_id, exception_type (no_one_home, refused, address_invalid, damaged), photo_evidence, resolution_action
- `BillingRecord` entity with customer_id, billing_period, total_stops, total_miles, surcharges, line_items
- CRUD endpoints for every entity (`/api/v1/shipments`, `/api/v1/drivers`, `/api/v1/routes`, …). Driver-app endpoints like `GET /drivers/me/manifest` and `POST /shipments/:id/pod`, or a public `GET /track/:shipment_token`, can be declared in Advanced Mode; the logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the fleet's data model and CRUD layer, compile-verified. The dispatcher dashboard, the customer tracking page, and a driver-specific API surface for the mobile client are code you build on it. Docker Compose spins up PostgreSQL, the API, and the frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire a real mapping provider.** The repo gives you `Route` and `RouteStop` with stop order and ETA fields; route ordering and ETA calculation are yours to write. Put them behind an `IRouteOptimizer` interface and implement it with a call to the Mapbox or Google Distance Matrix endpoint, using your own API key. For most courier operations a Mapbox Optimization API plan at $0.05/route is dramatically cheaper than paying Routific's per-vehicle subscription.

2. **Build the driver mobile client.** The backend exposes CRUD endpoints for shipments, check-ins, proofs of delivery and exceptions, with OpenAPI describing every request and response. Add the driver-facing endpoints you need (manifest pull, POD upload) and driver sign-in, then build a React Native or Flutter app against them, or use a no-code mobile tool that hits REST. Most courier operations ship a thin mobile app in two to four weeks once the backend is wired. Auth, conflict resolution and exception handling are part of that work; the data model they sit on is already there.

## What is not included

StackAlchemist is not a turnkey logistics platform. We do not provide the driver mobile app binary, do not bundle a mapping or routing service, and do not provide ongoing fleet telematics. We generate the backend data model and the CRUD API that your dispatcher dashboard, customer tracking page and driver app will consume. You build or buy the mobile client and you pay your own mapping provider.

We do not include integrations with carrier APIs (UPS, FedEx, USPS) out of the gate — this product is for operations running their own fleet, not for resellers of national carriers. We do not include EDI or freight tendering — that is a different vertical. If your operation needs those, the generated codebase is still a fine foundation, but you are writing the integrations after generation.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-driver fee. You own what you generate.

## Get started

Describe your fleet and delivery operation in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
