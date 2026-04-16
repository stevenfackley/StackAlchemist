import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { DocsMarkdown } from "@/components/docs-markdown";
import { ContentHeader } from "@/components/content-header";
import { getCompareBySlug } from "@/lib/compare";
import { getAllCompareSlugs } from "@/lib/compare-manifest";
import { breadcrumbJsonLd } from "@/lib/jsonld";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getAllCompareSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const entry = getCompareBySlug(slug);
  if (!entry) return {};

  return {
    title: `${entry.meta.competitorName} vs StackAlchemist — honest comparison`,
    description: entry.meta.tagline,
    alternates: { canonical: `/compare/${slug}` },
    openGraph: {
      title: `StackAlchemist vs ${entry.meta.competitorName}`,
      description: entry.meta.tagline,
      url: `/compare/${slug}`,
      type: "article",
    },
    twitter: {
      card: "summary_large_image",
      title: `StackAlchemist vs ${entry.meta.competitorName}`,
      description: entry.meta.tagline,
    },
  };
}

export default async function ComparePage({ params }: Props) {
  const { slug } = await params;
  const entry = getCompareBySlug(slug);
  if (!entry) notFound();

  const siteUrl = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");

  const breadcrumbLd = breadcrumbJsonLd([
    { name: "Home", item: `${siteUrl}/` },
    { name: "Compare", item: `${siteUrl}/compare` },
    { name: `vs ${entry.meta.competitorName}`, item: `${siteUrl}/compare/${slug}` },
  ]);

  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-3xl mx-auto py-16 px-6">
          <script
            type="application/ld+json"
            dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbLd) }}
          />

          <Link
            href="/compare"
            className="inline-flex items-center gap-1 text-slate-500 hover:text-slate-300 font-mono text-[10px] tracking-widest uppercase mb-8"
          >
            <ChevronLeft className="h-3 w-3" />
            All comparisons
          </Link>

          <DocsMarkdown content={entry.content} />

          <section className="mt-16 p-6 border border-electric/40 rounded-lg bg-electric/5">
            <h3 className="text-sm font-semibold text-white uppercase tracking-widest mb-3">
              The short answer
            </h3>
            <p className="text-slate-300 text-sm leading-relaxed mb-5">{entry.meta.verdict}</p>
            <Link
              href="/simple"
              className="inline-flex items-center gap-2 font-mono text-xs tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-4 py-2 uppercase"
            >
              Generate a SaaS
            </Link>
          </section>
        </article>
      </main>
    </div>
  );
}
