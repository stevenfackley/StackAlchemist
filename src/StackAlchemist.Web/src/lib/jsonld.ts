const SITE_NAME = "StackAlchemist";

export function softwareApplicationJsonLd(siteUrl: string, description: string): object {
  return {
    "@context": "https://schema.org",
    "@type": "SoftwareApplication",
    name: SITE_NAME,
    applicationCategory: "DeveloperApplication",
    operatingSystem: "Web",
    description,
    url: siteUrl,
  };
}

export function organizationJsonLd(siteUrl: string): object {
  const base = siteUrl.replace(/\/$/, "");
  return {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: SITE_NAME,
    url: siteUrl,
    logo: `${base}/logo.png`,
    description:
      "StackAlchemist turns natural-language specifications into fully compiled, downloadable .NET + Next.js + PostgreSQL code repositories using the Swiss Cheese Method.",
    sameAs: [
      "https://github.com/stevenfackley/StackAlchemist",
      "https://x.com/stackalchemist",
      "https://www.linkedin.com/company/stackalchemist",
      "https://discord.gg/stackalchemist",
    ],
  };
}

export function websiteJsonLd(siteUrl: string, description: string): object {
  return {
    "@context": "https://schema.org",
    "@type": "WebSite",
    name: SITE_NAME,
    url: siteUrl,
    description,
    publisher: { "@type": "Organization", name: SITE_NAME },
  };
}

export type PricingOfferLd = { name: string; price: number; href: string };

export function pricingProductJsonLd(siteUrl: string, offers: PricingOfferLd[]): object {
  const base = siteUrl.replace(/\/$/, "");
  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: SITE_NAME,
    description: "Compare StackAlchemist tiers and pick the handoff depth that fits your project.",
    brand: { "@type": "Brand", name: SITE_NAME },
    image: `${base}/opengraph-image`,
    offers: offers.map((o) => ({
      "@type": "Offer",
      name: o.name,
      priceCurrency: "USD",
      price: o.price,
      availability: "https://schema.org/InStock",
      url: o.href,
    })),
  };
}

export type BlogPostLd = {
  title: string;
  description: string;
  publishedAt: string;
  author: string;
  slug: string;
};

export function blogIndexJsonLd(siteUrl: string, posts: BlogPostLd[]): object {
  return {
    "@context": "https://schema.org",
    "@type": "Blog",
    name: `${SITE_NAME} Blog`,
    url: `${siteUrl}/blog`,
    description:
      "Founder-written essays on AI code generation, verified codegen, and the economics of AI-generated SaaS.",
    blogPost: posts.map((p) => ({
      "@type": "BlogPosting",
      headline: p.title,
      description: p.description,
      datePublished: p.publishedAt,
      author: { "@type": "Person", name: p.author },
      url: `${siteUrl}/blog/${p.slug}`,
    })),
  };
}

export type BlogPostMetaLd = {
  title: string;
  description: string;
  publishedAt: string;
  author: string;
  tags: readonly string[];
};

export function blogPostingJsonLd(
  siteUrl: string,
  slug: string,
  meta: BlogPostMetaLd,
): object {
  return {
    "@context": "https://schema.org",
    "@type": "BlogPosting",
    headline: meta.title,
    description: meta.description,
    datePublished: meta.publishedAt,
    dateModified: meta.publishedAt,
    author: { "@type": "Person", name: meta.author, url: `${siteUrl}/about` },
    publisher: { "@type": "Organization", name: SITE_NAME, url: siteUrl },
    mainEntityOfPage: { "@type": "WebPage", "@id": `${siteUrl}/blog/${slug}` },
    keywords: meta.tags.join(", "),
  };
}

export type BreadcrumbItemLd = { name: string; item: string };

export function breadcrumbJsonLd(items: BreadcrumbItemLd[]): object {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((it, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: it.name,
      item: it.item,
    })),
  };
}

export type FaqEntryLd = { question: string; answer: string };

export function faqPageJsonLd(entries: readonly FaqEntryLd[]): object {
  return {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: entries.map((f) => ({
      "@type": "Question",
      name: f.question,
      acceptedAnswer: { "@type": "Answer", text: f.answer },
    })),
  };
}
