# What actually compiles: a fair way to benchmark AI codegen

**By Steve Ackley · June 1, 2026 · 10 min read**

People keep asking me for a leaderboard: "What percentage of the time does each AI codegen tool produce code that compiles?" I understand the instinct — it feels like the obvious benchmark. It's also a category error, and if I published a clean table of percentages across the field, I'd be lying to you with decimal places.

So here's the honest version: why "compile rate" is the wrong metric, what the right one is, and an open test harness you can run yourself — so you don't have to trust anyone's marketing, including mine.

## Why a compile-rate leaderboard is a category error

Three reasons, and all three matter.

**The tools aren't in the same category.** You cannot put a single number on "[v0](/compare/v0)'s compile rate," because v0 generates a UI component you paste into an existing app — there is no standalone app to compile. [Cursor](/compare/cursor) edits your repo; its "compile rate" is really *your* repo's compile rate. [Bubble](/compare/bubble) and [Retool](/compare/retool) don't emit code you compile at all — they run a hosted runtime. Comparing their "compile rate" to a full-app generator's is comparing a paintbrush to a printing press and reporting which is faster.

**"Compiles" is defined differently for each tool.** [Bolt.new](/compare/bolt-new) and [Lovable](/compare/lovable) "compile" inside their own in-browser sandbox. [Replit Agent](/compare/replit-agent) and [base44](/compare/base44) run inside their hosted runtimes. That's a real thing — but it means "compiles in *their* environment," not "compiles in your CI, on your machine, under your Node and .NET versions." The word silently changes meaning from column to column, which makes the table meaningless even before you fill it in.

**A single number hides the failure you actually care about.** A tool could compile 100% of the time and still hand you something that won't `docker compose up`, won't apply a migration, or returns a 500 on the first real request. Compile is necessary, not sufficient — I made this case at length in [what "production-ready" actually means](/blog/what-production-ready-actually-means-for-generated-saas). A green build is step one of about five, and the table only shows step one.

If someone hands you a tidy "% compiles" leaderboard spanning these tools, they either don't understand the category or they're betting you don't.

## The metric that actually matters: deployable rate

Let me define the thing worth measuring, precisely. A generation is **deployable** if, on a clean machine, with zero manual fixes:

1. **It builds.** The compiler exits clean. (This is the only thing a "compile rate" measures.)
2. **`docker compose up` brings the whole stack up.** Database, API, frontend — all of it, no hand-holding.
3. **The migrations apply.** The schema actually materializes in a real database.
4. **A real request to the API returns a non-error response.** Not a sandbox preview — an actual HTTP call against the running service.
5. **A user can complete one core flow.** Sign up, create the primary entity, see it persist across a reload.

That is the bar. Compile is step 1 of 5. **Deployable rate** is the fraction of generations that clear all five with no hand-fixing. It's a harder, more honest number — and it's the one that actually predicts whether you'll ship.

## An open harness you can run yourself

Rather than ask you to trust my numbers, here's the shape of a test you can run against any tool that claims to generate a full application. The methodology, not the verdict, is the deliverable:

1. **Fix a prompt set.** Write N identical prompts spanning categories — a CRM, a booking app, a marketplace. Use the *same* prompts for every tool. (My own set leans on the [proven solution shapes](/blog/lessons-shipping-18-solution-templates).)
2. **For each tool, for each prompt:** generate, then run the five-step deployable check on a clean container. Script it so there's no judgment call.
3. **Score binary per step.** Report deployable rate = (all five pass) / (total). Crucially, record *where* each tool falls off — most fall off at step 2 or step 4, not step 1.
4. **Publish the prompts and the harness, not just the scores**, so anyone can re-run and check your work.

Here's the core of the check, as a starting point rather than gospel:

