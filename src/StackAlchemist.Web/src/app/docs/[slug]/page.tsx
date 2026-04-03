import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import { getDocBySlug, getAllSlugs, DOCS } from "@/lib/docs";
import { DocsMarkdown } from "@/components/docs-markdown";
import { ChevronLeft, ChevronRight, ExternalLink } from "lucide-react";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getAllSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const doc = getDocBySlug(slug);
  if (!doc) return {};

  return {
    title: `${doc.meta.title} — StackAlchemist Docs`,
    description: `StackAlchemist documentation: ${doc.meta.title}`,
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

  return (
    <article className="max-w-3xl">

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
