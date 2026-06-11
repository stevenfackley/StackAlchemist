import Link from "next/link";
import { Check } from "lucide-react";

const PRICING_TIERS = [
  {
    id: "blueprint",
    tier: "Tier 1",
    name: "Blueprint",
    tagline: "The Architecture",
    price: 299,
    description: "The full schema, API surface, and SQL scripts — delivered as precise technical documentation. Stack-agnostic.",
    features: [
      "Entity-Relationship Schema (JSON)",
      "API Specification (OpenAPI 3.0)",
      "SQL Migration Scripts",
      "Data Flow Diagram",
      "Architecture Decision Records",
    ],
    featured: false,
    href: "/advanced?step=4&tier=1",
  },
  {
    id: "boilerplate",
    tier: "Tier 2",
    name: "Boilerplate",
    tagline: "The Foundation",
    price: 599,
    description: "A complete, compiled, download-ready source repository. Guaranteed to build on first try — or we auto-correct up to three times.",
    features: [
      "Everything in Blueprint",
      ".NET 10 Web API (Controllers, Repos)",
      "Next.js 15 Frontend (App Router, TS)",
      "PostgreSQL Schema + Migrations",
      "Supabase Auth Integration",
      "Docker Compose Dev Environment",
      "Compile Guarantee (3-retry loop)",
    ],
    featured: true,
    href: "/advanced?step=4&tier=2",
  },
  {
    id: "infrastructure",
    tier: "Tier 3",
    name: "Infrastructure",
    tagline: "The Kingdom",
    price: 999,
    description: "Production-ready from day one. Cloud IaC, Kubernetes manifests, and a deployment runbook so complete a junior engineer could ship it.",
    features: [
      "Everything in Boilerplate",
      "AWS CDK Stack (Lambda, RDS, S3)",
      "Helm Charts for Kubernetes",
      "CI/CD Pipeline (GitHub Actions)",
      "Deployment Runbook (step-by-step)",
      "Environment Configuration Guide",
      "Cost Estimation Report",
    ],
    featured: false,
    href: "/advanced?step=4&tier=3",
  },
];

export function PricingSection() {
  return (
    <section id="pricing" className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
      <div className="mx-auto max-w-6xl">
        <div className="mb-14 text-center">
          <div className="mb-4 flex items-center justify-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Pricing</span>
            <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
          </div>
          <h2 className="text-3xl font-bold text-white lg:text-4xl">
            ONE PAYMENT.{" "}
            <span className="text-blue-400">OWN IT FOREVER.</span>
          </h2>
          <p className="mx-auto mt-4 max-w-xl text-slate-400 leading-relaxed">
            No subscriptions. No recurring fees. No seat licenses.
            Describe your SaaS, we transmute it — yours to keep, modify, and ship.
          </p>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {PRICING_TIERS.map((tier) => (
            <div
              key={tier.id}
              className={`relative rounded-xl border p-8 flex flex-col transition-all duration-300 ${
                tier.featured
                  ? "border-blue-500/50 bg-slate-700/50 shadow-[0_0_40px_rgba(59,130,246,0.12)]"
                  : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
              }`}
            >
              {tier.featured && (
                <>
                  <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-blue-500 to-transparent rounded-t-xl" />
                  <div className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-blue-500 px-3 py-1 text-xs font-medium text-white whitespace-nowrap">
                    Most Popular
                  </div>
                </>
              )}

              <div className="mb-1 font-mono text-xs tracking-wider text-slate-500 uppercase">{tier.tier}</div>
              <div className="mb-1 text-xs text-slate-500 font-mono">{tier.tagline}</div>
              <div className="mb-4 text-2xl font-bold text-white">{tier.name}</div>

              <div className="mb-4">
                <span className="text-4xl font-bold text-white">${tier.price}</span>
                <span className="ml-2 text-slate-500 text-sm">one-time</span>
              </div>

              <p className="text-sm text-slate-400 leading-relaxed mb-6">{tier.description}</p>

              <ul className="mb-8 space-y-2.5 flex-1">
                {tier.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2.5 text-sm text-slate-300">
                    <Check className="mt-0.5 h-4 w-4 shrink-0 text-blue-400" />
                    {feature}
                  </li>
                ))}
              </ul>

              <Link
                href={tier.href}
                className={`block text-center rounded-full py-3 text-sm font-medium transition-all duration-300 ${
                  tier.featured
                    ? "bg-blue-500 text-white hover:bg-blue-400 hover:shadow-[0_0_20px_rgba(59,130,246,0.35)]"
                    : "border border-slate-500/30 bg-slate-700/30 text-slate-200 hover:border-blue-500/40 hover:bg-slate-700/50"
                }`}
              >
                Get {tier.name} →
              </Link>
            </div>
          ))}
        </div>

        <p className="mt-8 text-center text-xs text-slate-500 font-mono">
          All prices are one-time charges in USD. No hidden fees. Secure checkout via Stripe.
        </p>
      </div>
    </section>
  );
}
