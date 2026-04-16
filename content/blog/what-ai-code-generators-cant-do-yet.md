# v0, Bolt.new, Lovable, Cursor — what AI code generators still cannot do in 2026

**By Steve Ackley · April 22, 2026 · 9 min read**

If you are shopping for an AI code generator in 2026, you have a real menu for the first time. The space has matured. Most of the tools on that menu are genuinely good at what they do. I use several of them myself in side projects.

This post is an honest, practitioner's take on where each of them stops. Not a hit piece, not a "why we're better" puff piece — a real map of the terrain, written by someone who both builds in this space and competes in it. Where a competitor genuinely wins, I will say so.

## The tools, and what they are actually for

Before we get into what these tools cannot do, let's be clear about what they are for. This is the part most comparison posts butcher.

### v0 (by Vercel)

**What it is genuinely great at:** generating a beautiful, production-shaped React component from a natural-language prompt or an uploaded screenshot. The output is clean, uses shadcn/ui, and drops cleanly into a Next.js app.

**What it is not trying to be:** a full-stack generator. v0 does not generate a backend. It does not generate auth. It does not generate a database schema. It is a UI component factory, and it is excellent at that.

If you already have a Next.js app and you need a settings page or a data table, v0 is probably the fastest way to get a first draft. I use it for exactly that.

### Bolt.new (StackBlitz)

**What it is genuinely great at:** iterating on a web app inside a sandboxed browser environment. You prompt, Bolt writes a small app, you see it running immediately, you prompt again, it iterates. The feedback loop is unmatched.

**What it is not trying to be:** a production platform. Bolt's output is a prototype. The environment is WebContainers — you cannot run .NET, you cannot run arbitrary CLI tools, you cannot do anything that assumes a full server. The tradeoff is speed: Bolt is faster to "working demo" than anything else I have used.

When I need to prototype a purely-JavaScript idea for a couple of hours, Bolt is my tool of choice. When I need to ship an actual SaaS, it is not.

### Lovable

**What it is genuinely great at:** a pleasant, visual-first building experience for non-developers. Lovable's UX is its moat — it is designed so someone who has never written code can build and deploy a working web app. That is a real accomplishment.

**What it is not trying to be:** a tool where you own the code. Lovable's output lives inside Lovable's platform. You can export, but the export is a second-class citizen. The primary experience is "keep it on Lovable." For founders who want a managed hosting experience, that is fine. For founders who want to own their stack and deploy it to their own infra, it is not.

### Cursor

**What it is genuinely great at:** making a developer who already knows what they are doing faster. Cursor is not a code generator in the StackAlchemist / Bolt / Lovable sense. It is a code editor with LLM superpowers. You open an existing codebase, Cursor understands it, and it helps you write the next function faster.

**What it is not trying to be:** something that generates an app from a prompt. Cursor starts from an existing repo. If you do not have a repo, Cursor is not how you get one.

I use Cursor daily for editing. It is a different category of product entirely from everything else on this list.

### Replit Agent

**What it is genuinely great at:** generating a working simple app with a unified hosting experience, all in-browser. Like Lovable, the pitch is simplicity for non-developers.

**What it is not trying to be:** a generator of production-shaped enterprise stacks. Replit's sweet spot is simple apps hosted on Replit. The moment you want a .NET backend and a Next.js frontend with Supabase auth, you are past its target.

### StackAlchemist (us)

Putting ourselves on the map honestly: we generate an opinionated, production-shaped full-stack SaaS (.NET 10 + Next.js 15 + PostgreSQL) with auth, CI/CD, and Docker already wired. We verify the output compiles before you download it. You own the code outright — one-time price, deploy wherever you want. We are slower than Bolt (minutes, not seconds) and more opinionated than v0 (you get a whole stack, not a component). That is the trade.

## Where each tool stops

Now the honest part. Every tool has a ceiling. Here is where each of these tools hits it.

### v0's ceiling: the full app

