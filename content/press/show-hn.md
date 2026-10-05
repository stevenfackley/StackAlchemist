# Show HN Submission Draft

**Status:** Draft — ready for review. Submit Tuesday 9:00am ET for best placement on the front page.

*Corrected October 4, 2026: earlier versions said generated repos include Supabase auth, Stripe billing, EF Core, shadcn/ui, a smoke-test run and GitHub Actions CI, and sold a $299 code tier; generated repos ship compile-verified scaffolding plus per-entity CRUD code, $299 buys docs only, and auth, payments and CI are yours to wire.*

---

## Title (80-char limit, aim for ≤60)

Primary (recommended):
> **Show HN: StackAlchemist – AI SaaS generator with a 100% compile guarantee**

Alternates (A/B if first falls flat):
- `Show HN: StackAlchemist – generate .NET + Next.js SaaS repos you own`
- `Show HN: I'm tired of AI codegen tools that ship broken zips, so I built this`
- `Show HN: Prompt → compiled, downloadable SaaS repo`

---

## URL
`https://stackalchemist.app`

---

## Body text (this goes in the first comment — HN shows it pinned at top)

Hey HN — I'm Steve Ackley, the founder of StackAlchemist.

I built this because I kept getting burned by AI code generators that ship you a tarball of code that has never been compiled. You download it, open it, run `dotnet build` or `pnpm install`, and nothing works. Half the imports don't resolve, the schema references columns that don't exist, the frontend calls API routes that were never generated.

StackAlchemist works differently. Before you can download the zip, our pipeline actually runs:

```
dotnet restore
dotnet build --no-restore
npm ci
npm run typecheck
npm run build      # next build
```

against the generated repo. If any of those fail, you never see a broken output — the compiler errors go back to the LLM, it patches, and the pipeline re-verifies, up to three repair attempts. If it still can't compile, you get a clear error and a refund, not a booby-trapped download.

The output is a full-stack repo:
- .NET 10 minimal API: Dapper over Npgsql, DI, Serilog logging, OpenAPI
- Next.js 16 App Router frontend with Tailwind, TypeScript types and a typed API client
- PostgreSQL schema as a SQL migration
- CRUD endpoints and pages for every entity in your schema
- Docker-compose for local dev
- One-time price, you own the code forever

Not in it: auth flows (the Supabase client is preinstalled, the flows are yours to write), Stripe (the entities have the fields, you wire billing), and CI. The compile gate runs on our side; `build-report.json` in the zip records every command and its exit code.

End to end it takes minutes: two LLM passes (the schema, then the per-entity CRUD code), a deterministic template render, and the compile gate. I have not measured enough paid runs to quote a number yet. We call the architecture the "Swiss Cheese Method" — deterministic templates for the 85% of code that shouldn't need creativity (project structure, routing, config, Docker), with LLM generation filling in the domain-specific holes.

A few opinionated choices worth flagging:
1. **Pricing is one-time, not subscription.** $299 for the architecture docs (Blueprint), $599 for the compile-verified repo (Boilerplate), $999 with AWS IaC on top (Infrastructure). You generate once, you own the repo forever. No seat rental, no usage meter. Subscriptions for full-app codegen only work if the average user underuses — that's a bundling trick, not a real pricing model.
2. **Backend is .NET, not Node.** WebContainers-based tools (Bolt.new) can't run a real backend runtime. We run generation jobs on ARM64 EC2 so we can compile real server code.
3. **No platform lock-in.** You download the zip, deploy wherever. We're a contractor, not a landlord.

We're pre-release beta, brand-new domain, zero marketing budget, zero backlinks. This is the actual first public post. Launching on HN before Product Hunt because I want engineer feedback before marketing feedback.

Things I'd love your thoughts on:
- **The Swiss Cheese Method** (deterministic templates + LLM logic) — is the boundary we draw the right one? Longer write-up: https://stackalchemist.app/blog/swiss-cheese-method-deterministic-templates-llm-logic
- **Compile guarantee** — does this seem like table stakes to you, or is the rest of the market right that speed matters more than verification? https://stackalchemist.app/blog/compile-guarantee-why-ai-codegen-must-verify
- **Pricing model** — does one-time pricing feel like a trap to you, or a feature?

Full landing page: https://stackalchemist.app
Pricing details: https://stackalchemist.app/pricing
Comparison with v0, Bolt.new, Lovable, Cursor: https://stackalchemist.app/blog/what-ai-code-generators-cant-do-yet

