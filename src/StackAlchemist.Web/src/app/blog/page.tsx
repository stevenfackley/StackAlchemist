import type { Metadata } from "next";
import Link from "next/link";
import { getSortedBlogPosts } from "@/lib/blog-manifest";
import { ContentHeader } from "@/components/content-header";
import { blogIndexJsonLd } from "@/lib/jsonld";

export const metadata: Metadata = {
  title: "Blog — AI Codegen, SaaS Generation, and the Compile Guarantee",
  description:
    "Founder-written essays on AI code generation, the Swiss Cheese Method, and why StackAlchemist ships verified full-stack SaaS repos instead of broken zips. By Steve Ackley.",
  alternates: { canonical: "/blog" },
  openGraph: {
    title: "StackAlchemist Blog",
    description:
      "Founder-written essays on AI code generation, verified codegen, and the economics of AI-generated SaaS. By Steve Ackley.",
    url: "/blog",
    type: "website",
  },
};

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

export default function BlogIndexPage() {
  const posts = getSortedBlogPosts();
  const siteUrl = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");

  const ldJson = blogIndexJsonLd(siteUrl, posts);

  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-4xl mx-auto py-16 px-6">
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(ldJson) }}
          />

          <header className="mb-14">
            <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
              Blog
            </span>
            <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
              Notes from the founder
            </h1>
            <p className="mt-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
              Opinionated essays on AI code generation, the Swiss Cheese Method, and the economics of
              shipping SaaS in 2026. Written by Steve Ackley, founder of StackAlchemist.
            </p>
          </header>

          <div className="divide-y divide-slate-700/50">
            {posts.map((post) => (
              <Link
                key={post.slug}
                href={`/blog/${post.slug}`}
                className="group block py-8 hover:bg-slate-800/30 -mx-4 px-4 rounded-lg transition-colors"
              >
                <div className="flex items-center gap-3 mb-3">
                  <time className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                    {formatDate(post.publishedAt)}
                  </time>
                  <span className="text-slate-700">·</span>
                  <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                    {post.readingTimeMinutes} min read
                  </span>
                </div>
                <h2 className="text-xl sm:text-2xl font-semibold text-white group-hover:text-electric transition-colors leading-tight">
                  {post.title}
                </h2>
                <p className="mt-3 text-slate-400 text-sm leading-relaxed">{post.description}</p>
                <div className="mt-4 flex flex-wrap gap-2">
                  {post.tags.map((tag) => (
                    <span
                      key={tag}
                      className="font-mono text-[10px] tracking-wider text-slate-500 px-2 py-0.5 border border-slate-700/60 rounded"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              </Link>
            ))}
          </div>
        </article>
      </main>
    </div>
  );
}
