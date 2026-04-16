# Product Hunt Hunter — Strategy & Candidate List

**Status:** Strategic plan for finding a PH hunter. User has no hunter lined up as of 2026-04-16.

## Why a hunter matters (short version)

A "hunter" on Product Hunt is the person who submits (hunts) your product on launch day. They are not required — you can self-hunt — but a well-connected hunter multiplies day-one upvote velocity, which controls whether you make the daily top-5.

For a brand-new domain with zero organic PH presence like StackAlchemist, a hunter is ~4-10× the launch-day reach compared to self-hunting.

## What makes a good hunter for us

Traits to prioritize (in order):

1. **AI / dev-tool credibility.** The audience for StackAlchemist is engineers and indie founders. A hunter whose PH profile is known for AI dev-tool launches carries far more weight than a general-purpose hunter.
2. **Recent activity on PH.** A hunter with 0 hunts in the last 6 months has a cold audience and poor reach. Target hunters with 3+ hunts in the last 90 days.
3. **Warm audience that overlaps ours.** Their past hunts should have ranked in the top-10 on launch day. Hunter reach = hunter's followers who turn up when they post.
4. **Willing to engage in the launch.** A hunter who just submits and disappears is worth ~30% of one who posts comments, answers questions, and drives traffic from their own channels.
5. **Not over-extended.** If they hunted 3 products last month, PH's algorithm dampens their reach. Target hunters with moderate cadence.

## Candidate categories (who to approach)

### Category A: AI / dev-tool-focused hunters
These are the bullseye — their audience is our audience.

- **Chris Messina** (@chrismessina on PH) — Legendary PH hunter, invented the hashtag. Hunts broadly but has strong dev-tool chops. Busy/expensive, but if we can land him the launch is made.
- **Kevin William David** (@kevinwilliamdavid) — Hunted hundreds of products, AI-focused lately, still active.
- **Ben Lang** (@benln) — Notion alum, hunts indie products, warm dev community reach.
- **Jiaona "JZ" Zhang** (@jiaona) — Product leadership audience with heavy dev-tool overlap.

### Category B: Indie-hacker / founder-hunters
These hunters have built their own products and hunt only things they genuinely like — smaller reach but higher-quality audience.

- Hunters whose own top products have been dev-tools or AI-adjacent. Search PH for "top maker" in AI category last 6 months.
- Active indie hackers on IndieHackers.com who also maintain PH profiles — their audience crossover is high.

### Category C: Your warm network
Often overlooked. If someone you have actually worked with has a decent PH profile, they are a more reliable hunter than a stranger with a bigger audience. Warm + medium > cold + big.

Action for user: comb LinkedIn and past engineering circles for anyone with a PH profile + 100+ followers. We should rank these candidates against Category A before deciding.

## How to reach out (email / DM template)

Short, specific, respectful of their time:

> Subject: Quick question — would you hunt StackAlchemist?
>
> Hey [Name],
>
> I'm Steve Ackley, founder of StackAlchemist — the AI SaaS generator with a compile guarantee ([stackalchemist.app](https://stackalchemist.app)). We ship full-stack .NET + Next.js repos that are verified-compiled before download. No broken zips, no subscriptions, you own the code.
>
> I've been following your hunts (especially [specific recent hunt, 1 line on why it resonated]), and I'd be honored if you'd consider hunting us.
>
> Launch date: [date]. We'd prep a full press kit: demo video, gallery assets, competitive positioning, and a live launch-day war room so you're not answering comments alone.
>
> If it's a yes or a maybe, happy to share the full launch plan. If it's a no, no hard feelings — thanks for the consideration either way.
>
> — Steve

**Don't:**
- Send a generic blast. Hunters get dozens of requests per week and filter the cold ones instantly.
- Lead with "will you hunt my product" without a hook.
- Undersell — state the differentiator (compile guarantee, own the code, full-stack .NET) concretely.

**Do:**
- Mention one specific recent hunt of theirs (signals you actually know them).
- State a launch date (commitment seriousness).
- Offer to prep the press kit and assets (reduce their effort).
- Give them a graceful out.

## Alternative: the "find a hunter" marketplaces

If cold outreach to Category A comes up empty, there are marketplaces / services specifically for finding hunters. These are hit-or-miss:

- **Ship** (ship.producthunt.com) — PH's own pre-launch tool. Free; builds a subscriber list pre-launch that you can tap on launch day. Lower leverage but free.
- **Paid hunter marketplaces** — existed in 2022-2023, largely reputation-dead in 2026. Avoid.
- **Twitter/X DMs** to top hunters — some respond, most don't. Same principles as the email template.

## Fallback: self-hunt with strong pre-launch

If no hunter lands by 2 weeks before target date, self-hunt. Mitigate with:

- **Ship page live ≥30 days before launch** — grows subscriber list; they get notified on launch day.
- **X/Twitter thread at 8:55am** on launch day — drives first-hour upvotes from your own audience.
- **Email all subscribers at T-0** — launch-day email is the single highest-ROI touchpoint.
- **Do a simultaneous HN Show HN** — see `show-hn.md` draft. HN traffic → PH signal loop.
- **Rally IndieHackers, Reddit /r/SaaS, /r/SideProject** in the morning of launch.

Self-hunt caps us at maybe top-10 daily if pre-launch is strong. Top-5 effectively requires a hunter.

## Recommended action plan

**This week (week of 2026-04-16):**
- [ ] User picks 5 Category A + 5 Category B candidates from the lists above.
- [ ] Send personalized outreach to Top 3 candidates using the template.
- [ ] Stand up PH "Ship" page — start building a subscriber list.
- [ ] Draft press kit assets (90-sec demo video, 3 gallery images at 1270×760, launch-day copy).

**Next 2 weeks:**
- [ ] Hear back from initial 3. If all pass, move to next 3.
- [ ] Continue building Ship subscribers (target: 200+ before launch).
- [ ] Write launch-day X thread (draft + schedule).

**Decision point at week 4 (2026-05-14):**
- If a hunter has committed → set firm launch date 2 weeks out.
- If no hunter → self-hunt with full pre-launch prep; schedule for a Tuesday at least 2 weeks after the Show HN launch (avoid overlap).

## What we still need from the user

- [ ] Make a candidate list from their warm network (Category C) — nobody else can do this research.
- [ ] Commit to a launch date range so outreach emails can state a specific date.
- [ ] Approve the outreach email template above or request edits.
- [ ] Confirm the 90-sec demo video can be produced in time.

## Sanity checks before launch

- [ ] Prod site handles Slashdot-scale traffic (light load testing done)
- [ ] Signup flow is friction-free from PH referrer
- [ ] Pricing page is clear (PH audiences are pricing-sensitive and detect hidden fees)
- [ ] Comparison pages live (/compare/v0, /compare/bolt-new — PH visitors will ask about alternatives)
- [ ] Plausible analytics segmented by PH referrer to measure launch-day conversion
