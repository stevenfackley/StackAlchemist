# Frequently Asked Questions

---

## General

### What is StackAlchemist?

StackAlchemist is an AI-powered architecture synthesis engine. You describe a SaaS application in natural language (or define it via a visual entity wizard), and StackAlchemist generates a complete, compiled, production-ready codebase — guaranteed to build on first try.

### Is it a no-code tool?

No. StackAlchemist generates real source code that you own and modify. It's a **code generation** tool, not a no-code platform. The output is standard .NET 10 and Next.js 15 — the same code an experienced developer would write by hand, just scaffolded automatically.

### What stack does StackAlchemist generate?

V1 generates:
- **Backend:** .NET 10 Web API with Dapper ORM and PostgreSQL
- **Frontend:** Next.js 15 (App Router, TypeScript, Tailwind CSS)
- **Database:** PostgreSQL with Supabase auth
- **Dev Environment:** Docker Compose

Additional stacks are planned for V2.

### Do I need an account to try it?

You can explore the interface and define your schema without an account. An account is required at the point of purchase and download.

---

## Generation

### How long does generation take?

- **Simple schemas** (3–5 entities): ~30 seconds
- **Medium schemas** (6–10 entities): ~60 seconds
- **Complex schemas** (10+ entities): ~90 seconds

You'll see real-time progress updates throughout.

### What is the Compile Guarantee?

Every Boilerplate and Infrastructure package is run through the actual .NET and Next.js compilers before delivery:

1. `dotnet build` is executed against the .NET API
2. `npm run build` is executed against the Next.js frontend
3. If either fails, the error output is fed back to the LLM and the failing files are regenerated
4. This retry loop runs up to **3 times**
5. If the build is still failing after 3 attempts: **full refund, no questions asked**

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
| Blueprint ($299) | Architecture documents: schema, OpenAPI spec, SQL, data flow diagram |
| Boilerplate ($599) | Full compiled source code + Docker Compose |
| Infrastructure ($999) | Everything + AWS CDK, Helm charts, CI/CD pipeline, deployment runbook |

See [Tiers and Pricing](./tiers-and-pricing) for full details.

### Do you offer refunds?

- **Compile Guarantee failure:** Automatic full refund if the generated code fails to compile after 3 retries.
- **Other refunds:** Contact us within 24 hours of purchase if you have a legitimate issue.

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
- `Next.js 15` — MIT
- `Tailwind CSS` — MIT
- `Supabase JS` — Apache 2.0

No StackAlchemist SDK or runtime dependency is injected into the generated code.

### Can I deploy the generated code anywhere?

Yes. The Boilerplate tier generates a Docker Compose setup that runs on any machine with Docker. The Infrastructure tier includes AWS CDK for cloud deployment, but the application itself can run on any cloud or on-premise environment.

### What database does it use?

PostgreSQL for the primary data store. Supabase provides auth, real-time subscriptions, and storage on top of PostgreSQL. The generated code uses raw SQL via Dapper — not Entity Framework — so the database layer is lightweight and explicit.

---

## Technical

### What AI model does StackAlchemist use?

The generation engine uses Claude 3.5 Sonnet for business logic injection. The structural scaffolding (file layout, class skeletons, import paths) is handled by deterministic Handlebars templates — not the LLM.

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
