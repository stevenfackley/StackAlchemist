# Show HN Submission Draft

**Status:** Draft — ready for review. Submit Tuesday 9:00am ET for best placement on the front page.

---

## Title (80-char limit, aim for ≤60)

Primary (recommended):
> **Show HN: StackAlchemist – AI SaaS generator with a 100% compile guarantee**

Alternates (A/B if first falls flat):
- `Show HN: StackAlchemist – generate .NET + Next.js SaaS repos you own`
- `Show HN: I'm tired of AI codegen tools that ship broken zips, so I built this`
- `Show HN: Prompt → compiled, downloadable SaaS repo in 12 minutes`

---

## URL
`https://stackalchemist.app`

---

## Body text (this goes in the first comment — HN shows it pinned at top)

Hey HN — I'm Steve Ackley, the founder of StackAlchemist.

I built this because I kept getting burned by AI code generators that ship you a tarball of code that has never been compiled. You download it, open it, run `dotnet build` or `pnpm install`, and nothing works. Half the imports don't resolve, the schema references columns that don't exist, the frontend calls API routes that were never generated.

StackAlchemist works differently. Before you can download the zip, our pipeline actually runs:

```
dotnet build
dotnet test --filter Category=Smoke
pnpm --filter web build
pnpm --filter web typecheck
```

against the generated repo. If any of those fail, you never see a broken output — the pipeline retries with targeted error feedback to the LLM, re-generates, and re-verifies. If it can't fix it within the retry budget, you get a clear error, not a booby-trapped download.

The output is a full-stack SaaS:
- .NET 10 Web API with EF Core, DI, logging, test scaffolding
- Next.js 15 App Router frontend with shadcn/ui
- PostgreSQL schema + migrations
- Supabase auth wired
- Stripe billing wired
- Docker-compose for local dev
- GitHub Actions CI with the compile gate built in
- Everything under your own LICENSE — one-time price, you own the code forever

It takes ~12 minutes end to end. About 5 of those minutes are LLM work (domain modeling, business logic). The rest is deterministic template rendering + the compile gate. We call the architecture the "Swiss Cheese Method" — deterministic templates for the 85% of code that shouldn't need creativity (auth, routing, CI, Docker), with LLM generation filling in the domain-specific holes.

A few opinionated choices worth flagging:
1. **Pricing is one-time, not subscription.** $299 / $599 / $999 per generation by tier. You generate once, you own the repo forever. No seat rental, no usage meter. Subscriptions for full-app codegen only work if the average user underuses — that's a bundling trick, not a real pricing model.
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
> v0 generates a single React component. We generate a full-stack SaaS (backend, DB, auth, payments, CI). Different category. Long comparison here: https://stackalchemist.app/compare/v0

**"How does this differ from Bolt.new?"**
> Bolt runs everything in-browser WebContainers — no real backend runtime. We compile a .NET 10 API on our servers and deliver a downloadable zip. Bolt is for prototyping; we're for shipping. https://stackalchemist.app/compare/bolt-new

**"Why .NET and not Node?"**
> Because real SaaS with typed APIs, EF Core, and background workers is still better in .NET than it's given credit for. Also: nobody else in the AI-codegen space does .NET, which is exactly why there's room for it. The frontend is Next.js 15.

**"Isn't one-time pricing leaving money on the table?"**
> The alternative is subscription, which only works if the average user underuses. That's a bundling model. Ownership is the thing customers actually want; we priced for it.

**"What if the compile gate fails?"**
> Pipeline retries up to 3 times with targeted error feedback to the LLM. If all 3 retries fail, you don't pay — we return the credit and surface the error. You never see a broken zip.

**"Can I see example generated output?"**
> Yes — we publish a handful of example generations at /docs. Also: if you're considering this seriously, DM me and I'll share a live generation recording.

**"What LLM do you use?"**
> Claude 3.5 Sonnet for business logic generation. The scaffolding is Handlebars templates, no LLM.

**"Is the code open source?"**
> The generated code is yours, with your LICENSE. The engine (how we generate it) is proprietary. The templates are proprietary but can be inspected by paying customers.

**"Why should I trust a brand-new domain?"**
> You shouldn't, fully. Start with Simple-mode ($299) as a low-stakes test. If it doesn't compile, you get your money back. That's the deal.

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
