# From prompt to production: generating a .NET 10 + Next.js 16 SaaS in 12 minutes

**By Steve Ackley · April 25, 2026 · 8 min read**

*Corrected October 4, 2026: earlier versions said generated repos include Supabase auth pages, Stripe webhook handling, service-layer business logic, EF Core, a smoke-test run and GitHub Actions CI; generated repos ship compile-verified scaffolding plus per-entity CRUD code, and auth, payments and CI are yours to wire.*

People keep asking me what actually happens between the moment you hit "Generate" and the moment the zip lands in your download folder. Twelve minutes of black box feels long — especially compared to tools that return output in forty seconds.

This post is the narrated playback. I picked a real generation run from last week, stripped the identifying details, and walked through every stage with actual timings. Not a marketing story — the real thing.

## Minute 0: the prompt

The prompt I am using as the example:

> A subscription management dashboard for small gyms. Coaches can create class schedules, members book classes, admins see attendance and churn. Stripe for billing. Owner-role admin. Next.js + .NET + Postgres.

This is a Simple-mode prompt. In Advanced mode you would define the entities, fields and relationships yourself in the wizard (and can declare custom endpoints). Simple mode has the model infer them from the prompt.

One thing up front: the prompt asks for Stripe billing and an owner-role admin. Neither is generated. You get the entities those features need; wiring Stripe and auth is yours. More on that at the end.

User hits Generate. The clock starts.

## Minute 0 to 1: queue and provisioning

- **t=0s:** Prompt hits our API. We validate it, check the user's tier, check their remaining credit.
- **t=2s:** Job enqueued into our generation queue. This is a background worker pool running on ARM64 EC2 — one worker per concurrent build.
- **t=8s:** Worker picks up the job. Ephemeral build directory created, isolated from every other generation in flight.
- **t=15s:** Generation config loaded. We read the prompt, select the template set for the stack (.NET 10 + Next.js here), initialize the LLM session with our system prompt.

You are staring at a progress bar. On our side, nothing has touched the LLM yet. This is all scaffolding.

## Minute 1 to 3: domain modeling (LLM pass 1)

- **t=60s:** First LLM call goes out. Purpose: derive the domain model. The LLM is given the prompt plus the rules for a valid schema (a UUID primary key on every entity; field types from a fixed list; relationships declared explicitly).
- **t=90s:** LLM returns. In this run, it proposed entities: `Gym`, `Coach`, `Member`, `ClassSession`, `Booking`, `Subscription`, `Payment`, `AttendanceRecord`.
- **t=95s:** We validate the output: the JSON parses, every relationship points at an entity that exists, and the schema stays inside the size limits. If it fails, the run stops with the error instead of guessing. This one passed.

The domain model is now a typed schema. It is not yet code. In the product you see it at this point and can edit the entities, fields and relationships before code generation starts; the clock in this post leaves that review out. In Advanced mode you built the schema yourself, so this pass does not run.

Average for this stage: 1.5–2 minutes. It is bounded by LLM latency, not our code.

## Minute 3 to 5: per-entity code (LLM pass 2)

This is the stage that varies most by prompt, because it scales with the number of entities. For each entity in the schema, the LLM writes:

- The C# model record and its request DTO
- The Dapper repository (get all, get by id, create, update, delete)
- The CRUD endpoints
- The table's fragment of the SQL migration
- The TypeScript types, the typed API client calls, and the pages

Plus the one-line DI and route registrations that get spliced into `Program.cs`.

What it does not write: the double-booking rule, the capacity check, the subscription lifecycle, Stripe webhooks. The prompt asked for them, but they are business rules and integrations, and in the repo they are yours to write. The entities carry the fields they need.

- **t=180s:** LLM call for the per-entity code. This is the single heaviest LLM call of the run.
- **t=240s:** LLM returns. We parse the file blocks and check that every path lands inside the project tree the templates render. Output aimed anywhere else is rejected. Whether the code is actually right gets checked for real at the compile gate, below.

Average for this stage: 1.5–2 minutes. Again, LLM-bound.

## Minute 5 to 8: deterministic assembly

Now the Swiss Cheese kicks in. The LLM artifacts from passes 1 and 2 get merged into our deterministic scaffold.

