import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "StackAlchemist — AI SaaS Generator",
    short_name: "StackAlchemist",
    description:
      "AI SaaS generator with a 100% compile guarantee. Turn natural language into full-stack .NET + Next.js + PostgreSQL repositories you own.",
    start_url: "/",
    display: "standalone",
    background_color: "#0a0a0f",
    theme_color: "#0a0a0f",
    icons: [
      {
        src: "/favicon.svg",
        sizes: "any",
        type: "image/svg+xml",
      },
      {
        src: "/logo.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "any",
      },
    ],
  };
}
