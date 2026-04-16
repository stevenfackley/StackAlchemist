export interface FaqCategory {
  slug: string;
  label: string;
}

export interface FaqEntry {
  category: FaqCategory["slug"];
  question: string;
  // Plain-text answer (no markdown). Kept ≤4 sentences so Google's AI Overviews
  // and Perplexity can lift the whole answer as a citation without truncation.
  answer: string;
}

export const FAQ_CATEGORIES: FaqCategory[] = [
  { slug: "general", label: "General" },
  { slug: "generation", label: "Generation" },
  { slug: "pricing", label: "Pricing & Licensing" },
  { slug: "output", label: "The Output" },
  { slug: "technical", label: "Technical" },
];

export const FAQS: FaqEntry[] = [
  {
    category: "general",
    question: "What is StackAlchemist?",
    answer:
      "StackAlchemist is an AI SaaS generator that turns natural-language product descriptions into fully compiled, production-ready full-stack code repositories. The output includes a .NET 10 Web API, a Next.js 15 frontend, a PostgreSQL schema, Supabase auth, Docker Compose, and optional AWS CDK infrastructure — all guaranteed to build on delivery.",
  },
  {
    category: "general",
    question: "How is StackAlchemist different from v0, Bolt.new, or Lovable?",
    answer:
      "v0, Bolt.new, and Lovable generate prototypes or frontends that run in their hosted runtime. StackAlchemist generates a complete full-stack repository — .NET backend, PostgreSQL schema, infrastructure-as-code — that you download and own outright. Every generation passes through a real compiler before delivery, so the output is verified to build, not just rendered.",
  },
  {
    category: "general",
    question: "Is StackAlchemist a no-code tool?",
    answer:
      "No. StackAlchemist is a code generator, not a no-code platform. The output is standard .NET 10 and Next.js 15 source code that you read, modify, commit to your own repo, and deploy anywhere. There is no StackAlchemist runtime or SDK you depend on at runtime.",
  },
  {
    category: "general",
    question: "What is the Swiss Cheese Method?",
    answer:
      "The Swiss Cheese Method is StackAlchemist's approach to reliable AI code generation. Deterministic Handlebars templates define the outer structure (file layout, class skeletons, import paths), and an LLM fills only the business-logic holes (query implementations, domain validation). Structure is predictable; intelligence is applied exactly where variability is expected.",
  },

  {
    category: "generation",
    question: "What is the Compile Guarantee?",
    answer:
      "Every Boilerplate and Infrastructure generation runs through the real .NET and Next.js compilers before delivery. If either build fails, the compiler errors are fed back to the LLM and the failing files are regenerated — up to three times. If the output still does not compile, you receive a full refund automatically.",
  },
  {
    category: "generation",
    question: "How long does a generation take?",
    answer:
      "Simple schemas with 3–5 entities finish in about 30 seconds. Medium schemas of 6–10 entities take roughly 60 seconds. Complex schemas with 10 or more entities take about 90 seconds. Progress updates stream in real time throughout.",
  },
  {
    category: "generation",
    question: "What happens if my generated code does not work?",
    answer:
      "Compile-time failures trigger an automatic full refund — that is the guarantee. Runtime bugs are your responsibility to fix, same as any codebase you own; StackAlchemist guarantees the code compiles, not that every edge case is handled. You receive the full source, so you debug it exactly as you would any hand-written project.",
  },

  {
    category: "pricing",
    question: "Is StackAlchemist a subscription?",
    answer:
      "No. Every tier is a one-time payment per generation. You pay once, download the code, and own the output forever with no recurring fees and no licensing restrictions.",
  },
  {
    category: "pricing",
    question: "What are the pricing tiers?",
    answer:
      "Spark is a free tier for exploring schemas. Blueprint ($299) delivers architecture documents — schema, OpenAPI spec, SQL, and data-flow diagrams. Boilerplate ($599) adds the full compiled source plus Docker Compose. Infrastructure ($999) includes everything plus AWS CDK, Helm charts, a CI/CD pipeline, and a deployment runbook.",
  },
  {
    category: "pricing",
    question: "Can I use the generated code in a commercial product?",
    answer:
      "Yes. The generated code has no licensing restrictions imposed by StackAlchemist. You can ship it, sell it, modify it, and scale it — it is yours outright.",
  },

  {
    category: "output",
    question: "Can I deploy the generated code anywhere?",
    answer:
      "Yes. The Boilerplate tier ships a Docker Compose setup that runs on any Docker host — laptop, VPS, or cloud VM. The Infrastructure tier adds AWS CDK for one-command cloud deployment, but the application itself runs unmodified on any cloud provider or on-premise environment.",
  },
  {
    category: "output",
    question: "What stack does StackAlchemist generate?",
    answer:
      "The V1 stack is a .NET 10 Web API with Dapper and PostgreSQL on the backend, Next.js 15 with the App Router and Tailwind CSS on the frontend, Supabase for auth and storage, and Docker Compose for local development. Infrastructure tier adds AWS CDK infrastructure-as-code.",
  },
  {
    category: "output",
    question: "Does the generated code depend on any StackAlchemist libraries?",
    answer:
      "No. The output uses only mainstream open-source dependencies: .NET 10 / ASP.NET Core (MIT), Dapper (Apache 2.0), Next.js 15 (MIT), Tailwind CSS (MIT), and Supabase JS (Apache 2.0). There is no StackAlchemist SDK or runtime dependency injected into your codebase.",
  },

  {
    category: "technical",
    question: "What AI model does StackAlchemist use?",
    answer:
      "The generation engine uses Claude 3.5 Sonnet for business-logic injection. Structural scaffolding — file layout, class skeletons, and import paths — is produced by deterministic Handlebars templates, not the LLM. This split is how the Swiss Cheese Method keeps output reliable.",
  },
  {
    category: "technical",
    question: "Does StackAlchemist train on my data?",
    answer:
      "No. Your schema and prompts are used solely to generate your architecture. They are not used to train any model or shared with third parties beyond what is required for generation.",
  },
  {
    category: "technical",
    question: "Can I self-host StackAlchemist?",
    answer:
      "Yes. The platform itself ships as a Docker Compose stack, so self-hosting is supported for teams that want to run generations entirely inside their own infrastructure. See the Self-Hosting documentation for setup details.",
  },
];
