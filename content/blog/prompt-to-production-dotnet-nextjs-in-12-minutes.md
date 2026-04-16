# From prompt to production: generating a .NET 10 + Next.js 15 SaaS in 12 minutes

**By Steve Ackley · April 25, 2026 · 8 min read**

People keep asking me what actually happens between the moment you hit "Generate" and the moment the zip lands in your download folder. Twelve minutes of black box feels long — especially compared to tools that return output in forty seconds.

This post is the narrated playback. I picked a real generation run from last week, stripped the identifying details, and walked through every stage with actual timings. Not a marketing story — the real thing.

## Minute 0: the prompt

The prompt I am using as the example:

> A subscription management dashboard for small gyms. Coaches can create class schedules, members book classes, admins see attendance and churn. Stripe for billing. Owner-role admin. Next.js + .NET + Postgres.

This is a Simple-mode prompt. In Advanced mode you would also provide the entity list, role matrix, and feature flags. The engine handles them the same way internally; Simple mode just infers them.

User hits Generate. The clock starts.

## Minute 0 to 1: queue and provisioning

- **t=0s:** Prompt hits our API. We validate it, check the user's tier, check their remaining credit.
- **t=2s:** Job enqueued into our generation queue. This is a background worker pool running on ARM64 EC2 — one worker per concurrent build.
- **t=8s:** Worker picks up the job. Ephemeral build directory created, isolated from every other generation in flight.
- **t=15s:** Generation config loaded. We read the prompt, infer the vertical (fitness/subscription), select the `saas-subscription` template family, initialize the LLM session with our system prompt.

You are staring at a progress bar. On our side, nothing has touched the LLM yet. This is all scaffolding.

## Minute 1 to 3: domain modeling (LLM pass 1)

- **t=60s:** First LLM call goes out. Purpose: derive the domain model. The LLM is given the prompt plus a schema of what a valid domain looks like (must include User with Supabase auth fields; must declare relationships explicitly; etc.).
- **t=90s:** LLM returns. In this run, it proposed entities: `Gym`, `Coach`, `Member`, `ClassSession`, `Booking`, `Subscription`, `Payment`, `AttendanceRecord`.
- **t=95s:** We validate the LLM output against our schema. The first attempt failed because the LLM did not specify the relationship between `Coach` and `Gym`. This is a common failure mode. We re-prompt with a targeted error: "Coach must declare Gym relationship."
- **t=135s:** Second LLM call returns with the fix. Validation passes.
- **t=155s:** Domain model is now a typed artifact. It is not yet code.

Average for this stage: 1.5–2 minutes. It is bounded by LLM latency, not our code.

## Minute 3 to 5: business logic (LLM pass 2)

This is the stage that varies most by prompt. For a subscription gym, the LLM needs to generate service-layer methods for:

- Creating a class schedule (constraints: no double-booking a coach)
- Booking a class (constraints: capacity check, membership-tier check)
- Handling subscription lifecycle (new, pause, cancel)
- Processing Stripe webhooks (the glue code between Stripe events and our domain)

- **t=180s:** LLM call for service-layer generation. This is the single heaviest LLM call of the run — it emits several thousand lines of C# across the service classes.
- **t=240s:** LLM returns. Our validator runs: does every method signature match the interface we declared? Do the referenced entity fields all exist in the domain model from pass 1?
- **t=245s:** First validation pass fails. The LLM emitted a `CancelSubscriptionAsync` method that referenced a field `Subscription.CancelledOn` that does not exist in the domain model. We have two options: reject and re-prompt, or patch. For a missing-field error we usually patch — add the field to the domain model, extend the migration.
- **t=250s:** Patch applied.
- **t=270s:** Re-run validation. Passes.

Average for this stage: 1.5–2 minutes. Again, LLM-bound.

## Minute 5 to 8: deterministic assembly

Now the Swiss Cheese kicks in. The LLM artifacts from passes 1 and 2 get merged into our deterministic scaffold.

