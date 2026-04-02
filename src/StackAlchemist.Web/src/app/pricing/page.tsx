import Link from "next/link";
import { Check } from "lucide-react";
import Image from "next/image";

const tiers = [
  {
    id: "blueprint",
    name: "Blueprint",
    tagline: "The Architecture",
    price: 299,
    description:
      "Everything you need to understand the system before a single line of code is written. The full schema, API surface, and SQL scripts — delivered as precise technical documentation.",
    items: [
      "Entity-Relationship Schema (JSON)",
      "API Specification (OpenAPI 3.0)",
      "SQL Migration Scripts",
      "Data Flow Diagram",
      "Architecture Decision Records",
    ],
    highlight: false,
    cta: "Get the Blueprint",
    href: "/advanced?step=3&tier=1",
  },
  {
    id: "boilerplate",
    name: "Boilerplate",
    tagline: "The Foundation",
    price: 599,
    description:
      "A complete, compiled, download-ready source repository. Every file. Every layer. Guaranteed to build on the first try — or we correct it automatically, up to three times.",
    items: [
      "Everything in Blueprint",
      ".NET 10 Web API (Controllers, Repos, Models)",
      "Next.js 15 Frontend (App Router, TypeScript)",
      "PostgreSQL Schema + Migrations",
      "Supabase Auth Integration",
      "Docker Compose Dev Environment",
      "Compile Guarantee (3-retry auto-correction)",
    ],
    highlight: true,
    cta: "Get the Boilerplate",
    href: "/advanced?step=3&tier=2",
  },
  {
    id: "infrastructure",
    name: "Infrastructure",
    tagline: "The Kingdom",
    price: 999,
    description:
      "Production-ready from day one. Cloud infrastructure as code, Kubernetes manifests, and a deployment runbook so complete you could hand it to a junior engineer and disappear.",
    items: [
      "Everything in Boilerplate",
      "AWS CDK Stack (Lambda, RDS, S3, CloudFront)",
      "Helm Charts for Kubernetes",
      "CI/CD Pipeline (GitHub Actions)",
      "Deployment Runbook (step-by-step)",
      "Environment Configuration Guide",
      "Cost Estimation Report",
    ],
    highlight: false,
    cta: "Get the Infrastructure",
    href: "/advanced?step=3&tier=3",
  },
];

const faqs = [
  {
    q: "Is this a subscription?",
    a: "No. Every tier is a one-time payment. You pay once, you own the architecture forever. No monthly fees, no lock-in.",
  },
  {
    q: "What is the Compile Guarantee?",
    a: "Your generated Boilerplate or Infrastructure package is run through dotnet build before delivery. If it fails, an automatic correction loop re-runs the LLM with the compiler output and retries — up to three times. If it still fails, you get a full refund.",
  },
  {
    q: "What stack does V1 generate?",
    a: ".NET 10 Web API + Next.js 15 (App Router, TypeScript, Tailwind CSS) + PostgreSQL + Supabase. Additional stacks are planned for V2.",
  },
  {
    q: "Can I use the generated code commercially?",
    a: "Yes. The generated code is yours entirely. No attribution required, no licensing restrictions. Build your SaaS, sell it, scale it.",
  },
  {
    q: "How long does generation take?",
    a: "Simple schemas generate in under 30 seconds. Complex multi-entity systems typically take 60–90 seconds. You see real-time progress throughout.",
  },
  {
    q: "What if my idea doesn't fit the V1 stack?",
    a: "The Blueprint tier is stack-agnostic — schema, API specs, and SQL are transferable to any stack. Additional templates are on the roadmap.",
  },
];

const comparison = [
  { label: "Schema JSON + ER Diagram", bp: true, bb: true, infra: true },
  { label: "OpenAPI Specification", bp: true, bb: true, infra: true },
  { label: "SQL Migration Scripts", bp: true, bb: true, infra: true },
  { label: ".NET 10 Web API Source", bp: false, bb: true, infra: true },
  { label: "Next.js 15 Frontend Source", bp: false, bb: true, infra: true },
  { label: "Supabase Auth Integration", bp: false, bb: true, infra: true },
  { label: "Docker Compose Environment", bp: false, bb: true, infra: true },
  { label: "Compile Guarantee (3-retry)", bp: false, bb: true, infra: true },
  { label: "AWS CDK Infrastructure Stack", bp: false, bb: false, infra: true },
  { label: "Helm Charts (Kubernetes)", bp: false, bb: false, infra: true },
  { label: "GitHub Actions CI/CD", bp: false, bb: false, infra: true },
  { label: "Deployment Runbook", bp: false, bb: false, infra: true },
  { label: "Cost Estimation Report", bp: false, bb: false, infra: true },
];

