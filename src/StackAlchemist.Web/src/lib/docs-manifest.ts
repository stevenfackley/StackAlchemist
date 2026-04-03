export interface DocMeta {
  slug: string;
  title: string;
  section: "user" | "advanced";
  relativePath: string;
}

export const DOC_SECTIONS = [
  {
    label: "User Guide",
    key: "user" as const,
  },
  {
    label: "Advanced",
    key: "advanced" as const,
  },
];

export const DOCS: DocMeta[] = [
  {
    slug: "getting-started",
    title: "Getting Started",
    section: "user",
    relativePath: "user/getting-started.md",
  },
  {
    slug: "simple-mode",
    title: "Simple Mode",
    section: "user",
    relativePath: "user/simple-mode.md",
  },
  {
    slug: "advanced-mode",
    title: "Advanced Mode",
    section: "user",
    relativePath: "user/advanced-mode.md",
  },
  {
    slug: "tiers-and-pricing",
    title: "Tiers & Pricing",
    section: "user",
    relativePath: "user/tiers-and-pricing.md",
  },
  {
    slug: "your-output",
    title: "Your Output",
    section: "user",
    relativePath: "user/your-output.md",
  },
  {
    slug: "faq",
    title: "FAQ",
    section: "user",
    relativePath: "user/faq.md",
  },
  {
    slug: "troubleshooting",
    title: "Troubleshooting",
    section: "user",
    relativePath: "user/troubleshooting.md",
  },
  {
    slug: "swiss-cheese-method",
    title: "The Swiss Cheese Method",
    section: "advanced",
    relativePath: "advanced-docs/swiss-cheese-method.md",
  },
  {
    slug: "compile-guarantee",
    title: "The Compile Guarantee",
    section: "advanced",
    relativePath: "advanced-docs/compile-guarantee.md",
  },
  {
    slug: "architecture-overview",
    title: "Architecture Overview",
    section: "advanced",
    relativePath: "advanced-docs/architecture-overview.md",
  },
  {
    slug: "self-hosting",
    title: "Self-Hosting",
    section: "advanced",
    relativePath: "advanced-docs/self-hosting.md",
  },
];
