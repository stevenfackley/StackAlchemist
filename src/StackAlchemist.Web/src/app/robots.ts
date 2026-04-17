import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/constants";

const siteUrl = SITE_URL;

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