```bash
# deployable-check.sh — run against a freshly generated repo
set -euo pipefail
REPO="$1"
cd "$REPO"

# 1. Compiles
docker compose build              || { echo "FAIL: build";         exit 1; }
# 2. The whole stack comes up
docker compose up -d              || { echo "FAIL: up";            exit 1; }
# 3 + 4. Health, then a real request against the core entity
./scripts/wait-for-healthz.sh     || { echo "FAIL: healthz";       exit 1; }
curl -fsS localhost:3000/api/<core-entity> >/dev/null \
                                  || { echo "FAIL: core request";  exit 1; }

echo "DEPLOYABLE: all checks passed"
```

The point isn't my exact script. The point is that the criteria are objective, the test is runnable on a clean machine, and you don't have to believe anyone's slide deck — your own terminal gives you the answer.

## What I can tell you honestly, with no fake decimals

I'm not going to publish invented percentages for competitors. I haven't run a controlled, N-prompt study across all of them under identical conditions, and making up numbers would turn me into exactly the marketing I'm criticizing. What I *can* tell you comes from the structure of the category and from running these tools myself:

- **Component generators and IDE assistants aren't playing this game.** [v0](/compare/v0) and [Cursor](/compare/cursor) aren't trying to emit a deployable app — and that's fine, it's a different and valuable job. Their "deployable rate" is undefined, not low.
- **Sandbox and hosted-runtime generators clear "compiles" routinely — in their own environment.** [Bolt.new](/compare/bolt-new), [Lovable](/compare/lovable), [Replit Agent](/compare/replit-agent), and [base44](/compare/base44) will show you a running app fast. The open question is always step 2: does it come up on *your* infra, outside their runtime? That migration is where the friction lives, and it's structural, not incidental.
- **No-code platforms skip the code entirely.** [Bubble](/compare/bubble) and [Retool](/compare/retool) never hand you an artifact to deploy, so "deployable rate" doesn't apply — you simply never leave their runtime. That's a real trade-off I cover honestly on each page, not a score.
- **Boilerplates start at a 100% deployable template — because they're static and hand-maintained.** [OpenSaaS](/compare/wasp-opensaas) builds cleanly because a human keeps it building. But it isn't generating *your* domain, so the relevant question shifts from "did it compile" to "how much of my product did I still have to build by hand."

For StackAlchemist specifically, I'll tell you exactly what our gate does — because it's a fact about our own pipeline, not a claim about anyone else's. We run `dotnet build` and `pnpm build` on the generated artifact, retry up to three times feeding the compiler errors back to the model, and refund you if it still can't pass. That makes step 1 deterministic. Steps 2 through 5 are what the generated Docker config and CI exist to protect — and they're exactly what I'd want you to verify with the harness above, on *us* included. Hold us to the same bar I'm asking you to hold everyone to.

## How to use this when you're evaluating tools

- **Ignore compile-rate leaderboards.** Ask for deployable rate, defined as the five steps.
- **Run the harness on a trial generation.** Five minutes of `docker compose up` beats five slides of someone's benchmark.
- **Watch where a tool falls off.** Falling off at step 1 (won't compile) is rare and damning. Falling off at step 2 (won't run on your infra) is the common, quieter failure — and it's the one sandbox tools structurally risk.
- **Be fair about category.** Don't ding [v0](/compare/v0) for not being a deployable app; it never claimed to be one. Ding a tool only if it's *sold* as something it can't do under this test.

## Key takeaways

- **A "% compiles" leaderboard across AI codegen tools is a category error.** The tools don't emit comparable artifacts, and "compiles" means something different for each one.
- **The honest metric is deployable rate:** compiles, comes up in Docker, migrates, serves a real request, completes one core flow — with zero hand-fixing.
- **Don't trust the numbers — mine included.** Run the open harness on a trial generation yourself. Objective criteria, your own machine.
- **Most tools that fail, fail at "won't come up on your infra," not "won't compile."** Test step 2, because that's where the category quietly breaks.

Want to run it on us? [Start a generation](/simple), then point the harness at the repo. If we fall off at any of the five steps, that's a bug I want to hear about — by name.

— Steve
