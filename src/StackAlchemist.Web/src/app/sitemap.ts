import type { MetadataRoute } from "next";
import { DOCS } from "@/lib/docs-manifest";
import { BLOG_POSTS } from "@/lib/blog-manifest";
import { COMPARE_ENTRIES } from "@/lib/compare-manifest";
import { SOLUTION_ENTRIES } from "@/lib/solutions-manifest";
import { SITE_URL } from "@/lib/constants";

const siteUrl = SITE_URL;

const publicRoutes = [
  { url: "/", priority: 1, changeFrequency: "weekly" as const },
  { url: "/about", priority: 0.8, changeFrequency: "monthly" as const },
  { url: "/story", priority: 0.8, changeFrequency: "monthly" as const },
  { url: "/pricing", priority: 0.9, changeFrequency: "monthly" as const },
  { url: "/simple", priority: 0.9, changeFrequency: "weekly" as const },
  { url: "/advanced", priority: 0.9, changeFrequency: "weekly" as const },
  { url: "/login", priority: 0.3, changeFrequency: "monthly" as const },
  { url: "/register", priority: 0.3, changeFrequency: "monthly" as const },
  { url: "/docs", priority: 0.7, changeFrequency: "monthly" as const },
  { url: "/faq", priority: 0.8, changeFrequency: "monthly" as const },
  { url: "/blog", priority: 0.8, changeFrequency: "weekly" as const },
  { url: "/compare", priority: 0.8, changeFrequency: "monthly" as const },
  { url: "/solutions", priority: 0.8, changeFrequency: "monthly" as const },
];

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  return [
    ...publicRoutes.map((route) => ({
      url: `${siteUrl}${route.url}`,
      lastModified: now,
      priority: route.priority,
      changeFrequency: route.changeFrequency,
    })),
    ...DOCS.map((doc) => ({
      url: `${siteUrl}/docs/${doc.slug}`,
      lastModified: now,
      priority: doc.section === "user" ? 0.6 : 0.5,
      changeFrequency: "monthly" as const,
    })),
    ...BLOG_POSTS.map((post) => ({
      url: `${siteUrl}/blog/${post.slug}`,
      lastModified: new Date(post.publishedAt),
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
