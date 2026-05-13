# Show HN — StackAlchemist

**Author:** Steve Ackley
**Status:** Draft v2 — submission-ready text, awaits only (a) 30s demo video uploaded to the staged URL, and (b) Plausible verification live in prod.
**Submission target:** stackalchemist.app
**Last updated:** 2026-05-13

> HN style notes: no "Hi HN", no emojis, no marketing voice. First-person. Optimal body length ~150–300 words. Be ready to engage in comments for 6+ hours after submission.

---

## Title

`Show HN: I built an AI codegen that refuses to hand you code that doesn't compile`

## URL

`https://stackalchemist.app`

## Body

I finally started StackAlchemist after realizing every AI tool on the market was perfectly content handing me missing imports and calling the job done.

v0 makes beautiful UI but stops at the component. Bolt.new gives you a runnable preview but in their sandbox. Lovable, Replit Agent — same shape: generate fast, ship broken, hope you fix it.

So I built around two ideas:

1. **The Swiss Cheese Method.** Static, deterministic Handlebars templates produce the scaffolding (project structure, auth, Stripe, Supabase wiring, CI). The LLM only fills in business-logic-shaped holes — entity classes, repository methods, route handlers. Templates can't hallucinate a missing import; the LLM can only see the holes, not the load-bearing structure.
2. **Compile guarantee.** Every generation runs `dotnet build` and `next build` before the user sees it. If the build fails, the LLM sees the compiler error and patches. If it still fails after N retries, the run errors instead of shipping you broken code. You never download something that doesn't compile.

Tech: Claude Sonnet 4.6 for generation, .NET 10 Web API + Next.js 15 (App Router) + Supabase + Stripe in the output, R2 for the artifact zip. One-time price, owned code, deploy anywhere — not a hosted runtime.

The output is opinionated about stack — if you don't want .NET, this isn't for you.

What I'd love feedback on: the Swiss Cheese Method as a category. Is "templates + LLM-filled holes" something other people have built and called something else, or is the framing useful as-is? And — for anyone who's tried v0/Bolt/Lovable on a real production attempt — what made you bounce off them, and would a compile guarantee have changed it?

## Pre-staged comments (to drop after the post lands)

These go in *as comments* on the post, not in the post body — HN penalizes long submissions but the comment thread is where the conversation actually happens.

### Comment 1 — demo / screenshot reply

`30-second demo of a full run: prompt in, generation, compile gate firing, downloadable repo out. https://stackalchemist.app/launch/demo.mp4`

<!-- TODO(steve, pre-submit only): Record the actual 30s clip and upload it to that exact URL. Comment text above is finalized — only the video file needs to land at the URL before submission. -->


### Comment 2 — "what's it cost"

`$599 one-time, no seats, no monthly. That's the Boilerplate tier — the full compiled .NET + Next.js repo with the compile guarantee. $299 if you only want the architecture docs (Blueprint), $999 if you want IaC + Helm + CI on top (Infrastructure). The output is yours — the repo, the Docker compose, the migrations. I deliberately didn't build a hosted runtime because I didn't want the business model to depend on locking up your code.`

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

**Submission window:** 8am–11pm EST, weekdays. (Note: HN front-page hits peak ~9–11am ET / 6–8am PT — earlier in the window is stronger. Avoid major conference weeks.)

## Remaining gates

All text is final. Only two non-text items left, both tracked in the pre-launch checklist above:

1. **Demo video file** — Record ~30s clip (prompt → generation → compile gate firing → download) and upload to `https://stackalchemist.app/launch/demo.mp4`. The comment copy referencing that URL is already finalized; only the asset needs to land.
2. **Plausible verification live in prod** — External dependency. The HN traffic spike is unmeasured otherwise.

**Resolved 2026-05-13:**
- Stripe reconciliation — "Stripe Payments Integration" added to Boilerplate tier items and to the comparison matrix in `src/StackAlchemist.Web/src/app/pricing/page.tsx`. Body claim and /pricing now agree.
