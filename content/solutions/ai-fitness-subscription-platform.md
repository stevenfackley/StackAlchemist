# Generate a full AI Fitness Subscription Platform from a prompt

You describe the kind of studio or fitness business you want to run. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI fitness subscription platform with:

- **Members** — a `Member` entity with profile, emergency contact and waiver fields
- **Plans and subscriptions** — `MembershipPlan` and `Subscription` entities for monthly, annual, class-pack and drop-in plans, with a `stripe_subscription_id` column ready for your Stripe integration
- **Classes and bookings** — `Class`, `ClassSession`, `Booking` and `WaitlistEntry` entities with capacity, instructor and status
- **Check-ins** — `CheckIn` and `Attendance` entities with no-show tracking fields
- **Instructors** — `Instructor`, `Certification` and `InstructorSchedule` entities with bios and payout rates
- **Workout programs** — `WorkoutProgram`, `Exercise` and `WorkoutLog` entities with sets, reps and progressions
- **Locations** — `Location`, `Room` and `Equipment` entities
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** Stripe Subscriptions and their webhooks, capacity checks and waitlist promotion, the QR check-in flow, the member portal and admin dashboard, member and staff sign-in (the Supabase client is preinstalled; the auth flows are yours to write), instructor payouts, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

You generate it from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of paying Mindbody

**Mindbody charges $129 to $595 per month per location and the UX is from 2014.** Their pricing scales with you, their reporting is locked behind upgrade tiers, and your member data lives on their servers. A generated codebase costs once, runs on your infrastructure, and lets you redesign the booking flow whenever you want.

**ClassPass takes 30 to 50 percent of class revenue.** That's not a platform fee — that's a partner taking half your gross every time a member walks in. Owned code keeps 100 percent of subscription revenue in your account. The Stripe fee is the only cut.

**Glofox, ABC, and Mariana Tek all rent you software.** They host your members, they own the schedule, they decide when features ship. When they raise prices you have no leverage. A StackAlchemist-generated platform is yours — modify the booking logic, add a new membership tier, integrate a wearable, ship it tonight.

## Who this is for

- **Boutique studio owners** running yoga, pilates, barre, or spin who are sick of paying Mindbody $300+/month and want to own their member relationships.
- **CrossFit affiliate gyms** that need class capacity, workout-of-the-day programming, and member progress tracking without the $215/month box-management fee.
- **Multi-location martial arts and boxing chains** that need shared member rosters, location-specific schedules, and consolidated billing across gyms.
- **Virtual-fitness startups** launching a subscription service who want the member, plan and class model generated, so their engineering months go into auth, billing, content delivery and member dashboards instead of plumbing.

## Example entities generated

A typical AI fitness subscription platform generation produces entities like:

- `Member` / `MembershipPlan` / `Subscription`
- `Class` / `ClassSession` / `Booking` / `WaitlistEntry`
- `Instructor` / `Certification` / `InstructorSchedule`
- `Location` / `Room` / `Equipment`
- `WorkoutProgram` / `Exercise` / `WorkoutLog`
- `CheckIn` / `Attendance`
- `Payment` / `Invoice` / `Refund`

The exact shape depends on your prompt. A boutique pilates studio generates different entities than a multi-location boxing chain with virtual class delivery.

### Real example: Three-location boutique pilates studio

Imagine you submit this spec:

> "We run three pilates studios. Members pick a plan — unlimited monthly, 8-class pack, or drop-in. They book classes through a member portal, can join a waitlist when full, and check in at the studio. Each class has a capped reformer count, an instructor, and a location. We need Stripe to handle monthly billing and to charge class-pack purchases. Admins need to see attendance, no-shows, and revenue per instructor."

StackAlchemist generates:

- `Member` entity with name, email, phone, waiver_signed_at, emergency_contact, profile_photo
- `MembershipPlan` entity with name, price, billing_interval (monthly, annual, pack), class_credits, max_classes_per_week
- `Subscription` entity with member_id, plan_id, stripe_subscription_id, status (active, paused, canceled), next_billing_date
- `Location` entity with name, address, timezone, capacity, equipment_count
- `Class` entity with name, description, duration_minutes, default_instructor_id, location_id, max_capacity
- `ClassSession` entity with class_id, instructor_id, starts_at, capacity (override), status (scheduled, canceled, completed)
- `Booking` entity with session_id, member_id, status (booked, attended, no-show, canceled), booked_at, credits_used
- `WaitlistEntry` entity with session_id, member_id, position, joined_at, promoted_at
- `Instructor` entity with name, bio, certifications, payout_rate, profile_photo, stripe_connect_id
- `CheckIn` entity with booking_id, checked_in_at, checked_in_by (front_desk or self)
- CRUD endpoints for every entity (`/api/v1/members`, `/api/v1/bookings`, `/api/v1/checkins`, …). Endpoints like `POST /classes/:id/book`, `POST /classes/:id/waitlist` or a revenue-per-instructor report can be declared in Advanced Mode; the capacity, waitlist and reporting logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the studio's data model and CRUD layer, compile-verified. The member portal, capacity checks, atomic waitlist promotion, Stripe Subscriptions (recurring billing, dunning, pack-credit accounting) and the webhook handlers for `customer.subscription.updated`, `invoice.paid` and `invoice.payment_failed` are code you write on top. Docker Compose spins up PostgreSQL, the .NET API, and the Next.js frontend so you can boot the stack in one command on a dev laptop.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Wire Stripe and run a test subscription.** The generated repo does not include Stripe; `MembershipPlan` and `Subscription` hold the plan, the `stripe_subscription_id` and the billing status. Create your plans as Stripe Prices, add a checkout endpoint that starts a subscription, and write a webhook handler for `customer.subscription.updated`, `invoice.paid` and `invoice.payment_failed` that updates `Subscription` and adds class credits. Drop your Stripe test keys into `.env.local` and sign up as a test member. You should see the subscription create, the first invoice paid, and the credits land in the member's account. From there, you switch to your production Stripe account and you are billing real members.

2. **Build the policy that makes your studio yours.** Maybe you want late-cancel fees ($10 if a member cancels within 4 hours of class). Maybe you want a no-show policy that auto-deducts a credit after the third no-show in a month. Maybe you want priority booking for annual members (they can book 14 days out, monthly members 7 days out). These are business rules — not template features — and the generated code is yours to extend. You add a `LateCancelPolicy` service, a background job that processes no-shows, or a `BookingWindow` rule in the class endpoints. This is the product working as designed.

## What is not included

StackAlchemist is not Mindbody. We do not host your booking site, do not provide a mobile app out of the box (the frontend is a Next.js web app, not native), and do not provide ongoing platform operations. We generate the code. You deploy and run it.

We do not include native iOS/Android apps; if you want one, you build it against the generated API, and App Store submission is on you. Wearable integrations (Apple Health, Fitbit, Whoop) are not included — wire them in once you own the code. PCI compliance is offloaded to Stripe Checkout once you wire it in, but tax handling, waiver storage, and HIPAA considerations (if you collect health data) are your responsibility once the code is yours.

For most studios and gyms, this is the correct trade. You stop renting Mindbody, you own the member relationship, and you keep 30 percent of revenue that used to go to ClassPass.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No revenue share — keep 100 percent of subscription revenue minus Stripe fees. You own what you generate.

## Get started

Describe your studio or fitness business in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
