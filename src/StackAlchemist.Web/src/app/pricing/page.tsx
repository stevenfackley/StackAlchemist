import Link from "next/link";
import { Logo } from "@/components/logo";

const tiers = [
  {
    id: "blueprint",
    name: "Blueprint",
    tagline: "The Architecture",
    price: 299,
    description:
      "Everything you need to understand the system before a single line of code is written. The full schema, API surface, and SQL scripts — delivered as a precise technical document.",
    items: [
      "Entity-Relationship Schema (JSON)",
      "API Specification (OpenAPI 3.0)",
      "SQL Migration Scripts",
      "Data Flow Diagram",
      "Architecture Decision Records",
    ],
    highlight: false,
    cta: "Get the Blueprint",
    color: "electric",
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
    color: "electric",
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
    color: "electric",
  },
];

const faqs = [
  {
    q: "Is this a subscription?",
    a: "No. Every tier is a one-time payment. You pay once, you own the architecture forever. No monthly fees, no lock-in.",
  },
  {
    q: "What is the Compile Guarantee?",
    a: "Your generated Boilerplate or Infrastructure package is run through dotnet build before delivery. If it fails, an automatic correction loop re-runs the LLM with the compiler output and retries — up to three times. If it still can't compile, you get a full refund.",
  },
  {
    q: "What stack does V1 generate?",
    a: ".NET 10 Web API + Next.js 15 (App Router, TypeScript, Tailwind CSS) + PostgreSQL + Supabase. Additional stacks are planned for V2.",
  },
  {
    q: "Can I use the generated code commercially?",
    a: "Yes. The generated code is yours entirely. No attribution required, no licensing restrictions. Build your SaaS, sell it, scale it — it's your architecture.",
  },
  {
    q: "How long does generation take?",
    a: "Simple schemas generate in under 30 seconds. Complex multi-entity systems with many relationships typically take 60–90 seconds. You'll see real-time progress during generation.",
  },
  {
    q: "What if my idea doesn't fit the V1 stack?",
    a: "The Blueprint tier is stack-agnostic — schema, API specs, and SQL are transferable to any stack. Additional templates are on the roadmap.",
  },
];