v0 stops at the component. A generated component drops into your Next.js app cleanly, but v0 is not building your app. If you need the full app — routing, data, auth, state — you are stitching pieces together yourself, or moving to something else. v0 is the fastest way to generate part of a thing. It is not how you generate the whole thing.

### Bolt.new's ceiling: the sandbox

Bolt is phenomenal inside its sandbox, but the sandbox is its ceiling. You cannot generate a .NET backend because WebContainers do not run .NET. You cannot generate a native mobile app. You cannot run arbitrary Docker containers. Anything that requires a real server-side runtime beyond Node is out of scope. If your app fits inside WebContainers, Bolt is a great place to live. If it does not, you are migrating the moment you try to scale.

### Lovable's ceiling: ownership

This is the one that matters most for serious founders. Lovable's app runs on Lovable. Exporting is possible but awkward — the export is not a first-class product. The moment you need to deploy to your own VPC, migrate to your own database, or do anything that assumes you control the infrastructure, you are fighting the tool. For hobby projects, fine. For a SaaS you plan to sell, this is a real risk.

### Cursor's ceiling: cold start

Cursor needs a codebase to work on. It is incredible at editing, terrible at starting. If you are staring at an empty folder, Cursor cannot help you. You need a different tool to get to "I have a repo" before Cursor is useful.

### Replit Agent's ceiling: the platform

Same as Lovable but with slightly different framing. Replit is great if you want to live on Replit. The export story is better than Lovable's, but the primary experience is Replit-hosted. If your business plan involves owning your infra, Replit is a step, not a destination.

## What none of them do (as of 2026)

After using all five of these tools in anger over the past year, here is the honest list of what still cannot be done by any of them:

1. **Generate a verified, compiling, full-stack repo with a real backend.** v0 and Bolt and Lovable all hand you code that compiles sometimes. None of them run a full build as a gate before you download. This is [the compile guarantee problem](/blog/compile-guarantee-why-ai-codegen-must-verify) I have been banging on about.
2. **Generate an owned codebase.** Cursor edits yours; Lovable and Replit want to own theirs. No tool in the first-generation category hands you a cleanly-exportable, your-name-in-the-LICENSE repo by default.
3. **Generate with production opinions.** "Here is a sandbox that kinda works" is not the same as "here is a repo with CI/CD, auth, Docker, and a Dockerfile ready to deploy." Most of the tools stop at "it runs."
4. **Give you a backend that a senior engineer would recognize.** The .NET + Next.js + Postgres combination is boring on purpose. It is what serious SaaS looks like. Most of the tools in this space are focused on pure-JavaScript outputs because that is what the WebContainer / serverless demo environment tolerates.

## Where StackAlchemist fits

I am not pretending we are the answer for everyone. We are the answer for one specific person: someone who wants a compiled, owned, production-shaped SaaS repo that they can deploy anywhere, and they want it today, not after a week of wiring.

If you want to prototype a UI: use v0. It is faster, and the output drops cleanly into a Next.js project (including, coincidentally, one we generated).

If you want to iterate visually on a small JS app: use Bolt. The feedback loop is unmatched.

If you want a non-developer to build a hosted app: use Lovable. The UX is genuinely kind.

If you want to edit faster: use Cursor.

If you want a full-stack SaaS, compiled, owned, no vendor lock-in, [use us](/simple).

The space is big enough for all of us. Where it is not big enough is in the category of "a tool that pretends to do everything and does none of it well." Pick the right tool for the job.

## Key takeaways

- **v0** is the best UI component generator; it is not a full-stack platform.
- **Bolt.new** is the best JS prototype iterator; it is bounded by WebContainers.
- **Lovable** is the best non-developer visual builder; ownership of the code is weak.
- **Cursor** is the best editor augmentation; it needs an existing repo.
- **StackAlchemist** is the only tool in this list that hands you a verified, compiling, owned, production-shaped full-stack SaaS from a prompt.

Pick the right tool for the right job. And if the right job is "a real SaaS I own," you know where to find me.

— Steve
