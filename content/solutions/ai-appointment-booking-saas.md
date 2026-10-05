# Generate a full AI Appointment Booking SaaS from a prompt

You describe the kind of booking system you want. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. Booking is your storefront — owning it means a branded experience, no per-seat tax as the team grows, and the customer database stays yours. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI appointment booking SaaS with:

- **Services catalog** — `Service` and `ServiceCategory` entities with durations, prices, buffer times and category grouping
- **Staff availability** — `Staff`, `StaffAvailability` and `TimeOff` entities for recurring weekly schedules and one-off blackouts
- **Appointments and bookings** — `Appointment`, `Booking` and `Customer` entities with status, timezone, deposit fields and a `stripe_payment_intent_id` column ready for your payment integration
- **Reminders** — a `Reminder` entity (channel, send time, status) for the reminder sender you plug in
- **Recurring series and policies** — `RecurringSeries`, `CancellationPolicy` and `RescheduleRule` entities holding cutoff windows and forfeit rules
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** slot calculation and the customer booking flow, Stripe deposits and payments, the email and SMS reminder sender, cancellation-rule enforcement, the admin calendar, staff and customer sign-in (the Supabase client is preinstalled; the auth flows are yours to write), and your CI pipeline. The schema has the fields for all of it; the behavior is your code.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of renting Calendly

**Per-seat pricing punishes you for growing.** Calendly is $10-20 per seat per month. Acuity Scheduling is $16-49 per month per business but the higher tiers gate features you actually need. Add ten staff members and you are paying $100-200 a month forever. A generated codebase has no seat tax. You add staff by inserting rows.

**Their booking page is their brand, not yours.** Calendly puts their logo at the bottom. SimplyBook.me does too. Your customers learn to trust the tool, not your business. A booking page generated under your domain, with your colors and your copy, is a brand asset you own.

**The customer list is the business.** Square Appointments and Acuity both keep customer data in their cloud. Switch providers and you are re-entering history. With a generated platform, the database is yours from day one. Export, migrate, integrate with your CRM — no API limits, no export fees.

## Who this is for

- **Solo consultants and coaches** who do not want to pay Calendly forever and want the booking page to feel like their brand.
- **Salons and spas** with multiple stylists, complex availability, and a need for deposit collection — generate once, customize for the shop.
- **B2B sales teams** taking demo bookings who want to integrate the booking flow into their marketing site and pipe leads directly into their own CRM.
- **Healthcare clinics** that need a branded booking layer in front of their patient portal (and yes, point at the healthcare patient portal vertical too for the records side).

## Example entities generated

A typical AI appointment booking SaaS generation produces entities like:

- `Service` / `ServiceCategory` / `Duration`
- `Staff` / `StaffAvailability` / `TimeOff`
- `Appointment` / `AppointmentStatus` / `RecurringSeries`
- `Customer` / `CustomerNote`
- `Booking` / `BookingPayment` / `Deposit`
- `Reminder` / `ReminderTemplate`
- `CancellationPolicy` / `RescheduleRule`

The exact shape depends on your prompt. A solo therapist generates different entities than a five-chair salon.

### Real example: Three-stylist hair salon

Imagine you submit this spec:

> "We run a salon with three stylists. Services are haircut (45 min, $60), color (2 hours, $150), and blowout (30 min, $45). Each stylist works different days — Sarah does Tue/Wed/Sat, Mike does Mon/Thu/Fri/Sat, Lin does Wed/Thu/Fri. Customers book online, pay a $20 deposit, and get an SMS reminder 24 hours before. Cancel less than 4 hours out and the deposit is forfeit. We need an admin view of the daily calendar."

StackAlchemist generates:

- `Service` entity with name, duration_minutes, price_cents, buffer_before, buffer_after, and category
- `Staff` entity with name, email, phone, and a one-to-many to availability rules
- `StaffAvailability` entity with staff_id, day_of_week, start_time, end_time (recurring weekly schedule)
- `TimeOff` entity with staff_id, start_datetime, end_datetime, reason (one-off blackouts)
- `Appointment` entity with customer_id, staff_id, service_id, start_at, end_at, status (pending, confirmed, completed, no_show, cancelled), timezone
- `Booking` entity with deposit_amount, deposit_paid_at, total_amount, stripe_payment_intent_id
- `Customer` entity with name, email, phone, preferred_contact_method, timezone
- `Reminder` entity with appointment_id, channel (email or sms), send_at, sent_at, status
- `CancellationPolicy` entity with cutoff_hours (4), deposit_forfeit_rule (true)
- CRUD endpoints for every entity (`/api/v1/services`, `/api/v1/staff`, `/api/v1/appointments`, `/api/v1/bookings`, …). Workflow endpoints such as `GET /availability`, `POST /bookings/:id/cancel` and `POST /bookings/:id/reschedule` can be declared in Advanced Mode; the slot math and cancellation rules behind them are yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the salon's data model and CRUD layer, compile-verified. The customer booking flow with its timezone-aware slot grid, the Stripe Checkout deposit and its webhook, and the job that sends SMS reminders 24 hours out are code you add on top. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe deposits and an SMS provider, then run a live test booking.** The repo is not wired to any payment or messaging provider. It gives you `Booking` with deposit fields and a `stripe_payment_intent_id` column, and `Reminder` with channel, send time and status. Add a Stripe Checkout call for the $20 deposit and a webhook endpoint that sets `deposit_paid_at`. Then add a small scheduled job that picks up due `Reminder` rows and sends them through Twilio, MessageBird, or your provider of choice, with `SMS_PROVIDER_API_KEY` in your `.env.local`. Book yourself a test appointment one hour out. You should get an SMS. You are now operating a booking platform that actually sends reminders, and you control the SMS spend at-cost from your provider — not marked up by Calendly.

2. **Write the availability logic, including the rules only your shop has.** The repo gives you the availability, time-off and appointment tables with Dapper repositories; turning them into bookable slots is your code. Write a `CanBook(staffId, serviceId, slotStart, slotEnd)` method that checks the weekly schedule, time off and existing appointments, then add your own rules: maybe Sarah only takes color appointments on Saturdays, or you want to block double-booking across services that require the same shampoo bowl. This is not a hack — the codebase is yours, and these business rules are exactly where you extend it.

## What is not included

StackAlchemist is not a hosted Calendly replacement. We do not host your booking page, do not provide an SMS-sending service ourselves (you plug in Twilio or similar), and do not operate the platform for you. We generate you the code. You deploy and operate it. SMS and email costs are at-provider — no markup, but also no included quota.

We do not include Google Calendar / Outlook two-way sync: OAuth flows for every calendar provider add complexity most operators don't need on day one, and that integration is yours to write when you need it. We also do not include payroll, commission tracking, or POS integration — if you need a full salon management system on top of bookings, those are extensions you build on the foundation. For most booking businesses, what you want is ownership of the booking layer with the freedom to add the rest. That is what this gives you.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your booking platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