export default function PricingPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      {/* Header */}
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <nav className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
          <Logo />
          <div className="flex items-center gap-6">
            <Link href="/about" className="text-xs font-mono tracking-widest text-slate-400 hover:text-electric transition-colors uppercase">About</Link>
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
        <section className="border-b border-slate-surface py-20 px-4 text-center">
          <div className="max-w-2xl mx-auto space-y-4">
            <p className="font-mono text-xs tracking-[0.3em] text-electric uppercase">
              Pricing
            </p>
            <h1 className="font-mono text-4xl md:text-5xl font-bold text-white tracking-tight">
              ONE PAYMENT.
              <br />
              <span className="text-electric">OWN IT FOREVER.</span>
            </h1>
            <p className="text-slate-400 text-sm leading-relaxed max-w-lg mx-auto">
              No subscriptions. No recurring fees. No seat licenses. You describe
              your SaaS, we transmute it into architecture — yours to keep,
              modify, and ship.
            </p>
          </div>
        </section>

        {/* Tiers */}
        <section className="py-20 px-4">
          <div className="max-w-6xl mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-0 border border-slate-border">
              {tiers.map((tier, i) => (
                <div
                  key={tier.id}
                  className={`relative p-8 space-y-6 flex flex-col ${
                    i < tiers.length - 1 ? "md:border-r border-b md:border-b-0 border-slate-border" : ""
                  } ${tier.highlight ? "bg-slate-surface/40" : "bg-void"}`}
                >
                  {tier.highlight && (
                    <div className="absolute top-0 left-0 right-0 h-px bg-electric" />
                  )}
                  {tier.highlight && (
                    <span className="absolute -top-3 left-1/2 -translate-x-1/2 bg-electric text-white font-mono text-[10px] tracking-widest uppercase px-3 py-0.5">
                      Most Popular
                    </span>
                  )}

                  <div>
                    <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-1">
                      {tier.tagline}
                    </p>
                    <h2 className="font-mono text-lg font-bold text-white uppercase tracking-widest">
                      {tier.name}
                    </h2>
                    <div className="mt-3 flex items-baseline gap-1">
                      <span className="font-mono text-4xl font-bold text-white">
                        ${tier.price}
                      </span>
                      <span className="font-mono text-xs text-slate-500">
                        one-time
                      </span>
                    </div>
                  </div>

                  <p className="text-slate-400 text-sm leading-relaxed">
                    {tier.description}
                  </p>

                  <ul className="space-y-2 flex-1">
                    {tier.items.map((item) => (
                      <li key={item} className="flex items-start gap-2">
                        <span className="text-electric font-mono text-xs mt-0.5 shrink-0">›</span>
                        <span className="font-mono text-xs text-slate-300">{item}</span>
                      </li>
                    ))}
                  </ul>

                  <Link
                    href="/advanced?step=3"
                    className={`block text-center font-mono text-xs tracking-widest uppercase py-2.5 px-4 transition-colors border ${
                      tier.highlight
                        ? "bg-electric border-electric text-white hover:bg-blue-600"
                        : "border-slate-border text-slate-300 hover:border-electric hover:text-electric"
                    }`}
                  >
                    {tier.cta} →
                  </Link>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Comparison table */}
        <section className="border-t border-slate-surface py-20 px-4">
          <div className="max-w-4xl mx-auto">
            <h2 className="font-mono text-xs tracking-[0.3em] text-electric uppercase mb-10 text-center">
              Full Comparison
            </h2>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse font-mono text-xs">
                <thead>
                  <tr className="border-b border-slate-border">
                    <th className="text-left py-3 pr-6 text-slate-500 font-normal tracking-widest uppercase w-1/2">
                      Feature
                    </th>
                    <th className="py-3 px-4 text-center text-slate-400 font-bold tracking-widest uppercase">Blueprint</th>
                    <th className="py-3 px-4 text-center text-electric font-bold tracking-widest uppercase">Boilerplate</th>
                    <th className="py-3 px-4 text-center text-slate-400 font-bold tracking-widest uppercase">Infra</th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    ["Schema JSON + ER Diagram", true, true, true],
                    ["OpenAPI Specification", true, true, true],
                    ["SQL Migration Scripts", true, true, true],
                    [".NET 10 Web API Source", false, true, true],
                    ["Next.js 15 Frontend Source", false, true, true],
                    ["Supabase Auth Integration", false, true, true],
                    ["Docker Compose Environment", false, true, true],
                    ["Compile Guarantee (3-retry)", false, true, true],
                    ["AWS CDK Infrastructure Stack", false, false, true],
                    ["Helm Charts (Kubernetes)", false, false, true],
                    ["GitHub Actions CI/CD", false, false, true],
                    ["Deployment Runbook", false, false, true],
                    ["Cost Estimation Report", false, false, true],
                  ].map(([label, bp, bb, infra]) => (
                    <tr key={label as string} className="border-b border-slate-surface/60 hover:bg-slate-surface/20 transition-colors">
                      <td className="py-3 pr-6 text-slate-300">{label as string}</td>
                      <td className="py-3 px-4 text-center">
                        {bp ? <span className="text-emerald">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                      <td className="py-3 px-4 text-center bg-slate-surface/20">
                        {bb ? <span className="text-emerald">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                      <td className="py-3 px-4 text-center">
                        {infra ? <span className="text-emerald">✓</span> : <span className="text-slate-700">—</span>}
                      </td>
                    </tr>
                  ))}
                  <tr className="border-t border-slate-border">
                    <td className="py-4 text-white font-bold">Price</td>
                    <td className="py-4 px-4 text-center text-white font-bold">$299</td>
                    <td className="py-4 px-4 text-center text-electric font-bold bg-slate-surface/20">$599</td>
                    <td className="py-4 px-4 text-center text-white font-bold">$999</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        {/* FAQ */}
        <section className="border-t border-slate-surface py-20 px-4">
          <div className="max-w-3xl mx-auto">
            <h2 className="font-mono text-xs tracking-[0.3em] text-electric uppercase mb-12 text-center">
              Frequently Asked
            </h2>
            <div className="space-y-0 divide-y divide-slate-surface">
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
        <section className="border-t border-slate-surface py-20 px-4 text-center">
          <div className="max-w-lg mx-auto space-y-6">
            <h2 className="font-mono text-2xl font-bold text-white tracking-tight">
              Ready to transmute your idea?
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed">
              Start with a prompt. StackAlchemist handles the architecture.
            </p>
            <div className="flex items-center justify-center gap-4">
              <Link
                href="/"
                className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-6 py-2.5 uppercase tracking-widest transition-colors"
              >
                Start Building →
              </Link>
              <Link
                href="/about"
                className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-6 py-2.5 uppercase tracking-widest transition-colors"
              >
                Learn More
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
            <Link href="/story" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Story</Link>
            <Link href="/" className="font-mono text-xs text-slate-600 hover:text-electric transition-colors uppercase tracking-widest">Build</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
