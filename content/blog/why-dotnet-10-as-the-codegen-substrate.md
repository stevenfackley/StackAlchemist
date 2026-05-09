# Why .NET 10 — and why the substrate is the most important decision in AI codegen

**By Steve Ackley · May 9, 2026 · 8 min read**

The question I get asked most after "what's the compile guarantee" is "why .NET?"

The honest answer is that the substrate choice — what language and framework the LLM has to fill in — is the single most important decision in AI codegen, and Node was the wrong choice for the kind of guarantee I wanted to make. This post is the long version of that answer, because the short version sounds glib and the real answer is interesting.

## The substrate is not a stack opinion

Most AI codegen tools treat the output stack as a marketing decision. v0 picked React + shadcn because their audience uses React + shadcn. Bolt picked Node because Node runs in their browser sandbox. Lovable picked Supabase + edge functions because they integrate well into a hosted iteration loop. Each of those is a coherent choice for the product.

When you are building a tool whose central promise is "the build will not be broken," the substrate is not a marketing decision. It is an engineering one. The substrate decides how forgiving the compile gate gets to be, how much of the LLM's output the type system can catch before a human ever sees it, and how often retries actually converge on a passing build.

Pick the wrong substrate and the compile guarantee becomes "we tried `pnpm build`, it threw a runtime error two minutes after starting, we shrugged and shipped it anyway." Which is what most AI codegen tools functionally do today, even if they don't admit it.

## What I needed from the substrate

Before I picked a stack, I wrote down what the substrate had to give me to make the compile guarantee real. There were five hard requirements.

**1. The compiler has to know things the LLM doesn't.** When the LLM forgets a parameter or invents a method name, I want the compiler to refuse the build, not silently produce a runtime time bomb. The more checks the compiler does at build time — null safety, generics, exhaustive switches, async correctness — the more LLM mistakes get caught before the user sees the artifact.

**2. Build errors have to be specific.** When the compile gate fires, I send the build error back to the LLM and ask it to patch. If the error is "Cannot find name 'foo'" with a line number, the LLM patches it on the first try. If the error is "TypeError: undefined is not a function" with a stack trace, the LLM has to guess what produced the undefined, and retries take three or four passes instead of one.

**3. The runtime has to be deterministic across hosts.** A SaaS that compiles on my CI but fails on the user's laptop is worse than no SaaS. I needed a runtime where the build output is the same artifact everywhere it lands, with the same dependency graph, the same file layout, the same startup behavior.

**4. The output has to be hireable.** A generated repo that nobody else in the world can read is not a SaaS. It is a hostage situation. The substrate has to be one that a senior full-stack engineer can be hired against — they know the language, they know the framework, they have written tests in it before.

**5. The substrate has to be willing to be templated.** This is the subtle one. The Swiss Cheese Method depends on me writing static, deterministic Handlebars templates that produce the scaffolding, with holes the LLM fills. Some languages and frameworks fight templating. They use too much convention, too much codegen-on-top-of-codegen, too much "magic" that obscures what the file actually says. Others — including .NET — are remarkably template-friendly, because the language is explicit about what it is doing.

Hold those five requirements next to candidates and the picture clarifies fast.

## Where Node specifically would have failed

Node was the obvious default. I evaluated it seriously. Here is where it broke down for me.

**The compiler-knows-things test.** TypeScript is a real type system, and a strict TS config catches a lot. But it is bolted onto a runtime that ignores it. A generated TS file can typecheck cleanly and still throw at runtime because some `any` slipped through, because the `tsconfig` was lenient, because the production transpile path stripped types and the assumption broke. .NET 10 is end-to-end typed: source through compile to runtime. There is no "the types are vibes at runtime" failure mode.

**The build-errors-are-specific test.** Roslyn errors point at exact tokens with exact reasons. The LLM patches them on the first retry the vast majority of the time. TypeScript errors are also good, but production Node toolchains layer Webpack, esbuild, swc, Next.js, Turbo, and so on — and when something fails three layers deep, the error is "Module not found at chunk-XYZ123.js" and the LLM has to spelunk. Retry counts went up sharply during the Node prototype.

**The deterministic-runtime test.** This was a real loss for Node. `node_modules` resolution depends on hoisting behavior, on which package manager the user runs, on whether postinstall scripts succeed in the user's environment, on Native Node modules compiling against the user's libc version. .NET 10 with self-contained publishing produces a single binary with the runtime baked in. The same artifact runs on the same kernel everywhere. For a generated SaaS where I cannot supervise the user's environment, that is the difference between "ships" and "support load."

**The hireable test.** Node and .NET both pass — both have giant talent pools. Net even.

