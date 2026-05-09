export interface SolutionEntry {
  slug: string;
  vertical: string;
  h1: string;
  metaTitle: string;
  metaDescription: string;
  relativePath: string;
  entityExamples: readonly string[];
  primaryKeywords: readonly string[];
  updatedAt: string;
}

export const SOLUTION_ENTRIES = [
  {
    slug: "ai-ecommerce-platform",
    vertical: "AI E-commerce Platform",
    h1: "Generate a full AI E-commerce Platform from a prompt",
    metaTitle: "AI E-commerce Platform Generator — Compile-Guaranteed SaaS",
    metaDescription:
      "Generate a full .NET 10 + Next.js 15 AI-powered e-commerce platform with Stripe, auth, and inventory in 12 minutes. Verified build, owned code, one-time price.",
    relativePath: "ai-ecommerce-platform.md",
    entityExamples: [
      "Product",
      "Variant",
      "Inventory",
      "Cart",
      "Order",
      "Customer",
      "Review",
      "PromoCode",
    ],
    primaryKeywords: [
      "ai ecommerce platform",
      "ai ecommerce builder",
      "ai shopify alternative",
      "generate ecommerce saas",
    ],
    updatedAt: "2026-04-16",
  },
  {
    slug: "ai-lms-builder",
    vertical: "AI LMS Builder",
    h1: "Generate a full AI Learning Management System from a prompt",
    metaTitle: "AI LMS Builder — Generate a Full Learning Platform in Minutes",
    metaDescription:
      "Generate a full .NET 10 + Next.js 15 AI-powered LMS with courses, lessons, quizzes, enrollments, and Stripe billing. Verified build, owned code, one-time price.",
    relativePath: "ai-lms-builder.md",
    entityExamples: [
      "Course",
      "Lesson",
      "Module",
      "Quiz",
      "Question",
      "Enrollment",
      "Student",
      "Instructor",
    ],
    primaryKeywords: [
      "ai lms builder",
      "ai learning management system",
      "ai course platform generator",
      "ai teachable alternative",
    ],
    updatedAt: "2026-04-16",
  },
  {
    slug: "fintech-saas-generator",
    vertical: "Fintech SaaS Generator",
    h1: "Generate a full Fintech SaaS from a prompt",
    metaTitle: "Fintech SaaS Generator — Generate a Compliant Finance Platform",
    metaDescription:
      "Generate a .NET 10 + Next.js 15 fintech SaaS with accounts, transactions, KYC hooks, audit trails, and Stripe. Verified build, owned code, one-time price.",
    relativePath: "fintech-saas-generator.md",
    entityExamples: [
      "Account",
      "Transaction",
      "Ledger",
      "KycProfile",
      "AuditEvent",
      "Institution",
      "PaymentMethod",
    ],
    primaryKeywords: [
      "fintech saas generator",
      "generate fintech platform",
      "ai fintech builder",
      "fintech codegen",
    ],
    updatedAt: "2026-04-16",
  },
  {
    slug: "ai-crm-builder",
    vertical: "AI CRM Builder",
    h1: "Generate a full AI CRM from a prompt",
    metaTitle: "AI CRM Builder — Generate a Custom CRM SaaS in Minutes",
    metaDescription:
      "Generate a .NET 10 + Next.js 15 CRM with contacts, deals, pipelines, tasks, and email integration. Verified build, owned code, one-time price — no Salesforce tax.",
    relativePath: "ai-crm-builder.md",
    entityExamples: [
      "Contact",
      "Account",
      "Deal",
      "Pipeline",
      "Stage",
      "Activity",
      "Task",
      "Note",
    ],
    primaryKeywords: [
      "ai crm builder",
      "generate crm saas",
      "custom crm generator",
      "ai salesforce alternative",
    ],
    updatedAt: "2026-05-09",
  },
  {
    slug: "ai-marketplace-platform",
    vertical: "AI Marketplace Platform",
    h1: "Generate a full AI Marketplace Platform from a prompt",
    metaTitle: "AI Marketplace Generator — Generate a Two-Sided Marketplace SaaS",
    metaDescription:
      "Generate a .NET 10 + Next.js 15 two-sided marketplace with listings, vendors, buyers, payouts, and Stripe Connect. Verified build, owned code, one-time price.",
    relativePath: "ai-marketplace-platform.md",
    entityExamples: [
      "Listing",
      "Vendor",
      "Buyer",
      "Order",
      "Payout",
      "Review",
      "Category",
      "Dispute",
    ],
    primaryKeywords: [
      "ai marketplace generator",
      "generate marketplace saas",
      "two sided marketplace builder",
      "ai marketplace platform",
    ],
    updatedAt: "2026-05-09",
  },
  {
    slug: "ai-jobboard-builder",
    vertical: "AI Job Board Builder",
    h1: "Generate a full AI Job Board from a prompt",
    metaTitle: "AI Job Board Builder — Generate a Niche Job Board SaaS",
    metaDescription:
      "Generate a .NET 10 + Next.js 15 niche job board with listings, employer accounts, applicants, billing, and an admin panel. Verified build, owned code, one-time price.",
    relativePath: "ai-jobboard-builder.md",
    entityExamples: [
      "JobListing",
      "Employer",
      "Applicant",
      "Application",
      "Subscription",
      "Category",
      "Location",
      "FeaturedSlot",
    ],
    primaryKeywords: [
      "ai job board builder",
      "niche job board generator",
      "generate job board saas",
      "ai jobboard codegen",
    ],
    updatedAt: "2026-05-09",
  },
] as const satisfies readonly SolutionEntry[];

export function getSolutionBySlug(slug: string): SolutionEntry | undefined {
  return SOLUTION_ENTRIES.find((e) => e.slug === slug);
}

export function getAllSolutionSlugs(): string[] {
  return SOLUTION_ENTRIES.map((e) => e.slug);
}
