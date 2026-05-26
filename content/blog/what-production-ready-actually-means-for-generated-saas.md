# What "production-ready" actually means for a generated SaaS

**By Steve Ackley · May 26, 2026 · 9 min read**

The single most overloaded phrase in AI-codegen marketing is "production-ready." Every tool in the space uses it. Most of the tools using it ship code that crashes on the first real request, refuses to deploy without a half-day of glue-fixing, or works locally and breaks the moment a second user touches it.

So I want to write down what production-ready actually means when the SaaS you're shipping was generated, not hand-written. Because if the word means nothing, the category is selling vapor. And I want StackAlchemist to be in the category that means something.

This post is the bar I hold StackAlchemist to. It is also the bar I think you should hold every AI-codegen tool to before you give them money.

## Production-ready is not "it compiled"

Let me start with the lowest bar, because it's the bar most tools secretly settle for.

"It compiled" means `npm run build` exited with code 0. It does not mean the build artifact runs. It does not mean a request to your API returns 200. It does not mean the database migration applied. It does not mean a user can log in. It does not mean your Stripe webhook signature verifies. It means a particular toolchain ran to completion on one machine at one moment in time.

StackAlchemist's compile guarantee is a floor, not a ceiling. We run `dotnet build` and `next build` before we let you download the zip. If either fails, we retry up to three times with the compiler errors fed back to the LLM. If it still fails after three retries, we refund you and tell you it failed. That is the floor.

But shipping a SaaS to production is a different bar, and confusing the two is how the AI-codegen category got its current reputation.

## The actual five-part bar

Here is the bar I hold StackAlchemist to. If you are buying any AI-codegen tool, hold them to this bar too. Make them prove it.

### 1. The generated repo runs in Docker on a fresh machine

`docker compose up` on a clean checkout. No manual steps. No "first run only" hacks. No `chmod +x` on the entrypoint. No "wait, you have to install Postgres globally first." The compose file spins up the database, runs the migrations, starts the API, starts the frontend, and you can hit `http://localhost:3000` in a browser within sixty seconds of `git clone`.

StackAlchemist generates `docker compose up`-ready output by default. Every generation. If yours doesn't, that's a bug worth a refund.

### 2. The database has migrations, not a SQL dump

Migrations are how a production database evolves. A SQL dump is how a starter project gets shipped by someone who hasn't thought past the first deploy.

The difference is real: with migrations, your team can change the schema next month, generate a new migration file, and roll forward (or back) cleanly. With a SQL dump, your schema is frozen on day one, and the second time you need to add a column, somebody has to remember which version was deployed and write the migration by hand from inference.

StackAlchemist generates EF Core migrations as `.cs` files alongside the entity definitions. Tests run against migrations. CI applies them in order. Day-two schema evolution is the path the generator already paved.

### 3. Authentication actually works against a real identity provider

A SaaS without working auth is a demo. A SaaS with a hand-rolled auth implementation is a security incident waiting to happen.

The bar is: the generator wires you into a real identity provider — Supabase, Auth0, Clerk, Cognito, your pick — with email/password, OAuth providers (Google, GitHub, Microsoft are table stakes), session management with HttpOnly cookies, CSRF protection, server-side session validation on every API call, password reset flows, and account deletion. You should not have to write a single security primitive yourself.

StackAlchemist defaults to Supabase Auth because it's the lowest-friction provider with a real free tier and a serious security posture. The middleware that validates the Supabase JWT on every API request is generated. The HttpOnly cookie config is generated. The CSRF posture is generated. If you want to swap providers later, that's a real day of work — but you start from real working auth, not from `// TODO: add auth`.

### 4. Payments are scaffolded against a real processor with webhook handling

The bar: Stripe integration with both the client-side checkout flow and the server-side webhook handler that verifies signatures and updates your database in response to Stripe events. Not just a "Pay with Stripe" button that POSTs to a TODO. Real webhook handling, with replay protection, idempotency keys, and the database state that Stripe is the source of truth for.

Webhooks are where most starter projects break. A Stripe Checkout session completes in the user's browser, then Stripe POSTs to your `/api/webhooks/stripe` endpoint, and your code is supposed to verify the signature, look up the corresponding order in your database, mark it paid, and trigger fulfillment. If any step in that chain is missing or broken, you charge the customer and never fulfill — the worst possible failure mode for a SaaS.

StackAlchemist generates webhook handling for `checkout.session.completed`, `invoice.payment_succeeded`, `customer.subscription.updated`, `customer.subscription.deleted`, and a few others, with signature verification using the Stripe library, idempotency via the Stripe event ID as a primary key in a `processed_webhook_events` table, and database transactions that wrap the state update so partial failures don't leave the database in a bad state. This is the unsexy correctness work that determines whether your SaaS actually takes money correctly.

