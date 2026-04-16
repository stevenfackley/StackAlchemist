# Why we charge once, not monthly: the economics of AI-generated SaaS

**By Steve Ackley · April 28, 2026 · 7 min read**

Every AI code generator in the market is a monthly subscription. v0 is seat-based. Bolt is usage-credits that roll over until they don't. Lovable is tiered monthly. Cursor is $20/month. Replit is usage + seat.

We are not. StackAlchemist is one-time. $299, $599, or $999, you pay once, you own the generated code forever, and if you want another generation later you pay for that one too. No rental. No seat. No per-run meter.

I get asked about this pricing choice more than any other decision in the product. Some people think it is a marketing gimmick. It is not. It is the only honest way to price this product, and in this post I am going to show you the math.

## The story every subscription tool tells

The standard pitch for AI-codegen subscriptions goes like this: "You build with us monthly because AI is expensive. You need ongoing generations as you iterate. The subscription gives you predictable access."

The second half of that pitch is true for tools that are assisting you in an ongoing codebase — Cursor fits here, and I do not argue with Cursor's pricing. You do use Cursor every day, and the value compounds.

The first half of the pitch — "AI is expensive, so you need a subscription" — is the part that falls apart under inspection when the tool is generating a full app, not editing one.

Here is the truth: generating a full-stack SaaS repo with StackAlchemist costs us somewhere between $3 and $18 in LLM tokens, depending on how complex the domain is and how many retries the compile gate forced. Call it $10 average. Add some infra cost for the build runners (compiling .NET + Next.js takes real compute), call it another $3 per generation. We are around $13 cost per generation.

If we charged you $20/month subscription for unlimited generations, and you generated once per quarter, we would make $60/quarter from you and pay $13 to fulfill it. Nice margins.

But the people actually paying for subscriptions are the people generating often. And the people generating often are paying $20/month to generate dozens of times. At five generations per month, we are at $65 in cost per month against $20 in revenue. We lose money. The only way the subscription math works is to aggressively rate-limit, which is exactly what every subscription-based tool in this space does once you dig in.

The subscription pricing model for codegen is not really a pricing model. It is a bundling model that relies on most users underusing. That is not a product I want to build.

## The one-time pricing math

Here is what actually honest pricing looks like for a generator.

A Simple-mode generation (our $299 tier) costs us about $8 in LLM and infra. Gross margin at $299: $291, which is ~97%.

A Blueprint-tier generation ($599) allows more complexity, tighter integrations, longer prompts. Cost rises to ~$14. Gross margin: $585, ~98%.

A Boilerplate-tier generation ($999) includes more entities, more vertical-specific tuning, and a higher retry budget on the compile gate. Cost ~$22. Gross margin: $977, ~98%.

An Infrastructure-tier engagement (the highest tier, currently quoted per project) is less about automated generation and more about custom scaffolding. Margins vary by engagement.

Across the three self-serve tiers, our gross margins are in the mid-to-high 90s. That is not by accident — it is because we only charge you when we actually do the work, and we do the work quickly and verifiably.

## Why the one-time model is better for you

Let's flip it and look at the deal from the buyer's side.

**Scenario A: Subscription model at $30/month (hypothetical standard.)**

You want to generate a SaaS to validate an idea. You subscribe for one month, generate, cancel. Cost: $30.

Six months later you have a second idea. You subscribe again, generate, cancel. Cost: $30.

Over a year you subscribe for 4 months (validate 4 ideas). Total: $120.

So far, one-time vs subscription is about even at this volume — until you account for the fact that with a subscription, you do not own the generated code. It ships with your subscription to the platform. If the platform raises prices, changes terms, or shuts down, your relationship with the code is at risk.

**Scenario B: StackAlchemist one-time at $299.**

You pay once. You own the code. It is yours, on disk, in your GitHub, in your LICENSE. If StackAlchemist shuts down tomorrow, your generated SaaS is unaffected. There is no dependency on us after the handoff.

The one-time price is higher per generation, obviously. But you are paying for ownership, not rental. For a serious founder shipping a product, that is the correct trade.

**Scenario C: you are generating five SaaS apps a year.**

At one-time $299 × 5 = $1495. At subscription $30/month × 12 = $360.

Subscription wins on raw cost, yes. But:

- Subscription tools are rate-limited — "five generations a year" likely means bumping into usage caps.
- Subscription tools are almost always hosted — your generated apps run on the tool's infra, not yours.
- Subscription tools quietly shift the unit economics: the provider needs you to generate less than the average subscriber to be profitable.

If you are doing serious work, you are going to hit the walls those tools are designed around.

## The philosophical point

I am going to be direct. The subscription model for full-app code generators exists because VCs love recurring revenue and founders are taught that recurring revenue is the only good revenue. That is a fashion, not a principle.

Here is the principle I actually believe: **you should own your stack.** The code you ship to your customers, the database your customers' data sits in, the API your mobile app calls — those should all be under your control. A generator that couples you to a monthly subscription to keep the lights on is not a tool, it is a landlord.

I chose the one-time model because I want StackAlchemist to be a tool you use and move on from, the same way you would use a contractor to frame a house and then own the house. If you come back for another project, great. If you do not, also great. The product does not depend on a captive subscriber base.

This works because our unit economics work. Charging once at mid-90s% margin on a job that takes 12 minutes is a sustainable business. It is not a hype-growth VC-backed business. It is a real business.

## The hidden cost of subscriptions I rarely see discussed

There is one more cost nobody talks about: the switching cost of leaving a subscription tool.

If your hosted app on Lovable is making you money and you want to migrate to your own infra, you have to re-implement the scaffolding yourself, re-wire the auth, re-do the deployments. That is days of work, minimum. Often weeks. That switching cost is deliberate — it is how subscriptions retain you.

With a one-time model where you own the code from day one, there is no switching cost. You are already in possession. That is not a side effect of our pricing; it is the point.

## What you are actually paying for

When you pay $299 for a Simple-mode generation, here is what you are getting:

- The generation run itself: LLM calls, template rendering, compile gate, retries.
- The templates that encode years of "how do you wire Supabase + Stripe + .NET + Next.js without it turning into a mess."
- The compile gate that means the code you download actually runs.
- Ownership — clean, transferable, no strings.

You are not paying for "access to our platform." You are paying for a finished product.

## Key takeaways

- **Subscription pricing for AI code generators relies on average under-use.** If you actually used them at capacity, the unit economics collapse.
- **StackAlchemist is priced per generation because that is the unit of work we do.** No rental, no seat, no meter.
- **One-time pricing means you own the code.** If we vanish tomorrow, your SaaS is unaffected.
- **The principle is ownership, not recurring revenue.** You should own your stack. A generator should be a contractor, not a landlord.

If this matches how you want to buy software, [our pricing page](/pricing) lays the tiers out plainly.

— Steve
