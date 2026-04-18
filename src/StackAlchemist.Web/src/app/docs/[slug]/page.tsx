import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import { getDocBySlug, getAllSlugs, DOCS } from "@/lib/docs";
import { getBlogPostMetaBySlug } from "@/lib/blog-manifest";
import { DocsMarkdown } from "@/components/docs-markdown";
import { ChevronLeft, ChevronRight, ExternalLink, BookOpen } from "lucide-react";
import { breadcrumbJsonLd } from "@/lib/jsonld";
import { SITE_URL } from "@/lib/constants";

// Docs → blog cross-links for concept pages where the founder essay gives
// deeper "why we built it this way" context than the reference page can. Key
// off doc slug; lookup the blog post at render time so metadata stays in sync.
const DOC_TO_BLOG: Record<string, string> = {
  "swiss-cheese-method": "swiss-cheese-method-deterministic-templates-llm-logic",
  "compile-guarantee": "compile-guarantee-why-ai-codegen-must-verify",
  "tiers-and-pricing": "why-we-charge-once-not-monthly",
};

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getAllSlugs().map((slug) => ({ slug }));
}

/**
 * Pull a search-friendly excerpt out of the markdown body. Strips fenced
 * code, headings, list markers, link syntax, images, inline markers, and
 * returns the first meaningful paragraph trimmed to ~160 chars.
 */
function extractExcerpt(markdown: string, max = 160): string {
  const stripped = markdown
    .replace(/```[\s\S]*?```/g, "") // fenced code blocks
    .replace(/`[^`]*`/g, "") // inline code
    .replace(/!\[[^\]]*\]\([^)]+\)/g, "") // images
    .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1") // links → link text
    .replace(/^---[\s\S]*?---/m, ""); // yaml frontmatter

  const firstParagraph =
    stripped
      .split(/\n\s*\n/)
      .map((block) => block.trim())
      .find((block) => block.length > 0 && !block.startsWith("#") && !block.startsWith(">")) ?? "";

  const clean = firstParagraph
    .replace(/^[-*+]\s+/gm, "") // list markers
    .replace(/[*_~]/g, "") // bold/italic/strike markers
    .replace(/\s+/g, " ")
    .trim();

  if (clean.length <= max) return clean;
  return `${clean.slice(0, max - 1).trimEnd()}…`;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const doc = getDocBySlug(slug);
  if (!doc) return {};

  const excerpt = extractExcerpt(doc.content);
  const description = excerpt || `StackAlchemist documentation: ${doc.meta.title}.`;

  return {
    title: `${doc.meta.title} — StackAlchemist Docs`,
    description,
    alternates: { canonical: `/docs/${slug}` },
    openGraph: {
      title: `${doc.meta.title} — StackAlchemist Docs`,
      description,
      url: `/docs/${slug}`,
      type: "article",
    },
  };
}

export default async function DocPage({ params }: Props) {
  const { slug } = await params;
  const doc = getDocBySlug(slug);

  if (!doc) notFound();

  // Prev / Next navigation
  const allDocs = DOCS;
  const idx = allDocs.findIndex((d) => d.slug === slug);
  const prev = idx > 0 ? allDocs[idx - 1] : null;
  const next = idx < allDocs.length - 1 ? allDocs[idx + 1] : null;

  const siteUrl = SITE_URL;
  const breadcrumbLd = breadcrumbJsonLd([
    { name: "Home", item: `${siteUrl}/` },
    { name: "Docs", item: `${siteUrl}/docs` },
    { name: doc.meta.title, item: `${siteUrl}/docs/${slug}` },
  ]);

  return (
    <article className="max-w-3xl">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbLd) }}
      />

      {/* Section badge */}
      <div className="mb-6 flex items-center gap-2">
        <span
          className={`font-mono text-[10px] tracking-[0.3em] uppercase px-2 py-0.5 border ${
            doc.meta.section === "user"
              ? "text-emerald-400 border-emerald-400/30 bg-emerald-400/5"
              : "text-blue-400 border-blue-400/30 bg-blue-400/5"
          }`}
        >
          {doc.meta.section === "user" ? "User Guide" : "Advanced"}
        </span>
        <span className="font-mono text-[10px] text-slate-600 tracking-wider">
          docs / {slug}
        </span>
      </div>

      {/* Markdown content */}
      <DocsMarkdown content={doc.content} />

      {/* Founder-essay cross-link for concept pages */}
      {(() => {
        const relatedBlogSlug = DOC_TO_BLOG[slug];
        const relatedPost = relatedBlogSlug ? getBlogPostMetaBySlug(relatedBlogSlug) : undefined;
        if (!relatedPost) return null;
        return (
          <Link
            href={`/blog/${relatedPost.slug}`}
            className="group mt-10 flex items-start gap-4 p-5 border border-slate-700/40 rounded-lg hover:border-electric/50 hover:bg-slate-800/30 transition-all"
          >
            <BookOpen className="h-5 w-5 text-electric mt-0.5 shrink-0" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <div className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric mb-1">
                Read the essay
              </div>
              <h3 className="text-base font-semibold text-white group-hover:text-electric transition-colors leading-tight">
                {relatedPost.title}
              </h3>
              <p className="mt-2 text-slate-300 text-sm leading-relaxed">
                {relatedPost.description}
              </p>
            </div>
          </Link>
        );
      })()}

      {/* Prev / Next footer */}
      <div className="mt-14 pt-6 border-t border-slate-700/50 grid grid-cols-2 gap-4">
        <div>
          {prev && (
            <Link
              href={`/docs/${prev.slug}`}
              className="group flex flex-col gap-1 p-4 rounded-lg border border-slate-700/40 hover:border-blue-400/30 hover:bg-slate-700/30 transition-all"
            >
              <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase flex items-center gap-1">
                <ChevronLeft className="h-3 w-3" />
                Previous
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
              href={`/docs/${next.slug}`}
              className="group flex flex-col gap-1 p-4 rounded-lg border border-slate-700/40 hover:border-blue-400/30 hover:bg-slate-700/30 transition-all text-right w-full"
            >
              <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase flex items-center justify-end gap-1">
                Next
                <ChevronRight className="h-3 w-3" />
              </span>
              <span className="text-sm text-slate-300 group-hover:text-white transition-colors">
                {next.title}
              </span>
            </Link>
          )}
        </div>
      </div>

      {/* Edit on GitHub link */}
      <div className="mt-6 flex justify-center">
        <a
          href={`https://github.com/stevenfackley/StackAlchemist/blob/main/docs/${
            doc.meta.section === "user" ? "user" : "advanced-docs"
          }/${slug}.md`}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 font-mono text-[10px] tracking-widest text-slate-600 hover:text-slate-400 transition-colors uppercase"
        >
          <ExternalLink className="h-3 w-3" />
          Edit this page on GitHub
        </a>
      </div>
    </article>
  );
}
