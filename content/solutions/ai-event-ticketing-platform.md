# Generate a full AI Event Ticketing Platform from a prompt

You describe the kind of ticketing platform you want. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. No per-ticket fees siphoning revenue. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI event ticketing platform with:

- **Event creation** with multi-event series, recurring shows, venue assignment, and draft/published states
- **Ticket inventory** for general admission and reserved seating with capacity controls and waitlist support
- **Seat-map editor scaffolding** with section/row/seat hierarchy, hold timers, and per-seat pricing tiers
- **QR-code tickets** generated server-side with signed payloads, delivered by email and downloadable PDF
- **Scanner check-in flow** with a mobile-friendly web scanner, duplicate-scan detection, and offline queue
- **Attendee data ownership** — full email list, order history, and CSV export, all on your database
- **Organizer dashboard** with sales-by-event, scan rates, attendee lookup, and refund tooling
- **Payouts** wired to Stripe Connect (or your processor) — funds land in your account, not a middleman's
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- `Attendee` entity aggregating purchase history across all events for a given email
- API endpoints: `POST /events`, `POST /orders/checkout`, `GET /tickets/:id/qr`, `POST /scan`, `GET /organizer/events/:id/sales`, plus admin endpoints for seat-map editing and refunds
- A Next.js scanner page that opens the device camera, decodes QR codes, and POSTs to `/scan` with optimistic UI and an offline IndexedDB queue
- A seat-map editor scaffold using SVG-based interaction with hold timers backed by a Redis-or-Postgres lock

All wired into a Next.js organizer dashboard and buyer flow, with a .NET backend handling QR signing, Stripe webhooks, and scan validation. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up Postgres, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe Connect to your test account and run a real end-to-end purchase.** The generated repo includes the Stripe Checkout integration and webhook handlers for payment_intent.succeeded, but you need to drop in your own test API keys and configure your Connect account so payouts route to your bank. Set `STRIPE_API_KEY`, `STRIPE_WEBHOOK_SECRET`, and `STRIPE_CONNECT_ACCOUNT_ID` in `.env.local`, run the migrations, buy a ticket as a test user, and confirm the QR code arrives by email. You now have a working purchase-to-scan loop without ever touching Eventbrite.

2. **Customize the scanner flow for your actual door staff.** The generated scanner is a generic phone-camera page, but every venue has weird door logic — early entry for VIPs, plus-ones at the door, in-and-out wristbands, will-call lookups by name. The code is yours to modify. You add a `wristband_color` field to the ticket entity, a will-call search endpoint, and a VIP-early-entry flag with a time window. This is not a template hack — this is the product working as designed. Five hours of work on day one buys you a check-in flow your staff actually likes using.

## What is not included

StackAlchemist is not Eventbrite. We do not host your ticketing site, do not run a discovery marketplace, and do not handle payouts for you (Stripe Connect does that — you plug it in). We generate you the code. You deploy and operate it.

We do not include native mobile scanner apps — the generated scanner is a mobile web page that works on any phone with a camera, which covers 95% of door-staff needs. We do not handle real-time waitlist SMS notifications by default unless your spec asks for them, because Twilio integration costs tokens and most organizers do not need it on day one. Reserved-seating with complex stadium-style maps (50,000+ seats with accessibility zones) is out of scope for the generated scaffolding — for true arena ticketing you are better off building on top of the generated base.

## Pricing

One-time, per generation:

- **Simple-mode ticketing** — $299. Single venue, GA tickets only, basic organizer dashboard, Stripe checkout, QR scanner.
- **Blueprint-tier** — $599. Adds reserved seating with seat-map editor, multi-event series, attendee CRM with email export, refund tooling.
- **Boilerplate-tier** — $999. Adds multi-venue, multi-organizer (white-label tenants), waitlist with auto-promotion, advanced sales analytics, payout splits across co-promoters.

No monthly fee. No per-ticket fee. You own what you generate.

## Get started

Describe your ticketing platform in plain English. We generate the code. You keep every dollar above your processor fees.

**[Start generating →](/simple)**
