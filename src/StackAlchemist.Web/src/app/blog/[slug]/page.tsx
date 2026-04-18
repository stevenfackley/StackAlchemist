import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { DocsMarkdown } from "@/components/docs-markdown";
import { ContentHeader } from "@/components/content-header";
import { getBlogPostBySlug } from "@/lib/blog";
import { getAllBlogSlugs, getRelatedBlogPosts, getSortedBlogPosts } from "@/lib/blog-manifest";
import { blogPostingJsonLd, breadcrumbJsonLd } from "@/lib/jsonld";
import { SITE_URL } from "@/lib/constants";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getAllBlogSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const post = getBlogPostBySlug(slug);
  if (!post) return {};

  return {
    title: `${post.meta.title} — StackAlchemist Blog`,
    description: post.meta.description,
    alternates: { canonical: `/blog/${slug}` },
    authors: [{ name: post.meta.author }],
    openGraph: {
      title: post.meta.title,
      description: post.meta.description,
      url: `/blog/${slug}`,
      type: "article",
      publishedTime: post.meta.publishedAt,
      authors: [post.meta.author],
      tags: [...post.meta.tags],
    },
    twitter: {
      card: "summary_large_image",
      title: post.meta.title,
      description: post.meta.description,
    },
  };
}

export default async function BlogPostPage({ params }: Props) {
  const { slug } = await params;
  const post = getBlogPostBySlug(slug);
  if (!post) notFound();

  const siteUrl = SITE_URL;

  const articleLd = blogPostingJsonLd(siteUrl, slug, post.meta);
  const breadcrumbLd = breadcrumbJsonLd([
    { name: "Home", item: `${siteUrl}/` },
    { name: "Blog", item: `${siteUrl}/blog` },
    { name: post.meta.title, item: `${siteUrl}/blog/${slug}` },
  ]);

  const sorted = getSortedBlogPosts();
  const idx = sorted.findIndex((p) => p.slug === slug);
  const prev = idx < sorted.length - 1 ? sorted[idx + 1] : null;
  const next = idx > 0 ? sorted[idx - 1] : null;
  const related = getRelatedBlogPosts(slug, 2);

  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-3xl mx-auto py-16 px-6">
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(articleLd) }}
          />
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbLd) }}
          />

          <Link
            href="/blog"
            className="inline-flex items-center gap-1 text-slate-300 hover:text-white font-mono text-[10px] tracking-widest uppercase mb-8"
          >
            <ChevronLeft className="h-3 w-3" />
            Back to blog
          </Link>

          <div className="mb-8 pb-6 border-b border-slate-700/50">
            <div className="flex flex-wrap items-center gap-3 text-xs font-mono tracking-widest uppercase text-slate-400">
              <span>
                By{" "}
                <Link href="/about" className="text-white hover:text-electric transition-colors">
                  {post.meta.author}
                </Link>
              </span>
              <span className="text-slate-600">·</span>
              <time dateTime={post.meta.publishedAt}>
                {new Date(post.meta.publishedAt).toLocaleDateString("en-US", {
                  year: "numeric",
                  month: "short",
                  day: "numeric",
                })}
              </time>
            </div>
          </div>

          <DocsMarkdown content={post.content} />

          {related.length > 0 && (
            <aside className="mt-16 pt-8 border-t border-slate-700/50">
              <h2 className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric mb-5">
                Related reading
              </h2>
              <div className="grid gap-4 sm:grid-cols-2">
                {related.map((r) => (
                  <Link
                    key={r.slug}
                    href={`/blog/${r.slug}`}
                    className="group block p-5 border border-slate-700/40 rounded-lg hover:border-electric/50 hover:bg-slate-800/30 transition-all"
                  >
                    <h3 className="text-base font-semibold text-white group-hover:text-electric transition-colors leading-tight">
                      {r.title}
                    </h3>
                    <p className="mt-2 text-slate-300 text-sm leading-relaxed">
                      {r.description}
                    </p>
                    <div className="mt-3 font-mono text-[10px] tracking-widest text-slate-400 uppercase">
                      {r.readingTimeMinutes} min read
                    </div>
                  </Link>
                ))}
              </div>
            </aside>
          )}

          <div className="mt-16 pt-6 border-t border-slate-700/50 grid grid-cols-2 gap-4">
            <div>
              {prev && (
                <Link
                  href={`/blog/${prev.slug}`}
                  className="group flex flex-col gap-1 p-4 rounded-lg border border-slate-700/40 hover:border-electric/30 hover:bg-slate-700/30 transition-all"
                >
                  <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                    ← Older
                  </span>
                  <span className="text-sm text-slate-300 group-hover:text-white transition-colors">
                    {prev.title}
                  </span>
                </Link>
              )}
            </div>
            <div className="flex justify-end">
              {next && (
                <Link
                  href={`/blog/${next.slug}`}
                  className="group flex flex-col gap-1 p-4 rounded-lg border border-slate-700/40 hover:border-electric/30 hover:bg-slate-700/30 transition-all text-right w-full"
                >
                  <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                    Newer →
                  </span>
                  <span className="text-sm text-slate-300 group-hover:text-white transition-colors">
                    {next.title}
                  </span>
                </Link>
              )}
            </div>
          </div>
        </article>
      </main>
    </div>
  );
}
