import type { Metadata } from "next";
import Link from "next/link";
import { ContentHeader } from "@/components/content-header";
import { COMPARE_ENTRIES } from "@/lib/compare-manifest";

export const metadata: Metadata = {
  title: "Compare StackAlchemist vs v0, Bolt.new, Lovable",
  description:
    "Honest, founder-written comparisons between StackAlchemist and other AI code generators. Where they win, where we win, and when to pick which.",
  alternates: { canonical: "/compare" },
  openGraph: {
    title: "StackAlchemist vs — honest comparisons",
    description:
      "Side-by-side comparisons of StackAlchemist against v0, Bolt.new, and other AI codegen tools. No safe marketing — the real trade-offs.",
    url: "/compare",
    type: "website",
  },
};

export default function CompareIndexPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-4xl mx-auto py-16 px-6">
          <header className="mb-14">
            <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
              Compare
            </span>
            <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
              StackAlchemist vs the rest of the AI codegen landscape
            </h1>
            <p className="mt-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
              Honest comparisons written by the founder. Where a competitor genuinely wins, I will
              say so. Where they fall short of shipping a real owned SaaS, I will also say so.
              Click through to see the full breakdown.
            </p>
          </header>

          <div className="grid gap-4 sm:grid-cols-2">
            {COMPARE_ENTRIES.map((entry) => (
              <Link
                key={entry.slug}
                href={`/compare/${entry.slug}`}
                className="group block p-6 border border-slate-700/50 rounded-lg hover:border-electric/50 hover:bg-slate-800/30 transition-all"
              >
                <div className="flex items-center justify-between mb-3">
                  <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase">
                    vs
                  </span>
                  <span className="font-mono text-[10px] tracking-widest text-slate-600 uppercase">
                    Updated {entry.updatedAt}
                  </span>
                </div>
                <h2 className="text-lg font-semibold text-white group-hover:text-electric transition-colors">
                  {entry.competitorName}
                </h2>
                <p className="mt-3 text-slate-400 text-sm leading-relaxed">{entry.tagline}</p>
              </Link>
            ))}
          </div>

          <section className="mt-16 p-6 border border-slate-700/40 rounded-lg bg-slate-800/20">
            <h3 className="text-sm font-semibold text-white uppercase tracking-widest mb-2">
              More coming
            </h3>
            <p className="text-slate-400 text-sm">
              We ship one new comparison per month. Next up: Lovable, Cursor, Replit Agent. Have a
              tool you want us to cover? Reach out.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
