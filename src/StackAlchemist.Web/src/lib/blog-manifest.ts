export interface BlogPostMeta {
  slug: string;
  title: string;
  description: string;
  publishedAt: string;
  author: string;
  tags: readonly string[];
  relativePath: string;
  readingTimeMinutes: number;
}

export const BLOG_AUTHOR = "Steve Ackley";

export const BLOG_POSTS = [
  {
    slug: "compile-guarantee-why-ai-codegen-must-verify",
    title: "The Compile Guarantee: why AI codegen must verify, not just generate",
    description:
      "Most AI code generators hand you broken code and call it shipped. Here is why a compile guarantee is the minimum bar for real SaaS builders, and how we enforce it.",
    publishedAt: "2026-04-16",
    author: BLOG_AUTHOR,
    tags: ["ai-codegen", "compile-guarantee", "engineering"],
    relativePath: "compile-guarantee-why-ai-codegen-must-verify.md",
    readingTimeMinutes: 7,
  },
  {
    slug: "swiss-cheese-method-deterministic-templates-llm-logic",
    title: "The Swiss Cheese Method: deterministic templates plus LLM logic",
    description:
      "Full LLM generation hallucinates. Pure templates are rigid. The Swiss Cheese Method fuses deterministic scaffolding with targeted LLM logic so the output actually compiles.",
    publishedAt: "2026-04-18",
    author: BLOG_AUTHOR,
    tags: ["architecture", "swiss-cheese-method", "engineering"],
    relativePath: "swiss-cheese-method-deterministic-templates-llm-logic.md",
    readingTimeMinutes: 8,
  },
  {
    slug: "what-ai-code-generators-cant-do-yet",
    title: "v0, Bolt.new, Lovable, Cursor — what AI code generators still cannot do in 2026",
    description:
      "An honest comparison of the AI-codegen landscape in 2026 and where each tool stops. v0 and Bolt are great inside their lane, but nobody ships a verified, owned, production SaaS from a single prompt except us.",
    publishedAt: "2026-04-22",
    author: BLOG_AUTHOR,
    tags: ["comparison", "ai-codegen", "market"],
    relativePath: "what-ai-code-generators-cant-do-yet.md",
    readingTimeMinutes: 9,
  },
  {
    slug: "prompt-to-production-dotnet-nextjs-in-12-minutes",
    title: "From prompt to production: generating a .NET 10 + Next.js 15 SaaS in 12 minutes",
    description:
      "A timed walkthrough of a real StackAlchemist generation run. Prompt in at t=0, zip of compiled code in your hands at t=12 minutes. Here is what actually happens in between.",
    publishedAt: "2026-04-25",
    author: BLOG_AUTHOR,
    tags: ["walkthrough", "generation", "engineering"],
    relativePath: "prompt-to-production-dotnet-nextjs-in-12-minutes.md",
    readingTimeMinutes: 8,
  },
  {
    slug: "why-we-charge-once-not-monthly",
    title: "Why we charge once, not monthly: the economics of AI-generated SaaS",
    description:
      "Every AI-codegen tool in the market is a monthly subscription. We are not. Here is the math on why a one-time price for generated code is the honest model and what it means for your margins.",
    publishedAt: "2026-04-28",
    author: BLOG_AUTHOR,
    tags: ["pricing", "business", "ownership"],
    relativePath: "why-we-charge-once-not-monthly.md",
    readingTimeMinutes: 7,
  },
] as const satisfies readonly BlogPostMeta[];

export function getBlogPostMetaBySlug(slug: string): BlogPostMeta | undefined {
  return BLOG_POSTS.find((p) => p.slug === slug);
}

export function getAllBlogSlugs(): string[] {
  return BLOG_POSTS.map((p) => p.slug);
}

export function getSortedBlogPosts(): BlogPostMeta[] {
  return [...BLOG_POSTS].sort((a, b) => (a.publishedAt < b.publishedAt ? 1 : -1));
}
