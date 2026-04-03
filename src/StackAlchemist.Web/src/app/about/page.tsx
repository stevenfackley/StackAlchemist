import Link from "next/link";
import { Logo } from "@/components/logo";

export default function AboutPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      {/* Header */}
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Logo />
          <div className="flex items-center gap-6">
            <Link href="/pricing" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Pricing</Link>
            <Link href="/story" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Our Story</Link>
            <Link href="/" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">Build</Link>
            <Link href="/login" className="text-xs font-mono tracking-widest border border-electric text-electric hover:bg-electric hover:text-white transition-colors px-3 py-1.5 uppercase">
              Login
            </Link>
          </div>
        </nav>
      </header>

      <main className="flex-1">

        {/* Hero */}
        <section className="border-b border-slate-surface py-24 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-xs tracking-[0.3em] text-electric uppercase mb-6">
              What is StackAlchemist
            </p>
            <h1 className="font-mono text-4xl md:text-6xl font-bold text-white tracking-tight leading-tight mb-8">
              THE GAP BETWEEN
              <br />
              <span className="text-electric">IDEA</span> AND{" "}
              <span className="text-emerald">ARCHITECTURE</span>
              <br />
              IS WIDER THAN IT SHOULD BE.
            </h1>
            <p className="text-slate-400 text-base leading-relaxed max-w-2xl">
              Every SaaS starts the same way: an idea, a napkin sketch, and then
              weeks of grinding through the scaffolding before you write a single
              line of real business logic. StackAlchemist eliminates that grind.
              You describe what you&apos;re building. We hand you a compiled,
              production-ready codebase.
            </p>
          </div>
        </section>

        {/* The Problem */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-16">
            <div className="space-y-4">
              <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">The Problem</p>
              <h2 className="font-mono text-2xl font-bold text-white">
                Scaffolding is solved work. Stop solving it.
              </h2>
              <div className="space-y-4 text-slate-400 text-sm leading-relaxed">
                <p>
                  A typical SaaS scaffolding sprint — entity modeling, API layer,
                  database migrations, auth, Docker setup, folder structure — eats
                  two to four weeks before you ship anything. That&apos;s two to four
                  weeks of work that looks the same on every project.
                </p>
                <p>
                  Every developer who has built more than one thing knows this
                  feeling: you&apos;ve done this before. The pattern is familiar. The
                  implementation is just... tedious. Necessary, but not interesting.
                </p>
                <p>
                  The interesting part is the idea itself. The domain logic. The
                  thing only your product does. That&apos;s where you should be spending
                  your time.
                </p>
              </div>
            </div>
            <div className="space-y-4">
              <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">The Solution</p>
              <h2 className="font-mono text-2xl font-bold text-white">
                Describe it. Receive it. Ship it.
              </h2>
              <div className="space-y-4 text-slate-400 text-sm leading-relaxed">
                <p>
                  StackAlchemist takes a natural language description of your SaaS
                  — your entities, relationships, and API endpoints — and generates
                  a complete, compilable source repository in minutes.
                </p>
                <p>
                  Not a template. Not a starter kit. A real codebase, shaped
                  precisely around your schema, with your naming conventions,
                  your business entities, your API surface.
                </p>
                <p>
                  The repository compiles on the first try — or we auto-correct it
                  and try again. That&apos;s not a marketing promise. It&apos;s a
                  technical guarantee baked into the delivery pipeline.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* How it works */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-12">
              How It Works
            </p>
            <div className="space-y-0">
              {[
                {
                  step: "01",
                  title: "Describe Your System",
                  body: "Use Simple Mode to describe your SaaS in plain language, or Advanced Mode to model entities, relationships, and API endpoints with precision. Either way, you&apos;re in control of the schema.",
                  detail: "Simple Mode — natural language prompt\nAdvanced Mode — visual entity modeler",
                },
                {
                  step: "02",
                  title: "Choose Your Tier",
                  body: "Blueprint gives you the architecture document. Boilerplate gives you the full source code. Infrastructure gives you everything, including cloud deployment. Pick the depth you need.",
                  detail: "Blueprint · Boilerplate · Infrastructure",
                },
                {
                  step: "03",
                  title: "The Transmutation Begins",
                  body: "Our generation engine applies the Swiss Cheese Method: deterministic Handlebars templates for structure, LLM intelligence injected into the business logic layer. The result is code that looks hand-written, not generated.",
                  detail: "Template engine + LLM injection",
                },
                {
                  step: "04",
                  title: "The Compile Guarantee",
                  body: "Before your archive is assembled, the generated code is run through the actual compiler. If it fails, the error output is fed back to the LLM for correction — up to three times. Delivery only happens when the build is green.",
                  detail: "dotnet build → auto-correct → retry",
                },
                {
                  step: "05",
                  title: "Download and Ship",
                  body: "You receive a complete ZIP archive: source code, migrations, Docker Compose, environment configuration. Clone it, run docker compose up, and your dev environment is running in under five minutes.",
                  detail: "ZIP archive → run locally in minutes",
                },
              ].map((item, i) => (
                <div key={item.step} className={`grid grid-cols-1 md:grid-cols-12 gap-6 py-8 ${i < 4 ? "border-b border-slate-surface" : ""}`}>
                  <div className="md:col-span-1">
                    <span className="font-mono text-xs text-slate-600 font-bold">{item.step}</span>
                  </div>
                  <div className="md:col-span-5">
                    <h3 className="font-mono text-sm font-bold text-white tracking-wide mb-2">{item.title}</h3>
                    <p className="text-slate-400 text-sm leading-relaxed">{item.body.replace(/&apos;/g, "'")}</p>
                  </div>
                  <div className="md:col-span-6 flex items-center">
                    <div className="border border-slate-border bg-slate-surface/30 px-4 py-3 w-full">
                      {item.detail.split("\n").map((line, j) => (
                        <p key={j} className="font-mono text-xs text-electric">{line}</p>
                      ))}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* The Swiss Cheese Method */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-16 items-start">
              <div className="space-y-4">
                <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">The Method</p>
                <h2 className="font-mono text-2xl font-bold text-white">
                  The Swiss Cheese Method
                </h2>
                <div className="space-y-4 text-slate-400 text-sm leading-relaxed">
                  <p>
                    Most AI code generators produce either too much structure
                    (rigid, one-size-fits-all boilerplate) or too little
                    (hallucinated, inconsistent, non-compilable fragments).
                  </p>
                  <p>
                    StackAlchemist uses a different approach. The outer structure
                    — file layout, project references, import paths, class
                    signatures — is defined by static Handlebars templates.
                    Deterministic. Reliable. Predictable.
                  </p>
                  <p>
                    The holes in the cheese — the business logic, domain
                    validation, query implementations — are filled by the LLM,
                    guided by your schema. Intelligence applied precisely where
                    variability is expected. Structure preserved everywhere it
                    matters.
                  </p>
                </div>
              </div>
              <div className="border border-slate-border bg-slate-surface/20 p-6 space-y-4">
                <p className="font-mono text-[10px] text-slate-500 tracking-widest uppercase mb-4">Architecture</p>
                {[
                  { layer: "Templates", desc: "Static Handlebars — file structure, class skeletons, import paths", type: "deterministic" },
                  { layer: "LLM Injection", desc: "Business logic, domain rules, query implementations", type: "intelligent" },
                  { layer: "Compiler", desc: "dotnet build validation — structure verified before delivery", type: "guaranteed" },
                  { layer: "Auto-Correction", desc: "3-retry loop with compiler output fed back as context", type: "resilient" },
                ].map((item) => (
                  <div key={item.layer} className="border-l-2 border-slate-border pl-4 space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-xs text-white font-bold">{item.layer}</span>
                      <span className={`font-mono text-[10px] tracking-widest uppercase px-2 py-0.5 border ${
                        item.type === "deterministic" ? "text-emerald border-emerald/30 bg-emerald/5" :
                        item.type === "intelligent" ? "text-electric border-electric/30 bg-electric/5" :
                        item.type === "guaranteed" ? "text-yellow-400 border-yellow-400/30 bg-yellow-400/5" :
                        "text-rose border-rose/30 bg-rose/5"
                      }`}>{item.type}</span>
                    </div>
                    <p className="font-mono text-xs text-slate-500">{item.desc}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* What you get */}
        <section className="border-b border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-12">
              V1 Stack
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-px bg-slate-border">
              {[
                {
                  tech: ".NET 10 Web API",
                  role: "Backend",
                  desc: "RESTful API with controllers, repository pattern, model validation, and Entity Framework-ready data access. Clean architecture, proper layering.",
                },
                {
                  tech: "Next.js 15",
                  role: "Frontend",
                  desc: "App Router, TypeScript strict mode, Tailwind CSS. Type-safe API client generated from your schema. No config required to run.",
                },
                {
                  tech: "PostgreSQL",
                  role: "Database",
                  desc: "Schema migrations scripted from your entity definitions. Foreign keys, indexes, and constraints modeled from your relationships.",
                },
                {
                  tech: "Supabase",
                  role: "Auth + Storage",
                  desc: "Row Level Security policies scaffolded for your entities. Auth flows wired into the frontend. Ready for production the moment you deploy.",
                },
                {
                  tech: "Docker Compose",
                  role: "Dev Environment",
                  desc: "One command to spin up the entire stack locally. Database, API, and frontend orchestrated together with proper networking and volume mounts.",
                },
                {
                  tech: "AWS CDK (Infra tier)",
                  role: "Cloud",
                  desc: "Lambda, RDS, S3, CloudFront — all as TypeScript infrastructure code. Deploy to production with a single CDK deploy command.",
                },
              ].map((item) => (
                <div key={item.tech} className="bg-void p-6 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-sm font-bold text-white">{item.tech}</span>
                    <span className="font-mono text-[10px] tracking-widest text-slate-500 uppercase border border-slate-border px-2 py-0.5">{item.role}</span>
                  </div>
                  <p className="text-slate-400 text-sm leading-relaxed">{item.desc}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA */}
        <section className="py-20 px-4 text-center">
          <div className="max-w-lg mx-auto space-y-6">
            <h2 className="font-mono text-2xl font-bold text-white tracking-tight">
              Stop scaffolding. Start building.
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed">
              The architecture is the easy part now.
            </p>
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <Link href="/" className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-6 py-2.5 uppercase tracking-widest transition-colors">
                Start Building →
              </Link>
              <Link href="/pricing" className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors">
                See Pricing
              </Link>
              <Link href="/story" className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors">
                Our Story
              </Link>
            </div>
          </div>
        </section>

        {/* Mascot Bio */}
        <section className="border-t border-slate-surface py-16 px-4">
          <div className="max-w-4xl mx-auto">
            <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-8">
              Mascots
            </p>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-px bg-slate-border">
              <article className="bg-void p-6 space-y-3">
                <div className="flex items-center justify-between">
                  <h3 className="font-mono text-base font-bold text-white">Auri</h3>
                  <span className="font-mono text-[10px] tracking-widest text-electric border border-electric/30 bg-electric/5 px-2 py-0.5 uppercase">
                    Main Mascot
                  </span>
                </div>
                <p className="text-slate-400 text-sm leading-relaxed">
                  Auri is our resident alchemist: equal parts wise guide and chaotic builder energy.
                  He represents what StackAlchemist is built for — turning rough ideas into shippable systems,
                  without losing the magic of building.
                </p>
              </article>

              <article className="bg-void p-6 space-y-3">
                <div className="flex items-center justify-between">
                  <h3 className="font-mono text-base font-bold text-white">Reto</h3>
                  <span className="font-mono text-[10px] tracking-widest text-emerald border border-emerald/30 bg-emerald/5 px-2 py-0.5 uppercase">
                    Swiss Cheese Method
                  </span>
                </div>
                <p className="text-slate-400 text-sm leading-relaxed">
                  Reto is the specialist behind the Swiss Cheese Method. He keeps structure solid,
                  leaves room for intelligent variation, and reminds us that reliable software is equal
                  parts discipline and craft.
                </p>
              </article>
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
            <Link href="/story" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Story</Link>
            <Link href="/" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Build</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
