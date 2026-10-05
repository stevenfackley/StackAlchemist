# Generate a full AI Event Ticketing Platform from a prompt

You describe the kind of ticketing platform you want. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. No per-ticket fees siphoning revenue. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI event ticketing platform with:

- **Events and series** — `Event`, `EventSeries` and `Venue` entities with dates, venue assignment and status (draft, on sale, sold out, completed)
- **Ticket inventory** — `TicketType` and `TicketInventory` entities with price, quantity and sales windows
- **Reserved seating data** — `Section`, `Row` and `Seat` entities with seat status (available, held, sold)
- **Orders and tickets** — `Order` and `Ticket` entities with buyer email, a `stripe_payment_intent_id` column, a QR payload field and a check-in timestamp
- **Check-in records** — `ScanLog` and `CheckInDevice` entities recording each scan and its result
- **Attendee data ownership** — `Attendee` and `EmailSubscription` records in your own database, so the email list is yours
- **Payouts and refunds** — `Payout` and `RefundRequest` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Stripe Checkout (and Stripe Connect if you split payouts), QR signing and ticket emails, the scanner page and its duplicate-scan logic, the seat-map editor and hold timers, the organizer dashboard, organizer sign-in (the Supabase client is preinstalled; the auth flows are yours to write), and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of using Eventbrite

**Eventbrite charges you 3.7% plus $1.79 per ticket.** On a $40 ticket that is roughly $3.30 gone before you pay your card processor. Sell 5,000 tickets and you handed Eventbrite $16,500 to host a form and send an email. A generated platform costs you one zip and your own Stripe fees. The math gets ugly fast for anyone selling at scale.

**Eventbrite owns the attendee relationship.** Their terms restrict how you market to your own attendees through their system, and the discovery side of the platform actively cross-promotes competing events to your buyers. When you generate your own platform, the email list is in your Postgres database. You market to your audience however you want. No platform sitting between you and the people who paid you.

**Ticketmaster and Universe are worse.** Ticketmaster is the textbook case of fee rent-seeking, and Universe — pitched as the indie alternative — is owned by Ticketmaster. The "small organizer" market is consolidating into the same hands that ruined large-venue ticketing. Generating your own stack is the only way out that does not involve hand-rolling everything from scratch.

## Who this is for

- **Conference organizers** running multi-event series who want one codebase across all their events and a clean attendee CRM after the fact.
- **Music venues** with weekly bookings who are tired of paying per-ticket fees on $15 door tickets where the margin is already thin.
- **Nonprofit fundraisers** running reserved-seating galas where seat selection matters and Eventbrite's fees are coming straight out of mission dollars.
- **Developer-founders** building a vertical ticketing product (yoga studios, esports, comedy clubs, supper clubs) and want a compile-verified starting point.

## Example entities generated

A typical AI event ticketing platform generation produces entities like:

- `Event` / `EventSeries` / `Venue`
- `TicketType` / `TicketInventory`
- `Section` / `Row` / `Seat`
- `Order` / `Ticket` / `QrPayload`
- `Attendee` / `EmailSubscription`
- `ScanLog` / `CheckInDevice`
- `Payout` / `RefundRequest`

The exact shape depends on your prompt. A 200-seat music venue generates different entities than a 5,000-attendee conference with breakout sessions.

### Real example: Mid-size music venue with weekly shows

Imagine you submit this spec:

> "We are a 400-cap music venue running 3-4 shows a week. Each show has GA floor tickets and a small reserved balcony (40 seats). Buyers should pick balcony seats from a map. Tickets are QR codes emailed to the buyer. At the door we scan them in from a phone. We need an organizer dashboard to see sales per show, scan rates during the show, and export the email list of everyone who has ever bought a ticket."

StackAlchemist generates:

- `Event` entity with name, date, doors_at, start_at, venue_id, status (draft, on_sale, sold_out, completed)
- `TicketType` entity with event_id, name (GA, Balcony), price, quantity, sales_window_start, sales_window_end
- `Section` / `Row` / `Seat` entities with seat_number, row_label, status (available, held, sold)
- `Order` entity with buyer email, total, stripe_payment_intent_id, created_at
- `Ticket` entity with order_id, ticket_type_id, seat_id (nullable for GA), qr_payload, checked_in_at
- `ScanLog` entity with ticket_id, device_id, scanned_at, result (valid, duplicate, void)
- `Attendee` entity holding purchase history across all events for a given email
- CRUD endpoints for every entity (`/api/v1/events`, `/api/v1/orders`, `/api/v1/tickets`, …). Endpoints like `POST /orders/checkout`, `GET /tickets/:id/qr` or `POST /scan` can be declared in Advanced Mode; the checkout, QR signing and scan validation behind them are yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the ticketing data model and CRUD layer, compile-verified. The buyer flow, the organizer dashboard, the scanner page (camera, QR decoding, an offline IndexedDB queue), the seat-map editor with hold timers, QR signing and the Stripe webhook are code you build on it. Docker Compose spins up Postgres, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe and run a real end-to-end purchase.** The generated repo does not include Stripe; `Order` has a `stripe_payment_intent_id` column waiting for it. Add a checkout endpoint that creates the payment, a webhook handler for `payment_intent.succeeded` that issues the tickets, and the code that signs each `qr_payload` and emails it. If you split revenue with co-promoters or need payouts routed to several bank accounts, configure Stripe Connect too. Set `STRIPE_API_KEY` and `STRIPE_WEBHOOK_SECRET` in `.env.local`, buy a ticket as a test user, and confirm the QR code arrives by email. You now have a working purchase loop without ever touching Eventbrite.

2. **Build the scanner flow for your actual door staff.** The repo gives you `Ticket`, `ScanLog` and `CheckInDevice`; the scanner itself is yours to build — a mobile web page that opens the camera, decodes the QR code, and POSTs the scan. Build it around your door logic, because every venue has some: early entry for VIPs, plus-ones at the door, in-and-out wristbands, will-call lookups by name. Add a `wristband_color` field to the ticket entity, a will-call search endpoint, and a VIP-early-entry flag with a time window. This is not a template hack — this is the product working as designed, and it buys you a check-in flow your staff actually likes using.

## What is not included

StackAlchemist is not Eventbrite. We do not host your ticketing site, do not run a discovery marketplace, and do not handle payouts for you (Stripe Connect does that — you plug it in). We generate you the code. You deploy and operate it.

We do not include a scanner app, native or web — a mobile web scanner you build on the generated API works on any phone with a camera, which covers most door-staff needs. We do not include SMS notifications (waitlist or otherwise); that Twilio integration is yours to add, and most organizers do not need it on day one. Reserved-seating with complex stadium-style maps (50,000+ seats with accessibility zones) is out of scope for the generated scaffolding — for true arena ticketing you are better off building on top of the generated base.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-ticket fee. You own what you generate.

## Get started

Describe your ticketing platform in plain English. We generate the code. You keep every dollar above your processor fees.

**[Start generating →](/simple)**
