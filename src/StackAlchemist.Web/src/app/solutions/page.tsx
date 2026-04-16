import type { Metadata } from "next";
import Link from "next/link";
import { ContentHeader } from "@/components/content-header";
import { SOLUTION_ENTRIES } from "@/lib/solutions-manifest";

export const metadata: Metadata = {
  title: "Solutions — StackAlchemist Generators by Vertical",
  description:
    "Generate a production-shaped SaaS for your vertical. AI e-commerce, LMS, fintech, and more — all .NET 10 + Next.js 15, compile-verified, owned outright.",
  alternates: { canonical: "/solutions" },
  openGraph: {
    title: "StackAlchemist Solutions",
    description:
      "Vertical-specific generators: AI e-commerce platform, LMS, fintech, and more. Compile-verified full-stack SaaS you own.",
    url: "/solutions",
    type: "website",
  },
};

export default function SolutionsIndexPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-4xl mx-auto py-16 px-6">
          <header className="mb-14">
            <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
              Solutions
            </span>
            <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
              Generators by vertical
            </h1>
            <p className="mt-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
              StackAlchemist generates production-shaped SaaS repositories tuned to specific
              verticals. Each page below describes the entities, workflows, and integrations you
              can expect from a prompt in that domain.
            </p>
          </header>

          <div className="grid gap-4 sm:grid-cols-2">
            {SOLUTION_ENTRIES.map((entry) => (
              <Link
                key={entry.slug}
                href={`/solutions/${entry.slug}`}
                className="group block p-6 border border-slate-700/50 rounded-lg hover:border-electric/50 hover:bg-slate-800/30 transition-all"
              >
                <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                  Vertical
                </span>
                <h2 className="mt-2 text-lg font-semibold text-white group-hover:text-electric transition-colors">
                  {entry.vertical}
                </h2>
                <p className="mt-3 text-slate-400 text-sm leading-relaxed">
                  {entry.metaDescription}
                </p>
                <div className="mt-4 flex flex-wrap gap-1">
                  {entry.entityExamples.slice(0, 5).map((ent) => (
                    <span
                      key={ent}
                      className="font-mono text-[10px] tracking-wider text-slate-500 px-2 py-0.5 border border-slate-700/60 rounded"
                    >
                      {ent}
                    </span>
                  ))}
                  {entry.entityExamples.length > 5 ? (
                    <span className="font-mono text-[10px] tracking-wider text-slate-600 px-2 py-0.5">
                      +{entry.entityExamples.length - 5}
                    </span>
                  ) : null}
                </div>
              </Link>
            ))}
          </div>

          <section className="mt-16 p-6 border border-slate-700/40 rounded-lg bg-slate-800/20">
            <h3 className="text-sm font-semibold text-white uppercase tracking-widest mb-2">
              More verticals coming
            </h3>
            <p className="text-slate-400 text-sm">
              We ship one new vertical solution page per month. Next up: CRM generator, marketplace
              generator, fitness-subscription generator, scheduling SaaS, analytics SaaS.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
