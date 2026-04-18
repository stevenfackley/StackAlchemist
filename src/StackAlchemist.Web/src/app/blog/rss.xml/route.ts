import { getSortedBlogPosts } from "@/lib/blog-manifest";
import { SITE_URL } from "@/lib/constants";

// RSS 2.0 feed for the blog. Static at request time — no DB, no caching layer
// needed; Next.js route handlers are edge-cacheable when there is no dynamic
// input. Consumed by readers (Feedly, NetNewsWire) and aggregators (HN, Reddit
// auto-poster hooks).

function escapeXml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

export async function GET() {
  const posts = getSortedBlogPosts();
  const siteUrl = SITE_URL;
  const lastBuild =
    posts[0]?.publishedAt ? new Date(posts[0].publishedAt).toUTCString() : new Date().toUTCString();

  const items = posts
    .map((p) => {
      const url = `${siteUrl}/blog/${p.slug}`;
      return [
        "    <item>",
        `      <title>${escapeXml(p.title)}</title>`,
        `      <link>${url}</link>`,
        `      <guid isPermaLink="true">${url}</guid>`,
        `      <pubDate>${new Date(p.publishedAt).toUTCString()}</pubDate>`,
        `      <description>${escapeXml(p.description)}</description>`,
        `      <author>hello@stackalchemist.app (${escapeXml(p.author)})</author>`,
        ...p.tags.map((t) => `      <category>${escapeXml(t)}</category>`),
        "    </item>",
      ].join("\n");
    })
    .join("\n");

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <title>StackAlchemist Blog</title>
    <link>${siteUrl}/blog</link>
    <atom:link href="${siteUrl}/blog/rss.xml" rel="self" type="application/rss+xml" />
    <description>Founder essays from Steve Ackley on AI code generation, the Swiss Cheese Method, and the economics of verified SaaS codegen.</description>
    <language>en-us</language>
    <lastBuildDate>${lastBuild}</lastBuildDate>
${items}
  </channel>
</rss>
`;

  return new Response(xml, {
    headers: {
      "Content-Type": "application/rss+xml; charset=utf-8",
      "Cache-Control": "public, s-maxage=3600, stale-while-revalidate=86400",
    },
  });
}
