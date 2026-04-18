import { ImageResponse } from "next/og";
import { getBlogPostMetaBySlug, getAllBlogSlugs } from "@/lib/blog-manifest";

// Dynamic OG image per blog post — inherits the brand gradient from the root
// /opengraph-image.tsx but swaps in the post's title and publish date. Rendered
// at build time for each slug via generateStaticParams so social scrapers hit
// a CDN-cached PNG, not a warm Node render.

export const runtime = "nodejs";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

const VOID = "#0f172a";
const VOID_ACCENT = "#1e293b";
const ELECTRIC = "#4da6ff";
const EMERALD = "#10b981";
const SLATE = "#94a3b8";

export async function generateImageMetadata({ params }: { params: { slug: string } }) {
  const post = getBlogPostMetaBySlug(params.slug);
  return [
    {
      id: params.slug,
      alt: post ? `${post.title} — StackAlchemist Blog` : "StackAlchemist Blog",
      contentType,
      size,
    },
  ];
}

export function generateStaticParams() {
  return getAllBlogSlugs().map((slug) => ({ slug }));
}

export default async function OgImage({ params }: { params: { slug: string } }) {
  const post = getBlogPostMetaBySlug(params.slug);
  const title = post?.title ?? "StackAlchemist Blog";
  const date = post
    ? new Date(post.publishedAt).toLocaleDateString("en-US", {
        year: "numeric",
        month: "long",
        day: "numeric",
      })
    : "";
  const tag = post?.tags[0] ?? "Blog";

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          padding: "64px",
          background: `linear-gradient(135deg, ${VOID} 0%, ${VOID_ACCENT} 100%)`,
          color: "#ffffff",
          fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, sans-serif",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
          <div
            style={{
              width: 52,
              height: 52,
              borderRadius: 8,
              background: ELECTRIC,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 30,
              fontWeight: 800,
              color: VOID,
            }}
          >
            SA
          </div>
          <span style={{ fontSize: 28, fontWeight: 700, letterSpacing: -0.5 }}>
            StackAlchemist Blog
          </span>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 28 }}>
          <div
            style={{
              fontSize: 20,
              letterSpacing: 6,
              textTransform: "uppercase",
              color: ELECTRIC,
              fontWeight: 600,
            }}
          >
            {tag}
          </div>
          <div
            style={{
              fontSize: title.length > 60 ? 60 : 72,
              fontWeight: 800,
              lineHeight: 1.1,
              letterSpacing: -1.5,
              maxWidth: 1070,
            }}
          >
            {title}
          </div>
        </div>

        <div style={{ display: "flex", gap: 32, alignItems: "center" }}>
          <div
            style={{
              fontSize: 18,
              fontWeight: 600,
              padding: "10px 16px",
              borderRadius: 4,
              background: EMERALD,
              color: VOID,
              letterSpacing: 1,
              textTransform: "uppercase",
            }}
          >
            By Steve Ackley
          </div>
          <span style={{ fontSize: 22, color: SLATE }}>{date}</span>
          <span style={{ fontSize: 22, color: SLATE, marginLeft: "auto" }}>
            stackalchemist.app
          </span>
        </div>
      </div>
    ),
    { ...size }
  );
}
