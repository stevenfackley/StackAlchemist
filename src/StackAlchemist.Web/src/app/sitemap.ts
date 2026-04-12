import type { MetadataRoute } from "next";
import { DOCS } from "@/lib/docs-manifest";

const siteUrl = process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000";

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
  ];
}
