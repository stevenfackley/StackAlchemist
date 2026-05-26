import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/constants";

const siteUrl = SITE_URL;

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        // Default-allow. Explicit `disallow` blocks app shell + auth pages
        // from indexation; login/register also carry per-page `robots: noindex`.
        disallow: ["/dashboard", "/generate", "/api", "/auth", "/login", "/register"],
      },
    ],
    sitemap: `${siteUrl}/sitemap.xml`,
  };
}
