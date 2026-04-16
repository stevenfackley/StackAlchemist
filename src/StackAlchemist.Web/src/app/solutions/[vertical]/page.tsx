import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { DocsMarkdown } from "@/components/docs-markdown";
import { ContentHeader } from "@/components/content-header";
import { getSolutionContentBySlug } from "@/lib/solutions";
import { getAllSolutionSlugs } from "@/lib/solutions-manifest";
import { breadcrumbJsonLd } from "@/lib/jsonld";

interface Props {
  params: Promise<{ vertical: string }>;
}

export async function generateStaticParams() {
  return getAllSolutionSlugs().map((vertical) => ({ vertical }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { vertical } = await params;
  const entry = getSolutionContentBySlug(vertical);
  if (!entry) return {};

  return {
    title: entry.meta.metaTitle,
    description: entry.meta.metaDescription,
    alternates: { canonical: `/solutions/${vertical}` },
    keywords: [...entry.meta.primaryKeywords],
    openGraph: {
      title: entry.meta.h1,
      description: entry.meta.metaDescription,
      url: `/solutions/${vertical}`,
      type: "article",
    },
    twitter: {
      card: "summary_large_image",
      title: entry.meta.h1,
      description: entry.meta.metaDescription,
    },
  };
}

export default async function SolutionPage({ params }: Props) {
  const { vertical } = await params;
  const entry = getSolutionContentBySlug(vertical);
  if (!entry) notFound();

  const siteUrl = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");

  const breadcrumbLd = breadcrumbJsonLd([
    { name: "Home", item: `${siteUrl}/` },
    { name: "Solutions", item: `${siteUrl}/solutions` },
    { name: entry.meta.vertical, item: `${siteUrl}/solutions/${vertical}` },
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
            href="/solutions"
            className="inline-flex items-center gap-1 text-slate-500 hover:text-slate-300 font-mono text-[10px] tracking-widest uppercase mb-8"
          >
            <ChevronLeft className="h-3 w-3" />
            All solutions
          </Link>

          <DocsMarkdown content={entry.content} />

          <section className="mt-16 p-6 border border-electric/40 rounded-lg bg-electric/5">
            <h3 className="text-sm font-semibold text-white uppercase tracking-widest mb-3">
              Ready to generate?
            </h3>
            <p className="text-slate-300 text-sm leading-relaxed mb-5">
              Describe your {entry.meta.vertical.toLowerCase()} in plain English. Compile-verified
              code in 12 minutes. Owned outright.
            </p>
            <Link
              href="/simple"
              className="inline-flex items-center gap-2 font-mono text-xs tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-4 py-2 uppercase"
            >
              Start generating
            </Link>
          </section>
        </article>
      </main>
    </div>
  );
}
