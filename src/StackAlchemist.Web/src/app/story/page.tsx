import type { Metadata } from "next";
import Link from "next/link";
import { Logo } from "@/components/logo";

export const metadata: Metadata = {
  title: "Our Story",
  description:
    "The origin story behind StackAlchemist — how a frustration with AI codegen that doesn't compile became a platform for AI-generated SaaS you actually own.",
  alternates: { canonical: "/story" },
};

export default function StoryPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      {/* Header */}
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Logo />
          <div className="flex items-center gap-6">
            <Link href="/about" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">About</Link>
            <Link href="/pricing" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Pricing</Link>
            <Link href="/" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Build</Link>
            <Link href="/login" className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase">
              Login
            </Link>
          </div>
        </nav>
      </header>

      <main className="flex-1">

        {/* Hero — the title reveal */}
        <section className="border-b border-slate-surface py-24 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-xs tracking-[0.3em] text-electric uppercase mb-8">
              The Name
            </p>
            <div className="space-y-2">
              <h1 className="font-mono text-6xl md:text-8xl font-bold tracking-tight">
                <span className="text-electric">STACK</span>
              </h1>
              <h1 className="font-mono text-6xl md:text-8xl font-bold tracking-tight">
                <span className="text-white">ALCHE</span><span className="text-emerald">MIST</span>
              </h1>
            </div>
            <p className="mt-8 text-slate-400 text-base leading-relaxed max-w-xl">
              Two words. Two worlds. One name that meant something to the person who built this —
              and hopefully means something to the developers who use it.
            </p>
          </div>
        </section>

        {/* Stack */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto grid grid-cols-1 md:grid-cols-12 gap-12">
            <div className="md:col-span-3">
              <span className="font-mono text-7xl font-bold text-electric/20 leading-none select-none">
                01
              </span>
            </div>
            <div className="md:col-span-9 space-y-6">
              <div>
                <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-2">The First Word</p>
                <h2 className="font-mono text-4xl font-bold text-electric tracking-tight">Stack</h2>
              </div>
              <div className="space-y-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
                <p>
                  In software, the stack is everything. It&apos;s the foundation that every line
                  of business logic rests on. Choose the wrong stack and you&apos;re fighting your
                  tools for the life of the product. Choose the right one and it disappears —
                  infrastructure so well-suited to the problem that it becomes invisible.
                </p>
                <p>
                  The stack is what separates a prototype from a product, a weekend project
                  from a company. It carries a kind of gravity that most technical decisions
                  don&apos;t. You don&apos;t easily change it. You live with it.
                </p>
                <p>
                  StackAlchemist exists because the stack you choose should be the product
                  of thought and craft — not the tax you pay before the real work begins.
                  The name is a reminder of what we&apos;re generating: not just code, but
                  the architectural foundation your idea deserves.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Alchemist */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto grid grid-cols-1 md:grid-cols-12 gap-12">
            <div className="md:col-span-3">
              <span className="font-mono text-7xl font-bold text-emerald/20 leading-none select-none">
                02
              </span>
            </div>
            <div className="md:col-span-9 space-y-6">
              <div>
                <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-2">The Second Word</p>
                <h2 className="font-mono text-4xl font-bold text-emerald tracking-tight">Alchemist</h2>
              </div>
              <div className="space-y-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
                <p>
                  Alchemy was the ancient pursuit of transformation. Lead into gold. The
                  raw into the refined. The ordinary elevated into something of enduring value.
                  The alchemist was part scientist, part philosopher — someone who believed
                  that with the right process, anything base could become something precious.
                </p>
                <p>
                  That&apos;s what we do. You come with an idea — something raw, unformed,
                  still living in the space between napkin sketch and reality. We apply
                  the process. We transmute it. What leaves is compiled, structured,
                  deployable code. Your plain idea, turned into architecture. Your
                  rough mental model, turned into gold.
                </p>
                <p>
                  The metaphor isn&apos;t decorative. The transmutation pipeline, the
                  generation engine, the LLM that fills the holes in the templates —
                  these are the tools of the trade. The output is the gold.
                  You already had the idea. We just have the furnace.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* The Book */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto grid grid-cols-1 md:grid-cols-12 gap-12">
            <div className="md:col-span-3">
              <span className="font-mono text-7xl font-bold text-slate-700 leading-none select-none">
                03
              </span>
            </div>
            <div className="md:col-span-9 space-y-6">
              <div>
                <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-2">The Inspiration</p>
                <h2 className="font-mono text-4xl font-bold text-white tracking-tight leading-tight">
                  The Alchemist<span className="text-slate-600">.</span>
                </h2>
                <p className="font-mono text-xs text-slate-500 mt-1">Paulo Coelho, 1988</p>
              </div>
              <div className="space-y-4 text-slate-400 text-sm leading-relaxed max-w-2xl">
                <p>
                  There is a book called <em className="text-white not-italic">The Alchemist</em> — a novel by Paulo Coelho that has
                  quietly changed the way a lot of people think about the work they choose to do
                  with their lives. It is the story of a shepherd who chases a dream across
                  continents, and of the alchemist who teaches him that the treasure he seeks
                  was never something he needed to find — it was something he needed to become.
                </p>
                <p>
                  The soul of that book is the idea of the Personal Legend: the thing you were
                  meant to build, the work you were meant to do. The universe, Coelho writes,
                  conspires to help those who follow it. The whole cosmos bends toward the person
                  who has the courage to pursue their dream with everything they have.
                </p>
                <p>
                  The builder of this tool loved that book. And spent years as a software
                  developer. The fusion was inevitable: the alchemist who transforms the raw
                  into gold, and the developer who transforms an idea into a product. Two kinds
                  of craft. The same underlying pursuit.
                </p>
                <p>
                  <em className="text-slate-300 not-italic">
                    &ldquo;When you want something, all the universe conspires in helping you to achieve it.&rdquo;
                  </em>
                </p>
                <p className="text-slate-600 text-xs font-mono">— Paulo Coelho, The Alchemist</p>
                <p>
                  StackAlchemist is what happens when you apply that conviction to the
                  act of building software. You have the idea. The universe — in the form
                  of a well-engineered generation pipeline — conspires to give you the architecture.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* The Fusion */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <div className="border border-slate-border bg-slate-surface/20 p-10 md:p-14 space-y-8">
              <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">
                Putting It Together
              </p>
              <div className="space-y-6 text-slate-300 text-base leading-relaxed max-w-2xl">
                <p>
                  <span className="text-electric font-bold font-mono">Stack</span> because
                  the architecture is the foundation. It is the decision that every future
                  decision depends on. It is not boilerplate — it is the skeleton of the
                  thing you are building.
                </p>
                <p>
                  <span className="text-emerald font-bold font-mono">Alchemist</span> because
                  the process is transmutation. Raw material — your description, your schema,
                  your intent — is passed through the furnace and comes out as something
                  refined. Something valuable. Something real.
                </p>
                <p>
                  And because a developer who loved a book about following your dream and
                  doing the work that only you can do decided, one day, to build the tool
                  they always wished existed. To fuse the literary with the technical.
                  To give the name a soul.
                </p>
                <p>
                  That is what this is.
                  <br />
                  <span className="text-white font-bold">StackAlchemist.</span>
                  <br />
                  <span className="text-slate-500 font-mono text-sm">
                    Your idea. Our furnace. Your gold.
                  </span>
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* CTA */}
        <section className="py-20 px-4 text-center">
          <div className="max-w-lg mx-auto space-y-6">
            <p className="font-mono text-xs tracking-[0.3em] text-slate-500 uppercase">
              Begin the Transmutation
            </p>
            <h2 className="font-mono text-2xl font-bold text-white tracking-tight">
              What are you building?
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed">
              Describe it. We&apos;ll handle the architecture.
            </p>
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <Link href="/" className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-6 py-2.5 uppercase tracking-widest transition-colors">
                Start Building →
              </Link>
              <Link href="/about" className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors">
                How It Works
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
            <Link href="/about" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">About</Link>
            <Link href="/pricing" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Pricing</Link>
            <Link href="/" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Build</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
