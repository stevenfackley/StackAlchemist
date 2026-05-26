import type { MetadataRoute } from "next";
import { DOCS, type DocMeta } from "@/lib/docs-manifest";
import { BLOG_POSTS, type BlogPostMeta } from "@/lib/blog-manifest";
import { COMPARE_ENTRIES } from "@/lib/compare-manifest";
import { SOLUTION_ENTRIES } from "@/lib/solutions-manifest";
import { SITE_URL } from "@/lib/constants";

const siteUrl = SITE_URL;

// Per-route `lastModified` is a real freshness signal — Google de-weights
// sitemap dates that always equal "now". Each entry below carries the last
// meaningful content change for that page. Bump when copy actually changes.
// Auth pages (/login, /register) and the /docs redirect are intentionally
// omitted; per-page robots noindex covers login/register, and /docs/getting-started
// is reachable via its real URL below.
const publicRoutes = [
  { url: "/", priority: 1, changeFrequency: "weekly" as const, lastModified: "2026-05-09" },
  { url: "/about", priority: 0.8, changeFrequency: "monthly" as const, lastModified: "2026-04-17" },
  { url: "/story", priority: 0.8, changeFrequency: "monthly" as const, lastModified: "2026-04-17" },
  { url: "/pricing", priority: 0.9, changeFrequency: "monthly" as const, lastModified: "2026-05-09" },
  { url: "/simple", priority: 0.9, changeFrequency: "weekly" as const, lastModified: "2026-04-17" },
  { url: "/advanced", priority: 0.9, changeFrequency: "weekly" as const, lastModified: "2026-04-17" },
  { url: "/faq", priority: 0.8, changeFrequency: "monthly" as const, lastModified: "2026-04-20" },
  { url: "/blog", priority: 0.8, changeFrequency: "weekly" as const, lastModified: "2026-05-09" },
  { url: "/compare", priority: 0.8, changeFrequency: "monthly" as const, lastModified: "2026-05-09" },
  { url: "/solutions", priority: 0.8, changeFrequency: "monthly" as const, lastModified: "2026-05-09" },
  { url: "/contact", priority: 0.5, changeFrequency: "yearly" as const, lastModified: "2026-04-17" },
  { url: "/privacy", priority: 0.3, changeFrequency: "yearly" as const, lastModified: "2026-04-17" },
  { url: "/terms", priority: 0.3, changeFrequency: "yearly" as const, lastModified: "2026-04-17" },
];

export default function sitemap(): MetadataRoute.Sitemap {
  return [
    ...publicRoutes.map((route) => ({
      url: `${siteUrl}${route.url}`,
      lastModified: new Date(route.lastModified),
      priority: route.priority,
      changeFrequency: route.changeFrequency,
    })),
    // Cast widens `as const satisfies` narrowing so we can read the optional
    // `updatedAt` field; falls back to a sensible default when posts/docs
    // haven't been explicitly revised.
    ...DOCS.map((doc) => ({
      url: `${siteUrl}/docs/${doc.slug}`,
      lastModified: new Date((doc as DocMeta).updatedAt ?? "2026-04-17"),
      priority: doc.section === "user" ? 0.6 : 0.5,
      changeFrequency: "monthly" as const,
    })),
    ...BLOG_POSTS.map((post) => ({
      url: `${siteUrl}/blog/${post.slug}`,
      lastModified: new Date((post as BlogPostMeta).updatedAt ?? post.publishedAt),
      priority: 0.7,
      changeFrequency: "monthly" as const,
    })),
    ...COMPARE_ENTRIES.map((entry) => ({
      url: `${siteUrl}/compare/${entry.slug}`,
      lastModified: new Date(entry.updatedAt),
      priority: 0.75,
      changeFrequency: "monthly" as const,
    })),
    ...SOLUTION_ENTRIES.map((entry) => ({
      url: `${siteUrl}/solutions/${entry.slug}`,
      lastModified: new Date(entry.updatedAt),
      priority: 0.75,
      changeFrequency: "monthly" as const,
    })),
  ];
}
