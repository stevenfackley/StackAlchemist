# From prompt to production: what happens when you generate a .NET 10 + Next.js 16 SaaS

**By Steve Ackley · April 25, 2026 · 7 min read**

*Corrected October 4, 2026: earlier versions presented this as a timed playback of a real run that took 12 minutes, and claimed a 97% first-try pass rate. Neither number was measured, so both are gone, along with claims that generated repos include Supabase auth pages, Stripe webhook handling, service-layer business logic, EF Core, a smoke-test run and GitHub Actions CI. What follows is the pipeline as the code runs it.*

People keep asking me what actually happens between the moment you hit "Generate" and the moment the zip lands in your download folder. It takes minutes, not the forty seconds some tools return in, and that gap needs explaining.

This post walks through every stage, in order, as the engine runs it. I have not attached timings: until I have measured a meaningful number of real paid runs, I would rather show you the stages than invent a stopwatch.

## The prompt

The example prompt:

> A subscription management dashboard for small gyms. Coaches can create class schedules, members book classes, admins see attendance and churn. Stripe for billing. Owner-role admin. Next.js + .NET + Postgres.

This is a Simple-mode prompt. In Advanced mode you would define the entities, fields and relationships yourself in the wizard (and can declare custom endpoints). Simple mode has the model infer them from the prompt.

One thing up front: the prompt asks for Stripe billing and an owner-role admin. Neither is generated. You get the entities those features need; wiring Stripe and auth is yours. More on that at the end.

## Stage 1: the domain model (LLM pass 1)

The first model call derives the domain model. It gets the prompt plus the rules for a valid schema: a UUID primary key on every entity, field types from a fixed list, relationships declared explicitly.

For this prompt, the schema would look like `Gym`, `Coach`, `Member`, `ClassSession`, `Booking`, `Subscription`, `Payment`, `AttendanceRecord`, with the relationships between them.

We validate the output: the JSON parses, every relationship points at an entity that exists, and the schema stays inside the size limits. If it fails, the run stops with the error instead of guessing.

The domain model is now a typed schema, not code. You see it at this point and can edit the entities, fields and relationships before anything is generated. In Advanced mode you built the schema yourself, so this pass does not run.

## Stage 2: payment, then the queue

Blueprint, Boilerplate and Infrastructure are paid tiers, so the next step is Stripe Checkout. When Stripe confirms the payment, the Engine marks the generation paid and queues it. One in-process compile worker takes jobs one at a time.

(A Blueprint stops early: it never calls the model for code. It writes `schema.json` and `api-docs.md` from your schema and goes straight to packaging.)

## Stage 3: per-entity code (LLM pass 2)

This is the heaviest model call, and the one that scales with your schema. For each entity, the LLM writes:

- The C# model record and its request DTO
- The Dapper repository (get all, get by id, create, update, delete)
- The CRUD endpoints
- The table's fragment of the SQL migration
- The TypeScript types, the typed API client calls, and the pages

Plus the one-line DI and route registrations that get spliced into `Program.cs`.

What it does not write: the double-booking rule, the capacity check, the subscription lifecycle, Stripe webhooks. The prompt asked for them, but they are business rules and integrations, and in the repo they are yours to write. The entities carry the fields they need.

When the response comes back, we parse the file blocks and check that every path lands inside the project tree the templates render. Output aimed anywhere else is rejected. Whether the code is actually right gets checked for real at the compile gate, below.

## Stage 4: deterministic assembly

Now the Swiss Cheese kicks in. Rendering the templates takes a fraction of a second; it is the part of the pipeline that is fully deterministic:

- The project skeleton: a `dotnet/` .NET 10 minimal API project (`Program.cs` with DI, Serilog logging, OpenAPI, CORS, config and a `/healthz` route) and a `nextjs/` Next.js 16 App Router project with Tailwind and the API client base.
- The LLM's per-entity files, merged into the skeleton: model records, Dapper repositories, endpoint groups, and the registration lines spliced into `Program.cs`.
- The migration: `001_initial_schema.sql` creates the tables with UUID primary keys, foreign keys and row-level security enabled (no policies written; those are yours). The table fragments come from the LLM pass; the file around them is template.
- The Next.js side: the types, the typed API client and a page per entity, inside the template's layout.
- The Supabase slots: `@supabase/supabase-js` is preinstalled and the `NEXT_PUBLIC_SUPABASE_*` env vars are passed through `next.config`, compose and `.env.example`. Nothing calls the client. No sign-in, sign-up or password-reset pages are generated; the auth flows are yours to write.
- A multi-stage Dockerfile, docker-compose, `.env.example` and `.gitignore`, 100% template. There is no CI workflow in the repo.

## Stage 5: the compile gate

Here is where most competitors stop. We do not.

The tree is copied into a sandbox and built by its real toolchains, as an unprivileged user:

1. `dotnet restore`, then `dotnet build`, on the API project.
2. `npm ci` on the Next.js app (falling back to `npm install` if a repair added a dependency).
3. `npm run typecheck` (`tsc --noEmit`).
4. `next build`.

When a step fails, the compiler errors go back to the model ("repository `X` references missing type `Y`"), it patches the failing files, and we run the builds again. Up to three repair attempts. If it still does not compile, the run fails and you are refunded. We never ship a non-compiling zip.

## Stage 6: packaging

- The source tree is zipped, without build residue, with `build-report.json` recording every build command, its exit code and the verdict.
- The zip is uploaded to object storage and you get a signed download link, on the page and by email.

## What you have

- A repo with a .NET 10 minimal API (Dapper over Npgsql, Serilog, OpenAPI) and a Next.js 16 frontend, with TypeScript types, a typed API client and a page for every entity.
- A PostgreSQL SQL migration for your schema, row-level security enabled on every table, no policies written.
- Docker Compose for local dev: `docker compose up` and it runs on your machine.
- The Supabase client preinstalled with its env slots. The auth flows are yours to write.
- `Subscription` and `Payment` entities with their CRUD code. Stripe is not in the repo; wiring billing to those entities is yours.
- No CI workflow. Add the one your team uses.

Everything compiled. Everything owned. Yours.

## Why minutes instead of seconds?

The short answer: we do the verification step. Bolt and v0 return in under a minute because they never run a full build. Their output may or may not compile, and it is your job to find out.

A real `npm ci`, `dotnet build` and `next build` take real time, and a failed build costs a repair round on top. The whole product's thesis is that you should not have to be the build system. So we pay for the build so you do not.

## Key takeaways

- A generation is two model calls (the schema, then the per-entity code), a deterministic template render, and a real compile gate, in that order.
- The compile gate runs `dotnet build`, the TypeScript typecheck and `next build`, with up to three repair attempts and a refund if it never compiles.
- Business rules, auth, payments and CI are not generated. The entities and their CRUD code are.
- I will publish real timings once I have measured them on paid runs, not before.

If you want to see it for yourself, [start with a prompt](/simple). The progress bar shows where in this sequence your job is.

— Steve
