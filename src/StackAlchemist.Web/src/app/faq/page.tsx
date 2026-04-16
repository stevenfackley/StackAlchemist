import type { Metadata } from "next";
import Link from "next/link";
import { Logo } from "@/components/logo";
import { FAQS, FAQ_CATEGORIES } from "@/lib/faq-manifest";
import { faqPageJsonLd } from "@/lib/jsonld";

export const metadata: Metadata = {
  title: "FAQ — StackAlchemist",
  description:
    "Common questions about StackAlchemist: the Compile Guarantee, Swiss Cheese Method, pricing tiers, how it compares to v0 / Bolt.new / Lovable, what stack the generated code uses, and more.",
  alternates: { canonical: "/faq" },
  openGraph: {
    title: "StackAlchemist FAQ — AI SaaS Generator",
    description:
      "Answers to the most common questions about StackAlchemist — the AI SaaS generator with a 100% compile guarantee.",
  },
};

const faqLd = faqPageJsonLd(FAQS);

export default function FaqPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Logo />
          <div className="flex items-center gap-6">
            <Link href="/pricing" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Pricing</Link>
            <Link href="/about" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">About</Link>
            <Link href="/" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Build</Link>
            <Link href="/login" className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase">
              Login
            </Link>
          </div>
        </nav>
      </header>

      <main className="flex-1">
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(faqLd) }}
        />

        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-xs tracking-[0.3em] text-electric uppercase mb-6">
              Frequently Asked Questions
            </p>
            <h1 className="font-mono text-4xl md:text-5xl font-bold text-white tracking-tight leading-tight mb-8">
              THE ANSWERS.
              <br />
              <span className="text-electric">CONDENSED</span>.
            </h1>
            <p className="text-slate-400 text-base leading-relaxed max-w-2xl">
              Short, plain answers to the questions we hear most. For deeper
              technical reading, see the{" "}
              <Link href="/docs" className="text-electric hover:underline">
                documentation
              </Link>
              .
            </p>
          </div>
        </section>

        {FAQ_CATEGORIES.map((cat) => {
          const entries = FAQS.filter((f) => f.category === cat.slug);
          if (entries.length === 0) return null;
          return (
            <section key={cat.slug} className="border-b border-slate-surface py-16 px-4">
              <div className="max-w-4xl mx-auto">
                <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-10">
                  {cat.label}
                </p>
                <div className="space-y-10">
                  {entries.map((entry) => (
                    <article key={entry.question} className="space-y-3">
                      <h2 className="font-mono text-lg font-bold text-white leading-snug">
                        {entry.question}
                      </h2>
                      <p className="text-slate-400 text-sm leading-relaxed">
                        {entry.answer}
                      </p>
                    </article>
                  ))}
                </div>
              </div>
            </section>
          );
        })}

        <section className="py-20 px-4 text-center">
          <div className="max-w-lg mx-auto space-y-6">
            <h2 className="font-mono text-2xl font-bold text-white tracking-tight">
              Still have questions?
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed">
              Dig into the docs or just start building — the free Spark tier
              lets you explore the schema workflow end-to-end.
            </p>
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <Link href="/" className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-6 py-2.5 uppercase tracking-widest transition-colors">
                Start Building →
              </Link>
              <Link href="/docs" className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors">
                Read the Docs
              </Link>
              <Link href="/pricing" className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors">
                See Pricing
              </Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-slate-surface py-6 px-4">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          <p className="font-mono text-xs text-slate-600 tracking-widest uppercase">
            StackAlchemist · Transmutation Engine v1.0
          </p>
          <div className="flex gap-6">
            <Link href="/pricing" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Pricing</Link>
            <Link href="/docs" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Docs</Link>
            <Link href="/" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Build</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
