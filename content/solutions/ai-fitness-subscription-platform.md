# Generate a full AI Fitness Subscription Platform from a prompt

You describe the kind of studio or fitness business you want to run. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, wires up Stripe Subscriptions, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI fitness subscription platform with:

- **Member accounts** with profiles, emergency contacts, waivers, and progress tracking
- **Subscription plans** (monthly, annual, class-packs, drop-ins) billed via Stripe Subscriptions with webhook handling
- **Class schedules** with recurring sessions, capacity limits, waitlists, and instructor assignment
- **Check-in flow** for the front desk or self-serve QR code, with no-show tracking
- **Instructor profiles** with bios, certifications, schedules, and class assignments
- **Workout programs** with exercises, sets, reps, progressions, and member assignment
- **Admin dashboard** for members, classes, billing, attendance, and instructor payouts
- **Member portal** for booking, cancellations, waitlist position, and billing history
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

## Why generate it instead of paying Mindbody

**Mindbody charges $129 to $595 per month per location and the UX is from 2014.** Their pricing scales with you, their reporting is locked behind upgrade tiers, and your member data lives on their servers. A generated codebase costs once, runs on your infrastructure, and lets you redesign the booking flow whenever you want.

**ClassPass takes 30 to 50 percent of class revenue.** That's not a platform fee — that's a partner taking half your gross every time a member walks in. Owned code keeps 100 percent of subscription revenue in your account. The Stripe fee is the only cut.

**Glofox, ABC, and Mariana Tek all rent you software.** They host your members, they own the schedule, they decide when features ship. When they raise prices you have no leverage. A StackAlchemist-generated platform is yours — modify the booking logic, add a new membership tier, integrate a wearable, ship it tonight.

## Who this is for

- **Boutique studio owners** running yoga, pilates, barre, or spin who are sick of paying Mindbody $300+/month and want to own their member relationships.
- **CrossFit affiliate gyms** that need class capacity, workout-of-the-day programming, and member progress tracking without the $215/month box-management fee.
- **Multi-location martial arts and boxing chains** that need shared member rosters, location-specific schedules, and consolidated billing across gyms.
- **Virtual-fitness startups** launching a subscription service who need the platform stack — auth, billing, content delivery, member dashboards — without 4 months of engineering.

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
- API endpoints: `POST /classes/:id/book`, `DELETE /bookings/:id`, `POST /classes/:id/waitlist`, `POST /check-ins`, `GET /members/:id/attendance`, plus admin endpoints for revenue per instructor and no-show reports
- Stripe webhook handlers for `customer.subscription.updated`, `invoice.paid`, `invoice.payment_failed`

All wired into a Next.js member portal and a .NET backend, with the booking flow handling capacity checks and waitlist promotion atomically. The Stripe Subscriptions integration manages recurring billing, dunning, and pack-credit accounting. Docker-compose spins up PostgreSQL, the .NET API, and the Next.js frontend so you can boot the stack in one command on a dev laptop.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Connect Stripe and run a test subscription.** The generated repo includes the Stripe Subscriptions integration, webhook handlers, and the pack-credit accounting logic. Drop your Stripe test keys into `.env.local`, run the generated migrations, and sign up as a test member. You will see the subscription create, the first invoice paid, and the credits land in the member's account — without writing any payment code yourself. From there, you wire your production Stripe account and you are billing real members.

2. **Build the policy that makes your studio yours.** Maybe you want late-cancel fees ($10 if a member cancels within 4 hours of class). Maybe you want a no-show policy that auto-deducts a credit after the third no-show in a month. Maybe you want priority booking for annual members (they can book 14 days out, monthly members 7 days out). These are business rules — not template features — and the generated code is yours to extend. You add a `LateCancelPolicy` service, a background job that processes no-shows, or a `BookingWindow` rule in the class controller. This is the product working as designed.

## What is not included

StackAlchemist is not Mindbody. We do not host your booking site, do not provide a mobile app out of the box (the Next.js frontend is mobile-responsive but not native), and do not provide ongoing platform operations. We generate the code. You deploy and run it.

We do not include native iOS/Android apps unless your prompt specifically asks for an Expo or React Native scaffold — and even then, App Store submission is on you. Wearable integrations (Apple Health, Fitbit, Whoop) are not included by default — wire them in once you own the code. PCI compliance is offloaded to Stripe Checkout, but tax handling, waiver storage, and HIPAA considerations (if you collect health data) are your responsibility once the code is yours.

For most studios and gyms, this is the correct trade. You stop renting Mindbody, you own the member relationship, and you keep 30 percent of revenue that used to go to ClassPass.

## Pricing

One-time, per generation:

- **Simple-mode fitness platform** — $299. Single location, standard plans (monthly + class-pack), booking, check-in, Stripe billing, admin dashboard.
- **Blueprint-tier** — $599. Multi-location, waitlists, workout programs, instructor payouts via Stripe Connect, member progress tracking.
- **Boilerplate-tier** — $999. Virtual class delivery, multi-brand support, custom membership rules, advanced reporting, white-label member apps.

No monthly fee. No revenue share — keep 100 percent of subscription revenue minus Stripe fees. You own what you generate.

## Get started

Describe your studio or fitness business in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