export default function PricingPage() {
  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      {/* Background */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none select-none" aria-hidden>
        <div
          className="absolute top-1/4 -right-1/4 h-[700px] w-[700px] rounded-full animate-pulse-glow"
          style={{
            backgroundImage:
              "radial-gradient(circle, rgba(59, 130, 246, 0.12) 0%, rgba(59, 130, 246, 0.04) 45%, transparent 70%)",
          }}
        />
        <div
          className="absolute inset-0 opacity-[0.02]"
          style={{
            backgroundImage:
              "linear-gradient(rgba(255,255,255,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.3) 1px, transparent 1px)",
            backgroundSize: "80px 80px",
          }}
        />
      </div>

      {/* Header */}
      <header className="relative z-50 border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0">
        <nav className="max-w-6xl mx-auto px-4 sm:px-8 h-14 flex items-center justify-between">
          <Link href="/" className="flex items-center gap-3 transition-opacity hover:opacity-80">
            <Image
              src="/logo.svg"
              alt="Stack Alchemist"
              width={32}
              height={32}
              className="drop-shadow-[0_0_8px_rgba(59,130,246,0.4)]"
            />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-blue-400">AL</span>CHEMIST
            </span>
          </Link>
          <div className="flex items-center gap-4 sm:gap-6">
            <Link href="/about" className="text-xs font-mono tracking-widest text-slate-400 hover:text-white transition-colors uppercase hidden sm:block">About</Link>
            <Link href="/story" className="text-xs font-mono tracking-widest text-slate-400 hover:text-white transition-colors uppercase hidden sm:block">Story</Link>
            <Link href="/" className="text-xs font-mono tracking-widest text-slate-400 hover:text-white transition-colors uppercase">Build</Link>
            <Link
              href="/login"
              className="rounded-full border border-slate-500/30 bg-slate-700/50 text-xs font-mono tracking-widest text-slate-300 hover:border-blue-500/40 hover:text-blue-400 transition-all px-3 py-1.5 uppercase"
            >
              Login
            </Link>
          </div>
        </nav>
      </header>

      <main className="relative z-10 flex-1">
        {/* Hero */}
        <section className="py-20 px-4 sm:px-8 text-center border-b border-slate-700/50">
          <div className="max-w-2xl mx-auto space-y-5">
            <div className="flex items-center justify-center gap-3">
              <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
              <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Pricing</span>
              <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
            </div>
            <h1 className="font-bold text-4xl md:text-5xl text-white tracking-tight leading-tight">
              ONE PAYMENT.
              <br />
              <span className="text-blue-400">OWN IT FOREVER.</span>
            </h1>
            <p className="text-slate-400 text-sm leading-relaxed max-w-lg mx-auto">
              No subscriptions. No recurring fees. No seat licenses. You describe your SaaS,
              we transmute it into architecture — yours to keep, modify, and ship.
            </p>
          </div>
        </section>

        {/* Tier Cards */}
        <section className="py-20 px-4 sm:px-8">
          <div className="max-w-6xl mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {tiers.map((tier) => (
                <div
                  key={tier.id}
                  className={`relative rounded-xl border p-8 flex flex-col transition-all duration-300 ${
                    tier.highlight
                      ? "border-blue-500/50 bg-slate-700/50 shadow-[0_0_40px_rgba(59,130,246,0.12)]"
                      : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                  }`}
                >
                  {tier.highlight && (
                    <>
                      <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-blue-500 to-transparent rounded-t-xl" />
                      <div className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-blue-500 px-3 py-1 text-xs font-medium text-white whitespace-nowrap">
                        Most Popular
                      </div>
                    </>
                  )}
                  <div className="mb-1 font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">{tier.tagline}</div>
                  <div className="mb-4 text-2xl font-bold text-white">{tier.name}</div>
                  <div className="mb-5">
                    <span className="text-4xl font-bold text-white">${tier.price}</span>
                    <span className="ml-2 text-slate-500 text-sm font-mono">one-time</span>
                  </div>
                  <p className="text-slate-400 text-sm leading-relaxed mb-6">{tier.description}</p>
                  <ul className="space-y-2.5 flex-1 mb-8">
                    {tier.items.map((item) => (
                      <li key={item} className="flex items-start gap-2.5 text-sm text-slate-300">
                        <Check className="mt-0.5 h-4 w-4 shrink-0 text-blue-400" />
                        {item}
                      </li>
                    ))}
                  </ul>
                  <Link
                    href={tier.href}
                    className={`block text-center rounded-full py-3 text-sm font-medium transition-all duration-300 ${
                      tier.highlight
                        ? "bg-blue-500 text-white hover:bg-blue-400 hover:shadow-[0_0_20px_rgba(59,130,246,0.35)]"
                        : "border border-slate-500/30 bg-slate-700/30 text-slate-200 hover:border-blue-500/40 hover:bg-slate-700/50"
                    }`}
                  >
                    {tier.cta} &rarr;
                  </Link>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Comparison Table */}
        <section className="border-t border-slate-700/50 py-20 px-4 sm:px-8">
          <div className="max-w-4xl mx-auto">
            <div className="mb-10 text-center">
              <div className="flex items-center justify-center gap-3">
                <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
                <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Full Comparison</span>
                <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
              </div>
            </div>
            <div className="overflow-x-auto rounded-xl border border-slate-700/50">
              <table className="w-full font-mono text-xs">
                <thead>
                  <tr className="border-b border-slate-700/50 bg-slate-700/20">
                    <th className="text-left py-4 px-6 text-slate-500 font-normal tracking-widest uppercase w-1/2">Feature</th>
                    <th className="py-4 px-4 text-center text-slate-400 font-bold tracking-widest uppercase">Blueprint</th>
                    <th className="py-4 px-4 text-center text-blue-400 font-bold tracking-widest uppercase bg-blue-500/5">Boilerplate</th>
                    <th className="py-4 px-4 text-center text-slate-400 font-bold tracking-widest uppercase">Infra</th>
                  </tr>
                </thead>
                <tbody>
                  {comparison.map((row) => (
                    <tr key={row.label} className="border-b border-slate-700/30 hover:bg-slate-700/20 transition-colors">
                      <td className="py-3 px-6 text-slate-300">{row.label}</td>
                      <td className="py-3 px-4 text-center">
                        {row.bp ? <span className="text-emerald-400">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                      <td className="py-3 px-4 text-center bg-blue-500/5">
                        {row.bb ? <span className="text-emerald-400">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                      <td className="py-3 px-4 text-center">
                        {row.infra ? <span className="text-emerald-400">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                    </tr>
                  ))}
                  <tr className="border-t-2 border-slate-600/50 bg-slate-700/10">
                    <td className="py-4 px-6 text-white font-bold">Price</td>
                    <td className="py-4 px-4 text-center text-white font-bold">$299</td>
                    <td className="py-4 px-4 text-center text-blue-400 font-bold bg-blue-500/5">$599</td>
                    <td className="py-4 px-4 text-center text-white font-bold">$999</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        {/* FAQ */}
        <section className="border-t border-slate-700/50 py-20 px-4 sm:px-8">
          <div className="max-w-3xl mx-auto">
            <div className="mb-12 text-center">
              <div className="flex items-center justify-center gap-3">
                <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
                <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Frequently Asked</span>
                <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
              </div>
            </div>
            <div className="divide-y divide-slate-700/50">
              {faqs.map((faq) => (
                <div key={faq.q} className="py-6 grid grid-cols-1 md:grid-cols-5 gap-4">
                  <div className="md:col-span-2">
                    <p className="font-mono text-xs text-white font-bold leading-relaxed">{faq.q}</p>
                  </div>
                  <div className="md:col-span-3">
                    <p className="text-slate-400 text-sm leading-relaxed">{faq.a}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA */}
        <section className="border-t border-slate-700/50 py-20 px-4 sm:px-8 text-center">
          <div className="max-w-lg mx-auto space-y-6">
            <h2 className="text-2xl font-bold text-white tracking-tight">
              Ready to transmute your idea?
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed">
              Start with a prompt. StackAlchemist handles the architecture.
            </p>
            <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
              <Link
                href="/"
                className="w-full sm:w-auto rounded-full bg-blue-500 text-white px-6 py-2.5 text-sm font-medium hover:bg-blue-400 hover:shadow-[0_0_20px_rgba(59,130,246,0.35)] transition-all duration-300 text-center"
              >
                Start Building &rarr;
              </Link>
              <Link
                href="/about"
                className="w-full sm:w-auto rounded-full border border-slate-500/30 bg-slate-700/30 text-slate-300 hover:border-blue-500/40 hover:text-blue-400 px-6 py-2.5 text-sm font-medium transition-all duration-300 text-center"
              >
                Learn More
              </Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="relative z-10 border-t border-slate-700/50 py-8 px-4 sm:px-8">
        <div className="max-w-6xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <Image src="/logo.svg" alt="Stack Alchemist" width={24} height={24} className="opacity-50" />
            <span className="font-mono text-xs tracking-widest text-slate-500">
              STACK <span className="text-blue-400/60">AL</span>CHEMIST
            </span>
          </div>
          <div className="flex gap-6">
            <Link href="/about" className="font-mono text-xs text-slate-600 hover:text-slate-300 transition-colors uppercase tracking-widest">About</Link>
            <Link href="/story" className="font-mono text-xs text-slate-600 hover:text-slate-300 transition-colors uppercase tracking-widest">Story</Link>
            <Link href="/" className="font-mono text-xs text-slate-600 hover:text-slate-300 transition-colors uppercase tracking-widest">Build</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
