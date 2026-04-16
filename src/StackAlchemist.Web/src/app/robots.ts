import type { MetadataRoute } from "next";

const siteUrl = process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        allow: [
          "/",
          "/about",
          "/story",
          "/pricing",
          "/docs",
          "/docs/",
          "/faq",
          "/blog",
          "/blog/",
          "/compare",
          "/compare/",
          "/solutions",
          "/solutions/",
        ],
        disallow: ["/dashboard", "/generate", "/api", "/auth"],
      },
    ],
    sitemap: `${siteUrl}/sitemap.xml`,
    host: siteUrl,
  };
}
