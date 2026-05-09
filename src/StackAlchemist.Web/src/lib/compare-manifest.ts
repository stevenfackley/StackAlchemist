export interface CompareEntry {
  slug: string;
  competitorName: string;
  tagline: string;
  relativePath: string;
  winsForCompetitor: readonly string[];
  winsForUs: readonly string[];
  verdict: string;
  updatedAt: string;
}

export const COMPARE_ENTRIES = [
  {
    slug: "v0",
    competitorName: "v0 by Vercel",
    tagline: "StackAlchemist vs v0 — full-stack SaaS generator vs UI component factory",
    relativePath: "v0.md",
    winsForCompetitor: [
      "Best-in-class UI component generation",
      "Native shadcn/ui output",
      "Drops cleanly into any Next.js app",
      "Fastest iteration loop for pure visual design",
    ],
    winsForUs: [
      "Full-stack: backend, database, auth, payments",
      "Compile guarantee — verified build before download",
      "Owned code, deploy anywhere",
      "One-time pricing, no seat rental",
    ],
    verdict:
      "v0 is the right tool if you need one beautiful component. StackAlchemist is the right tool if you need an entire SaaS. They compose well — use v0 to iterate on UI inside a StackAlchemist-generated Next.js app.",
    updatedAt: "2026-04-22",
  },
  {
    slug: "bolt-new",
    competitorName: "Bolt.new",
    tagline: "StackAlchemist vs Bolt.new — production SaaS generator vs in-browser prototype sandbox",
    relativePath: "bolt-new.md",
    winsForCompetitor: [
      "Instant in-browser preview loop",
      "Sub-minute generation for simple JS apps",
      "Great for throwaway prototypes",
      "No install friction — browser is the IDE",
    ],
    winsForUs: [
      "Real backend runtime (.NET 10 API, not just Node)",
      "Deployable to your own infra, not just Bolt's sandbox",
      "Supabase + Stripe + Postgres wired by default",
      "Compile-gated verified output",
    ],
    verdict:
      "Bolt is the right tool for prototyping JS ideas in an afternoon. StackAlchemist is the right tool when you need a SaaS you can actually deploy and sell.",
    updatedAt: "2026-04-22",
  },
  {
    slug: "lovable",
    competitorName: "Lovable",
    tagline: "StackAlchemist vs Lovable — owned full-stack repo vs hosted in-browser iteration",
    relativePath: "lovable.md",
    winsForCompetitor: [
      "Best-in-class live-preview iteration loop",
      "Zero install, zero local tooling",
      "Thoughtful Supabase integration",
      "Frictionless for non-technical founders",
    ],
    winsForUs: [
      "Real .NET 10 backend, not just edge functions",
      "Compile-gated verified output",
      "Owned repo — docker compose up, deploy anywhere",
      "One-time pricing, no message-metered subscription",
    ],
    verdict:
      "Lovable is the best in-browser iteration loop for a full app. StackAlchemist is the right tool when the goal shifts from sketching a product to owning a SaaS you can sell.",
    updatedAt: "2026-04-20",
  },
  {
    slug: "cursor",
    competitorName: "Cursor",
    tagline: "StackAlchemist vs Cursor — SaaS generator vs AI-native IDE (use both)",
    relativePath: "cursor.md",
    winsForCompetitor: [
      "Best AI-native IDE on the market",
      "Composer / agent mode on real repos",
      "VS Code-compatible — your setup carries over",
      "Thoughtful context handling on large codebases",
    ],
    winsForUs: [
      "Generate a full repo from a prompt — not file-by-file",
      "Compile-gated output: frontend + backend + DB + auth + Stripe",
      "One-time price for the artifact, not monthly seats",
      "Starts the repo; Cursor is perfect for the rest of its life",
    ],
    verdict:
      "Different categories. Cursor is the best AI IDE once you have a repo. StackAlchemist creates the repo. Use StackAlchemist to generate, then Cursor forever after.",
    updatedAt: "2026-04-20",
  },
  {
    slug: "replit-agent",
    competitorName: "Replit Agent",
    tagline: "StackAlchemist vs Replit Agent — owned repo on your infra vs hosted Replit workspace",
    relativePath: "replit-agent.md",
    winsForCompetitor: [
      "Zero-to-running in minutes on Replit's runtime",
      "Flexible stack — Python, Node, Go, whatever",
      "Built-in hosting, no infra decisions",
      "Great for teaching, hobby projects, hack weekends",
    ],
    winsForUs: [
      "Real repo on your GitHub, under your license",
      "Specific production stack: Next.js 15 + .NET 10 + Postgres",
      "Compile-gated output — every build passes before download",
      "One-time price, not hosted subscription",
    ],
    verdict:
      "Replit Agent is excellent for shipping inside the Replit ecosystem. StackAlchemist is the right tool when the goal is a SaaS you own, on your infra, under your license — a business you can run and sell.",
    updatedAt: "2026-04-20",
  },
  {
    slug: "bubble",
    competitorName: "Bubble",
    tagline: "StackAlchemist vs Bubble — owned full-stack code vs hosted no-code platform",
    relativePath: "bubble.md",
    winsForCompetitor: [
      "Best-in-class visual no-code app builder",
      "Real database + workflows without writing code",
      "Mature plugin ecosystem and community",
      "Genuinely usable by non-technical founders",
    ],
    winsForUs: [
      "Real owned code in a real repo — not platform-locked",
      "Production .NET 10 + Next.js 15 stack you can hire any full-stack dev to extend",
      "No per-user runtime tax; deploy to your infra at your costs",
      "Compile-gated output and predictable performance at scale",
    ],
    verdict:
      "Bubble is the right tool when you cannot or will not write code and want to ship fast. StackAlchemist is the right tool when you want a real owned codebase you can scale, sell, or hand to engineers without re-platforming.",
    updatedAt: "2026-05-09",
  },
  {
    slug: "retool",
    competitorName: "Retool",
    tagline: "StackAlchemist vs Retool — customer-facing SaaS generator vs internal-tools platform",
    relativePath: "retool.md",
    winsForCompetitor: [
      "Best-in-class for internal admin tools and dashboards",
      "Connects to existing DBs and APIs without code",
      "Fast for ops/data teams to ship internal UI in hours",
      "Strong feature set: Workflows, AI, granular permissions",
    ],
    winsForUs: [
      "Generates the customer-facing SaaS, not the admin layer on top of one",
      "Real owned repo, not a hosted runtime keyed to a Retool seat",
      "Backend + DB schema + auth + Stripe in one generation, not bolted on",
      "One-time price for the artifact, not per-user platform fees",
    ],
    verdict:
      "Retool is the right tool when you already have a SaaS and need the internal admin/ops UI on top of it. StackAlchemist is the right tool when the SaaS itself is what you need to exist.",
    updatedAt: "2026-05-09",
  },
] as const satisfies readonly CompareEntry[];

export function getCompareEntryBySlug(slug: string): CompareEntry | undefined {
  return COMPARE_ENTRIES.find((e) => e.slug === slug);
}

export function getAllCompareSlugs(): string[] {
  return COMPARE_ENTRIES.map((e) => e.slug);
}
