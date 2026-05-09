# Show HN — StackAlchemist

**Author:** Steve Ackley
**Status:** Draft v0 — needs founder voice pass + final title pick before submission
**Submission target:** stackalchemist.app
**Last updated:** 2026-05-09

> HN style notes: no "Hi HN", no emojis, no marketing voice. First-person. Optimal body length ~150–300 words. Be ready to engage in comments for 6+ hours after submission.

---

## Title — pick one before submission

HN strips and lowercases noise; aim for ≤80 chars. Three variants:

1. `Show HN: StackAlchemist – generate a verified .NET 10 + Next.js SaaS from a prompt`
2. `Show HN: StackAlchemist – AI codegen that fails the build instead of shipping broken code`
3. `Show HN: I built an AI codegen that refuses to hand you code that doesn't compile`

<!-- TODO(steve): Pick one, or write your own. Variant 3 is most "founder voice" but starts with "I" which some find self-promotional. Variant 1 is most descriptive. Variant 2 leads with the differentiator (compile guarantee). -->

## URL

`https://stackalchemist.app`

## Body draft

<!-- TODO(steve): The opening line is the most important sentence on the page.
     Replace the paragraph below with a one-liner only you can write —
     something that sounds like *you* in a room with engineers, not a brochure.
     Suggested angle: the moment you decided current AI codegen was broken. -->

I'm building StackAlchemist, an AI generator that produces a full .NET 10 + Next.js 15 SaaS from a single prompt — and refuses to let you download anything that doesn't compile.

The thing that pushed me to start: every AI codegen tool I tried in 2025 was happy to hand me code with missing imports, undefined types, or hallucinated APIs and call it shipped. v0 makes beautiful UI but stops at the component. Bolt.new gives you a runnable preview but in their sandbox. Lovable, Replit Agent — same shape: generate fast, ship broken, hope you fix it.

So I built around two ideas:

1. **The Swiss Cheese Method.** Static, deterministic Handlebars templates produce the scaffolding (project structure, auth, Stripe, Supabase wiring, CI). The LLM only fills in business-logic-shaped holes — entity classes, repository methods, route handlers. Templates can't hallucinate a missing import; the LLM can only see the holes, not the load-bearing structure.
2. **Compile guarantee.** Every generation runs `dotnet build` and `next build` before the user sees it. If the build fails, the LLM sees the compiler error and patches. If it still fails after N retries, the run errors instead of shipping you broken code. You never download something that doesn't compile.

Tech: Claude Sonnet 4.6 for generation, .NET 10 Web API + Next.js 15 (App Router) + Supabase + Stripe in the output, R2 for the artifact zip. One-time price, owned code, deploy anywhere — not a hosted runtime.

<!-- TODO(steve): Add 1–2 sentences on a real limitation. HN respects honesty
     here more than anywhere. Candidates:
     - "It's slow — 8 to 12 minutes per generation, vs Bolt's sub-minute prototype."
     - "The output is opinionated about stack — if you don't want .NET, this isn't for you."
     - "Multi-tenant isolation is hand-rolled per-tenant; I haven't yet generated row-level security policies that I'd trust under audit."
     Pick the one that's actually true and that you're willing to defend. -->

What I'd love feedback on: the Swiss Cheese Method as a category. Is "templates + LLM-filled holes" something other people have built and called something else, or is the framing useful as-is? And — for anyone who's tried v0/Bolt/Lovable on a real production attempt — what made you bounce off them, and would a compile guarantee have changed it?

## Pre-staged comments (to drop after the post lands)

These go in *as comments* on the post, not in the post body — HN penalizes long submissions but the comment thread is where the conversation actually happens.

### Comment 1 — demo / screenshot reply

<!-- TODO(steve): Stage one short clip or screenshot of the generation flow,
     compile gate firing, and the resulting download. ~30s max. Host on
     stackalchemist.app/launch/demo.mp4 or similar so you control the URL. -->

### Comment 2 — "what's it cost"

`One-time $X for a generation. No seats, no monthly. The output is yours — the repo, the Docker compose, the migrations. I deliberately didn't build a hosted runtime; HN comments will rightfully ask, and the answer is that I didn't want the business model to depend on locking up your code.`

<!-- TODO(steve): Fill in the actual price — pull current value from /pricing. -->

### Comment 3 — "why .NET"

`Because templates need to be static, type-safe, and AOT-friendly. A scaffold that compiles to native and survives a missing import without nondeterminism gives the LLM smaller, sharper holes to fill. Node would have worked but with worse error messages, which matter when the compile gate is the load-bearing piece.`

### Comment 4 — anticipated push-back: "isn't this just a glorified template"

`Templates alone produce identical output for every user — useless. Pure LLM produces unique output that doesn't compile — also useless. The Swiss Cheese Method is the specific claim that the *seam* between template and LLM is where the compile guarantee lives, and that designing the seam correctly is the work.`

## Pre-launch checklist

- [ ] stackalchemist.app loads in <2s on a cold visit (HN traffic is brutal)
- [ ] /pricing shows the real one-time price (anticipated comment 2)
- [ ] /compare/v0, /compare/bolt-new are public and reachable (HN will click through)
- [ ] Demo video staged at a stable URL
- [ ] Plausible verified so we can actually measure HN traffic
- [ ] [Issue tracker / contact email visible from the homepage] for feedback DMs
- [ ] Founder is genuinely available for 6 straight hours after submit (HN is real-time)

<!-- TODO(steve): Pick a submission window. HN front-page traffic peaks
     8am–11am US Pacific on weekdays. Avoid major conference weeks. -->

## What I (Claude) want from you (Steve) before this ships

1. **Pick a title** — one of the three above or your own.
2. **Replace the opening paragraph** with the one-liner only you can write. The current draft is descriptive; yours should be human.
3. **Pick the honest limitation** to call out (the second TODO comment in the body).
4. **Fill the price + demo URL** in the pre-staged comments.
5. **Decide submission timing** and put it on the calendar.

Once those five are in, this is shippable. Everything else is sandpaper.