Happy to answer anything — I'll be here all day.

— Steve

---

## Submission checklist

- [ ] Confirm prod deploy is green and site is responsive under light load
- [ ] Confirm `/pricing` is reachable and accurate
- [ ] Confirm `/blog/compile-guarantee-why-ai-codegen-must-verify` is live
- [ ] Confirm `/blog/swiss-cheese-method-deterministic-templates-llm-logic` is live
- [ ] Confirm `/blog/what-ai-code-generators-cant-do-yet` is live
- [ ] Confirm CSP / CORS allow HN traffic (check referer handling)
- [ ] Plausible analytics live to measure launch-day traffic
- [ ] GSC sitemap submitted (so HN crawl surfaces in Search)
- [ ] Pre-write 5–10 likely-FAQ replies (see below) and have them ready to paste
- [ ] Do NOT submit from a fresh account — use a real HN account with comment history
- [ ] Submit Tuesday 9:00 AM ET (peak algorithmic window; avoids Monday meetings and Friday fatigue)
- [ ] First comment (body above) posted within 30 seconds of submission

## Anticipated FAQ replies (draft ahead of time)

**"How does this differ from v0?"**
> v0 generates a single React component. We generate a compile-verified full-stack repo (backend, DB, frontend, Docker). Auth and payments are still yours to wire. Different category. Long comparison here: https://stackalchemist.app/compare/v0

**"How does this differ from Bolt.new?"**
> Bolt runs everything in-browser WebContainers — no real backend runtime. We compile a .NET 10 API on our servers and deliver a downloadable zip. Bolt is for prototyping; we're for shipping. https://stackalchemist.app/compare/bolt-new

**"Why .NET and not Node?"**
> Because real SaaS backends with typed APIs and typed data access are still better in .NET than they're given credit for. Also: nobody else in the AI-codegen space does .NET, which is exactly why there's room for it. The frontend is Next.js 16.

**"Isn't one-time pricing leaving money on the table?"**
> The alternative is subscription, which only works if the average user underuses. That's a bundling model. Ownership is the thing customers actually want; we priced for it.

**"What if the compile gate fails?"**
> Pipeline retries up to 3 times with targeted error feedback to the LLM. If all 3 retries fail, you don't pay — you're refunded and we surface the error. You never see a broken zip.

**"Can I see example generated output?"**
> Yes — we publish a handful of example generations at /docs. Also: if you're considering this seriously, DM me and I'll share a live generation recording.

**"What LLM do you use?"**
> Claude Sonnet 5.5 for the schema and the per-entity code (or your own key: Anthropic, OpenAI, OpenRouter). The scaffolding is Handlebars templates, no LLM.

**"Is the code open source?"**
> The generated code is yours, under whatever license you choose. The engine (how we generate it) is proprietary. The templates are proprietary but can be inspected by paying customers.

**"Why should I trust a brand-new domain?"**
> You shouldn't, fully. Start with Spark (free) to see the flow, or Blueprint ($299) for the schema and API docs. The repo is Boilerplate ($599); if it doesn't compile, you get your money back. That's the deal.

**"Is this VC-funded?"**
> No. I bootstrapped. That's why the pricing is per-artifact — no investor is forcing me to chase subscription ARR.

## Post-launch follow-ups (prepare a small PR/Twitter thread for each)

- If HN front page: post a thread on X summarizing the discussion + linking to the post
- If top 3: consider a follow-up blog post titled "What HN taught me about StackAlchemist"
- Regardless of outcome: reply to every substantive comment within 48 hours — HN rewards founder engagement

## Signals to watch

- Upvotes in first 30 minutes (target: 15+)
- Comment-to-upvote ratio (high = controversial and engaging; target: 0.4+)
- Time on page from HN referrer (via Plausible)
- Signup conversion from HN referrer (Plausible → /register goal)
- Duration on front page (target: 4+ hours = meaningful exposure)

## Don'ts

- **Do not brigade.** Don't ask friends to upvote — HN flag-detects this aggressively and kills posts.
- **Do not be defensive.** When a commenter criticizes the product, acknowledge the point before clarifying.
- **Do not over-edit the body.** HN parses the first post as the definitive pitch; rewrites look flaky.
- **Do not run a concurrent Product Hunt launch.** HN folks read PH and vice versa; overlap looks desperate.
- **Do not hide the pricing.** State it early and plainly. HN has a finely-tuned BS detector.