- **t=300s:** Project skeletons instantiated. One `.csproj` per bounded context, one `package.json` per Next.js app, pnpm workspace root generated.
- **t=320s:** Handlebars templates rendered. The LLM's domain model becomes EF Core entities with correctly-typed navigation properties. The LLM's service methods become real `.cs` files with dependency-injection boilerplate, logging, and exception handling.
- **t=340s:** Migration generated. Based on the entity definitions, we emit a SQL migration that creates the tables. We do NOT ask the LLM to write SQL — migrations are 100% deterministic.
- **t=360s:** Next.js side rendered. API routes for each entity, a typed data-access layer, a basic CRUD UI for each entity, and a starter dashboard.
- **t=400s:** Auth wiring added. Supabase project env vars declared, signup/login/password reset pages generated from our standard template.
- **t=430s:** Payments wiring. Stripe webhooks, products, subscription lifecycle handlers — all from our standard Stripe template, parameterized with the domain's subscription tiers.
- **t=460s:** Docker, docker-compose, CI/CD, README, LICENSE all emitted. These are 100% template.

This is the stage where most value is created. Notice it is almost entirely deterministic — LLM was heavily involved in minutes 1–5, then steps aside.

## Minute 8 to 11: the compile gate

Here is where most competitors stop. We do not.

- **t=480s:** Directory handed to the verification runner.
- **t=485s:** `dotnet restore` on the API projects.
- **t=540s:** `dotnet build` on the full solution. This is the expensive step — compiling a multi-project .NET 10 solution takes real CPU.
- **t=600s:** `dotnet test --filter Category=Smoke`. We run a small suite of smoke tests that are also generated from the domain model. They verify that entities can be created, read, updated, deleted, and that the service layer methods return the expected shapes.
- **t=630s:** `pnpm install` on the Next.js workspace.
- **t=660s:** `pnpm build`. Next.js compiles, typechecks, and produces a production build.

In 97% of runs this stage passes on the first try. In this run, it did.

In the 3% where it fails, we loop back to the relevant LLM stage with a targeted error ("service method `X` references missing type `Y`"), re-generate just the failing slice, and re-verify. Our retry budget is three attempts. If all three fail, we stop and return an error — we never ship a non-compiling zip.

## Minute 11 to 12: packaging

- **t=690s:** Full directory zipped.
- **t=700s:** Zip uploaded to our storage (S3-compatible, signed URL).
- **t=710s:** Download link emitted to the user. Progress bar hits 100%.
- **t=720s (12:00):** User clicks download.

## What you have at t=12 minutes

- A monorepo with .NET 10 API, Next.js 15 frontend, shared TypeScript types generated from C# models.
- Supabase auth wired. Stripe wired. PostgreSQL migrations ready to run.
- Docker-compose for local dev — `docker compose up` and everything is running on your machine in another ~2 minutes.
- GitHub Actions CI configured to run on push: lint, typecheck, unit tests, build.
- A README that tells you which env vars to set and what commands to run.
- A LICENSE file with your name in it (we ask at signup; if you did not provide one, it is blank).

Everything compiled. Everything owned. Yours.

## Why 12 minutes instead of 40 seconds?

The short answer: we do the verification step. Bolt and v0 return in under a minute because they never run a full build. Their output may or may not compile — it is your job to find out.

Our twelve minutes is mostly the compile gate. Remove that, and we are at about 6 minutes. But the whole product's thesis is that you should not have to be the build system. So we pay the six extra minutes so you do not.

## Key takeaways

- A StackAlchemist generation takes about 12 minutes end to end, from prompt to downloadable zip.
- **~5 minutes** is LLM work (domain model, business logic).
- **~3 minutes** is deterministic assembly (templates, scaffolding, wiring).
- **~4 minutes** is the compile gate (`dotnet build`, `dotnet test`, `pnpm build`).
- The compile gate is non-negotiable. It is the difference between a zip that runs and a zip that looks right but fails at `dotnet restore`.

If you want to run this yourself, [start with a prompt](/simple) and time it on your own clock. The progress bar will tell you where in this sequence your job is.

— Steve