### 5. The CI pipeline runs the compile gate on every push

This is the part that compounds. If your generated repo doesn't ship with CI that compiles, lints, type-checks, and runs tests on every push, then the first regression you introduce next week is the regression that ships to production silently.

StackAlchemist generates a GitHub Actions workflow that runs `dotnet build`, `dotnet test`, `pnpm typecheck`, `pnpm lint`, and `pnpm build` on every push and pull request. If any of those fail, the workflow fails. Your repo defaults to a "main branch is always green" posture from day one. That posture is hard to retrofit and easy to establish, which is exactly why we establish it for you.

## What "production-ready" emphatically does not include

I want to be honest about what the bar does not include, because being honest about scope is how a real category establishes itself.

**Multi-region deployment.** You get a single-region Docker artifact. Going multi-region is a real engineering project that depends on your traffic shape, your latency requirements, and your budget. We don't pretend to generate it.

**Horizontal autoscaling.** The generated infra is sized for a single VM by default. If you want Kubernetes autoscaling, you're at the Infrastructure tier, and even there you get the cluster manifests, not a guarantee of optimal scaling under your specific load.

**A complete observability stack.** You get structured logs, basic health checks, and an `/api/healthz` endpoint that load balancers can use. You do not get a Grafana dashboard, an OpenTelemetry trace exporter, or a paged-out alerting setup. Those are decisions that depend on which observability vendor you've committed to and what your SLO posture is.

**Compliance certifications.** SOC 2, HIPAA, PCI DSS Level 1 — these are operational programs, not codebase features. The generated code is consistent with these certifications (no PII in logs, real auth, real payment handling) but the certification itself is months of audit work.

**Disaster recovery beyond Postgres backups.** The Infrastructure tier includes daily backups and point-in-time recovery via the cloud provider's managed Postgres. Cross-region replication, RPO/RTO targets below an hour, runbook for full-region failure — those are work the buyer takes on once they own the code.

The honest framing is: StackAlchemist gets you to "production-ready" in the sense that you can take payments from real customers without lying to them about security or losing their data. It does not get you to "Series A SaaS at scale." That second mile is the user's, and we are clear about that.

## How most AI-codegen tools fail the bar

I want to name some specific failure modes I have seen from the tools in this space, so the bar is concrete and not theoretical.

**The "generated code, no Docker" failure.** v0 generates a Next.js component that you copy-paste into your existing app. The "production-ready" bar does not apply — there's no app being generated. v0 is a different category.

**The "in-browser sandbox" failure.** Bolt.new generates an app that runs in their hosted WebContainer. Production-ready in Bolt means "production-ready in Bolt." Migrating it to your own infrastructure is a separate engineering project. The compile guarantee, if it exists, is only valid in the sandbox.

**The "auth is a TODO" failure.** Many starter generators ship `const user = { id: 1 }` as a placeholder and call the auth implementation a feature. It is not. It is a security incident wearing a CTO costume.

**The "Stripe is just a button" failure.** A Stripe checkout button is the easy part. A webhook handler that reconciles state correctly is the actual integration. I have seen multiple generated codebases where the webhook handler is a TODO comment.

**The "works on my CI" failure.** A generator that compiles its output on its own CI but doesn't run that same CI on the user's repo after delivery is giving you a one-time pass and hoping you don't regress. CI on the buyer's repo is what makes the bar continuous, not pointlike.

If a tool can't pass the bar on any of these, they're not production-ready in the meaningful sense, even if they say they are.

## Why this matters for the category

The AI-codegen category right now is in the part of the hype cycle where every tool claims to solve every problem. The shakeout will be: which tools were actually production-ready, and which were demo-ready.

The tools that were only demo-ready will lose users when those users try to ship and hit the missing-auth or missing-webhooks or missing-CI walls. The tools that were actually production-ready will be the boring ones that kept saying "yes but" to the marketing-friendly claims.

I want StackAlchemist to be a boring one. Boring is what production means. Boring is what your customers expect. Boring is what does not crash at 3am.

## Key takeaways

- **"Production-ready" is meaningless without a checkable bar.** I propose the five-part bar in this post: Docker-runs, real migrations, real auth, real Stripe webhooks, real CI.
- **The compile guarantee is the floor, not the ceiling.** Code that compiles is necessary but not sufficient. Production-ready is a higher bar.
- **Honesty about scope is what separates a serious tool from a marketing tool.** StackAlchemist generates production-ready output as defined here. It does not generate a fully-operated Series A SaaS. Anyone claiming otherwise is selling vapor.
- **Hold the AI-codegen tool you are evaluating to this bar.** Make them prove the five things. The honest answers will sort the category.

If you're evaluating StackAlchemist against this bar, [start a generation](/simple) and run it through the checklist. If you find a place we don't meet the bar, that's a bug worth telling me about.

— Steve
