# Frequently Asked Questions

---

## General

### What is StackAlchemist?

StackAlchemist is an AI-powered architecture synthesis engine. You describe a SaaS application in natural language (or define it via a visual entity wizard), and StackAlchemist generates a complete, compiled, production-ready codebase — guaranteed to build on first try.

### Is it a no-code tool?

No. StackAlchemist generates real source code that you own and modify. It's a **code generation** tool, not a no-code platform. The output is standard .NET 10 and Next.js 16 — the same code an experienced developer would write by hand, just scaffolded automatically.

### What stack does StackAlchemist generate?

V1 generates (a FastAPI + React variant also exists, built and tested with its own Python toolchain):
- **Backend:** .NET 10 minimal API with Dapper and PostgreSQL (one project, not a solution)
- **Frontend:** Next.js 16 (App Router, TypeScript, Tailwind CSS) with a typed API client
- **Database:** PostgreSQL migration with UUID keys and row-level security enabled
- **Dev Environment:** Docker Compose + a multi-stage Dockerfile

No authentication flow is generated — the Supabase client and env slots ship preinstalled, the
wiring is yours. Additional stacks are planned for V2.

### Do I need an account to try it?

Yes, for anything past reading. The marketing pages, this FAQ, and the docs are public, but both
build modes are gated: `/simple` and `/advanced` redirect a signed-out visitor to sign-in, so the
account comes before the schema wizard, not after it. Accounts are Qavren Auth: email and password,
or Google, hosted at `auth.stackalchemist.app` (verification and password reset included). Starting a build needs one too — including
a free Spark build, which is limited to 5 per calendar month.

---

## Generation

### How long does generation take?

- **Simple schemas** (3–5 entities): ~30 seconds
- **Medium schemas** (6–10 entities): ~60 seconds
- **Complex schemas** (10+ entities): ~90 seconds

The status page refreshes itself every few seconds while the tab is open, so you see progress without reloading.

### What is the Compile Guarantee?

Every Boilerplate and Infrastructure package is run through the actual .NET and Next.js compilers before delivery:

1. `dotnet restore` and `dotnet build --no-restore` are executed against the .NET API
2. `npm ci`, `npm run typecheck` and `next build` are executed against the Next.js frontend
3. If either fails, the error output is fed back to the LLM and the failing files are regenerated
4. This retry loop runs up to **3 times**
5. If the build is still failing after 3 attempts: **a full refund is initiated automatically, no questions asked**
6. On success the archive gets a `build-report.json` naming every command, its exit code, and
   the verdict per half — the same verdict your delivery page shows

This is a hard technical constraint in the delivery pipeline — not a marketing claim.

### What happens if my generated code doesn't work?

If the code doesn't compile (build-time failure), you receive a full refund. If the code compiles but has a runtime bug, that's expected in any code — StackAlchemist guarantees compilation, not runtime correctness. Runtime issues are yours to debug and fix (it's your codebase).

### Can I regenerate with a different description?

Each generation is a separate purchase. If you want to try a different schema, submit a new generation. Refining your prompt in Simple Mode or adjusting entities in Advanced Mode before purchase is encouraged.

---

## Pricing and Licensing

### Is this a subscription?

No. Every tier is a one-time payment per generation. You pay once, you own the output forever.

### Can I use the generated code in a commercial product?

Yes. The generated code has no licensing restrictions. Use it, sell it, scale it — it's yours.

### What's the difference between the tiers?

| Tier | What you get |
|------|-------------|
| Spark (free) | A fixed demo app running in your browser. Not built from your description, not downloadable. |
| Blueprint ($299) | Architecture documents: `schema.json` and `api-docs.md` (the CRUD contract) |
| Boilerplate ($599) | The generated source built from your schema, both halves compiled, + Docker Compose |
| Infrastructure ($999) | Everything + AWS CDK, Terraform, Helm chart, deployment runbook |

See [Tiers and Pricing](./tiers-and-pricing) for full details.

### Do you offer refunds?

- **Compile Guarantee failure:** A full refund is initiated automatically the moment the 3rd retry fails — no request needed. It reaches your original payment method in 5–10 business days.
- **A refund you expected but did not get:** write to billing@stackalchemist.app. Per our Terms of Service, a Compile Guarantee refund can be requested within 14 days.

### Are there agency or volume discounts?

Contact us at [stackalchemist.app](https://stackalchemist.app) to discuss pricing for agencies, teams, or high-volume use.

---

## The Output

### Can I modify the generated code?

Yes. The generated code is standard .NET and Next.js. Modify it exactly as you would hand-written code.

### Does the generated code use any proprietary libraries or SDKs?

No. The generated code uses entirely open-source, mainstream dependencies:
- `.NET 10 / ASP.NET Core` — MIT
- `Dapper` — Apache 2.0
- `Next.js 16` — MIT
- `Tailwind CSS` — MIT
- `Supabase JS` — Apache 2.0

No StackAlchemist SDK or runtime dependency is injected into the generated code.

### Can I deploy the generated code anywhere?

Yes. The Boilerplate tier generates a Docker Compose setup that runs on any machine with Docker. The Infrastructure tier includes AWS CDK for cloud deployment, but the application itself can run on any cloud or on-premise environment.

### What database does it use?

PostgreSQL for the primary data store, reached with raw SQL via Dapper — not Entity Framework — so the database layer is lightweight and explicit. The Supabase client library and its env slots ship preinstalled in the frontend, but nothing uses them until you wire up auth yourself, and no auth code is generated. You can swap in any auth provider.

---

## Technical

### What AI model does StackAlchemist use?

Generation uses Claude Sonnet 5.5 by default. You can bring your own key and pick Claude Opus 5.5 (your own Anthropic key), Claude Haiku 4.5, OpenAI `gpt-6.1-sol` or OpenRouter `anthropic/claude-sonnet-5.5` in the dashboard API settings. File layout, class skeletons and import paths come from deterministic Handlebars templates, not the model.

### Does my data stay private?

Your schema and prompts are used solely to generate your architecture. They are not used to train any model. See our Privacy Policy for full details.

### What is the "Swiss Cheese Method"?

The Swiss Cheese Method is our approach to reliable code generation:
- **The cheese (structure)** — Handlebars templates define the deterministic outer structure: file layout, class signatures, import paths
- **The holes** — The LLM fills in the business-specific parts: query implementations, domain validation logic, custom endpoint bodies

See [The Swiss Cheese Method](./swiss-cheese-method) for a deep dive.

---

## Related Docs

- [Getting Started →](./getting-started)
- [Tiers and Pricing →](./tiers-and-pricing)
- [Troubleshooting →](./troubleshooting)
- [The Compile Guarantee (advanced) →](./compile-guarantee)
