# Generate a full AI Appointment Booking SaaS from a prompt

You describe the kind of booking system you want. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. Booking is your storefront — owning it means a branded experience, no per-seat tax as the team grows, and the customer database stays yours. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI appointment booking SaaS with:

- **Services catalog** with durations, prices, buffer times, and category grouping
- **Staff availability** with recurring weekly schedules, one-off blackouts, and time-off requests
- **Customer-facing booking flow** with timezone-aware slot pickers and confirmation pages
- **Stripe integration** for deposits, full payment at booking, or pay-on-arrival flows
- **Automated reminders** via email and SMS — 24-hour, 1-hour, and custom intervals
- **Recurring appointments** with series management and bulk reschedule
- **Cancellation and rescheduling rules** with cutoff windows and policy enforcement
- **Admin dashboard** for calendar view, staff schedules, customer history, and reporting
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- API endpoints: `GET /services`, `GET /availability?service_id&date_range`, `POST /bookings`, `POST /bookings/:id/cancel`, `POST /bookings/:id/reschedule`, `POST /webhooks/stripe`, plus admin endpoints for staff schedules and calendar views
- Next.js customer-facing booking flow with service picker, stylist picker, timezone-aware slot grid, and Stripe Checkout for the deposit
- .NET background job that scans for appointments 24 hours out and queues SMS reminders via the SMS provider

All wired into a Next.js storefront and a .NET backend with Stripe webhooks closing the loop on deposit payments. Docker-compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire your SMS provider and run a live test booking.** The generated code ships with a reminder service interface and a no-op default. Drop in Twilio, MessageBird, or your provider of choice — set `SMS_PROVIDER_API_KEY` in your `.env.local`, swap the implementation in DI registration, and book yourself a test appointment one hour out. You should get an SMS. You are now operating a booking platform that actually sends reminders, and you control the SMS spend at-cost from your provider — not marked up by Calendly.

2. **Add a domain-specific availability rule.** Maybe Sarah only takes color appointments on Saturdays, or you want to block double-booking across services that require the same shampoo bowl. The generated availability service is a normal C# class — add a `CanBook(staffId, serviceId, slotStart, slotEnd)` method that checks your custom rule alongside the standard availability lookup. This is not a hack — the codebase is yours, and these business rules are exactly where StackAlchemist expects you to extend.

## What is not included

StackAlchemist is not a hosted Calendly replacement. We do not host your booking page, do not provide an SMS-sending service ourselves (you plug in Twilio or similar), and do not operate the platform for you. We generate you the code. You deploy and operate it. SMS and email costs are at-provider — no markup, but also no included quota.

We do not include Google Calendar / Outlook two-way sync out of the gate unless your spec asks for it, because OAuth flows for every calendar provider add complexity most operators don't need on day one. We also do not include payroll, commission tracking, or POS integration — if you need a full salon management system on top of bookings, those are extensions you build on the foundation. For most booking businesses, what you want is ownership of the booking layer with the freedom to add the rest. That is what this gives you.

## Pricing

One-time, per generation:

- **Simple-mode booking** — $299. Single-location, basic services, staff availability, email reminders, Stripe deposits.
- **Blueprint-tier** — $599. Adds SMS reminders, recurring appointments, cancellation policy enforcement, customer login, and a richer admin calendar.
- **Boilerplate-tier** — $999. Multi-location, package deals, gift cards, advanced reporting, calendar sync scaffolding, and waitlist management.

No monthly fee. No per-seat tax. You own what you generate.

## Get started

Describe your booking platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