**The template-friendly test.** This is the one I underestimated until I tried both. .NET projects have explicit, readable scaffolding. A `Program.cs`, a `Startup.cs`-shaped builder pattern, explicit DI registrations, explicit EF Core `DbContext` declarations. When I write a Handlebars template that produces a `Program.cs`, the output looks like what a human .NET dev would have written. Node ecosystems vary so wildly — sometimes it is a `server.ts` with explicit Express, sometimes it is a Next.js API route, sometimes it is a tRPC router with adapters, sometimes it is a Hono handler in a Worker — that the templates have to embed an entire opinion stack just to produce a coherent file. The seam between template and LLM gets noisier in Node, which is precisely where the compile guarantee lives.

This is not an argument that Node cannot work. It is an argument that Node would have made the central guarantee weaker, and the central guarantee is the product.

## Why .NET 10 specifically — not .NET 8 or 9

I picked .NET 10 over its predecessors deliberately, and the reasons are practical.

**Native AOT is finally first-class.** .NET 10's AOT story is real. The output of a generation can be published as a native binary, which means the user's deploy box does not need a .NET runtime installed. For a generator that hands users a Docker compose file, that means the production image is small, fast to start, and does not depend on a runtime that has to be patched independently of the application.

**The minimal API and source generators in .NET 10 cut template noise.** Earlier .NET had a lot of ceremony — startup classes, separate DI builders, scattered configuration. .NET 10's minimal API + improved source generators let me write template scaffolding that is dense and readable. Less template surface to maintain, fewer holes for the LLM to misread.

**The Standard Library has caught up.** The kinds of things that used to require third-party libraries — JSON serialization, HTTP clients with reasonable defaults, async file IO, structured logging — are first-party in .NET 10. That means the generated repo's `csproj` has a small dependency graph, which means the dependency-resolution failure mode that haunts Node generations basically does not happen.

**Long-term support and Microsoft's release cadence.** .NET LTS releases ship every other year, supported for three. The output of a StackAlchemist generation today is a .NET 10 repo, and .NET 10 will be patched by Microsoft through 2028. That is a longer support window than most JavaScript runtimes commit to, and it matters when the user is taking the repo home and running it for years.

## The honest case for Node, briefly

I want to be fair: there are real things Node does better, and they are real reasons some users would still prefer a Node-output generator.

**Frontend developers know Node.** If your team is JS-only and you do not want to onboard anyone onto a new backend language, the Node ecosystem is the path of least resistance. StackAlchemist's frontend is Next.js — a Node project — but the backend is .NET, and that is a context switch some teams will not want.

**Edge / serverless story.** If your deploy target is Cloudflare Workers or Vercel Edge or Lambda, Node is the better fit. .NET on serverless is improving but is not where Node is. StackAlchemist outputs Docker-shaped infrastructure, which works fine for the kinds of SaaS we generate, but is not a serverless story.

**Iteration speed in dev.** Node's reload loop is faster than .NET's. For an iteration-loop tool, this would matter. For a generator that runs end to end and hands you a repo, the dev-reload speed of the language is invisible to the user — but if you plan to live in the repo daily after generation, it is a real cost.

If those things outweigh the substrate-correctness argument for your project, fair. Use a Node-output tool. The choice we made for the guarantee we want to make is .NET 10.

## What this means for you, the user of the tool

You do not have to care about the language argument. What you have to care about is the second-order effect: a generator that picks its substrate correctly produces output that compiles, runs, and continues to compile and run as you maintain it. A generator that picks its substrate badly produces output that requires you to be fluent in the failure modes of the chosen runtime to ship anything.

If your hard requirement is "I get something that compiles," the substrate choice is what makes that promise true or false. .NET 10 is the choice that lets us actually keep it.

If your hard requirement is "I want the generated stack to be a stack I already use," that is a different requirement and we are honest that .NET-generated output is not for everyone. The compile guarantee is the product. The .NET 10 substrate is the only honest way I have found to deliver it.

## Key takeaways

- **The substrate is not a marketing decision.** It is the single most important engineering decision in AI codegen, and it determines whether your compile guarantee is real or theatrical.
- **Node has real strengths but loses on substrate-correctness for this product.** TypeScript at the seam, runtime non-determinism, and noisy template surface area all weaken the guarantee.
- **.NET 10 wins on the five things I needed: strict typing end-to-end, specific build errors, deterministic runtime via AOT, hireable codebase, template-friendly scaffolding.**
- **You do not have to know .NET to use the output.** A senior full-stack engineer can pick up the repo on day one. But you do have to be willing to live in a .NET backend.

If you want a real owned, compile-verified SaaS and the .NET-backend trade is acceptable, [start a generation](/simple). If a Node backend is non-negotiable, that is fair, and we are not the right tool for you yet.

— Steve