- **t=300s:** Project skeleton rendered from Handlebars templates: a `dotnet/` .NET 10 minimal API project (`Program.cs` with DI, Serilog logging, OpenAPI, CORS and config) and a `nextjs/` Next.js 16 App Router project with Tailwind and the API client base.
- **t=320s:** The LLM's per-entity files are merged into the skeleton: model records, Dapper repositories, endpoint groups, and the registration lines spliced into `Program.cs`.
- **t=340s:** Migration assembled. `001_initial_schema.sql` creates the tables: UUID primary keys, foreign keys, row-level security enabled (no policies written; those are yours). The table fragments come from the LLM pass; the file around them is template.
- **t=360s:** Next.js side assembled. The types, the typed API client and a CRUD page for each entity, inside the template's layout.
- **t=400s:** Supabase slots. `@supabase/supabase-js` is preinstalled and the `NEXT_PUBLIC_SUPABASE_*` env vars are passed through `next.config`, compose and `.env.example`. Nothing calls the client. No sign-in, sign-up or password-reset pages are generated; the auth flows are yours to write.
- **t=460s:** Multi-stage Dockerfile, docker-compose, `.env.example` and `.gitignore` emitted. These are 100% template. There is no CI workflow in the repo.

This is the stage where most value is created. Notice it is almost entirely deterministic — LLM was heavily involved in minutes 1–5, then steps aside.

## Minute 8 to 11: the compile gate

Here is where most competitors stop. We do not.

- **t=480s:** Directory handed to the verification runner.
- **t=485s:** `dotnet restore` on the API project.
- **t=540s:** `dotnet build`. This is the expensive step — compiling .NET 10 takes real CPU.
- **t=600s:** `npm ci` on the Next.js app.
- **t=630s:** `npm run typecheck` (`tsc --noEmit`).
- **t=660s:** `next build`. Next.js compiles, typechecks, and produces a production build.

In 97% of runs this stage passes on the first try. In this run, it did.

In the 3% where it fails, the compiler errors go back to the model ("repository `X` references missing type `Y`"), it patches the failing files, and we run the builds again. Up to three repair attempts. If it still does not compile, the run fails and you are refunded — we never ship a non-compiling zip.

## Minute 11 to 12: packaging

- **t=690s:** Full directory zipped, with `build-report.json` recording every build command, its exit code and the verdict.
- **t=700s:** Zip uploaded to our storage (S3-compatible, signed URL).
- **t=710s:** Download link emitted to the user. Progress bar hits 100%.
- **t=720s (12:00):** User clicks download.

## What you have at t=12 minutes

- A repo with a .NET 10 minimal API (Dapper over Npgsql, Serilog, OpenAPI) and a Next.js 16 frontend, with TypeScript types, a typed API client and CRUD pages for every entity.
- A PostgreSQL SQL migration for your schema, row-level security enabled on every table, no policies written.
- Docker-compose for local dev — `docker compose up` and everything is running on your machine in another ~2 minutes.
- The Supabase client preinstalled with its env slots. The auth flows are yours to write.
- `Subscription` and `Payment` entities with their CRUD code. Stripe is not in the repo; wiring billing to those entities is yours.
- No CI workflow. Add the one your team uses.

Everything compiled. Everything owned. Yours.

## Why 12 minutes instead of 40 seconds?

The short answer: we do the verification step. Bolt and v0 return in under a minute because they never run a full build. Their output may or may not compile — it is your job to find out.

Our twelve minutes is mostly the compile gate. Remove that, and we are at about 6 minutes. But the whole product's thesis is that you should not have to be the build system. So we pay the six extra minutes so you do not.

## Key takeaways

- A StackAlchemist generation takes about 12 minutes end to end, from prompt to downloadable zip.
- **~5 minutes** is LLM work (domain model, per-entity CRUD code).
- **~3 minutes** is deterministic assembly (templates, scaffolding, merging).
- **~4 minutes** is the compile gate (`dotnet build`, typecheck, `next build`).
- The compile gate is non-negotiable. It is the difference between a zip that runs and a zip that looks right but fails at `dotnet restore`.

If you want to run this yourself, [start with a prompt](/simple) and time it on your own clock. The progress bar will tell you where in this sequence your job is.

— Steve
