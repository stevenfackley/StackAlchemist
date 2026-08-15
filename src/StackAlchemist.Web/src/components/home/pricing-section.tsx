import Link from "next/link";
import { Check } from "lucide-react";

// Keep these lists identical in substance to /pricing — both are claims about
// what the engine emits, and the two pages disagreeing is how a false one
// survives. See the sourcing note above `comparison` in app/pricing/page.tsx.
const PRICING_TIERS = [
  {
    id: "blueprint",
    tier: "Tier 1",
    name: "Blueprint",
    tagline: "The Architecture",
    price: 299,
    description: "Your data model and API contract, written down. No code — the documents you hand to a stakeholder or the engineer who will build it. Stack-agnostic.",
    features: [
      "schema.json — normalized entity-relationship model",
      "api-docs.md — the CRUD contract per entity",
      "Types, keys, nullability and defaults per field",
      "The relationship map between entities",
      "Transfers to any stack you like",
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
    description: "A download-ready source repository shaped around your schema. Both halves go through their real compilers before it ships — or we correct and rebuild, up to three times.",
    features: [
      ".NET 10 minimal API (records, Dapper repos, endpoints)",
      "Next.js 15 frontend (App Router, TS) + typed client",
      "PostgreSQL migration — UUID keys, FKs, RLS enabled",
      "Docker Compose + multi-stage Dockerfile",
      "Compile Guarantee — .NET and Next.js both built",
      "build-report.json with every command and verdict",
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
    description: "The Boilerplate repository plus the infrastructure to put it in a cloud. Two IaC paths, a Kubernetes chart, and a runbook a junior engineer could ship from.",
    features: [
      "Everything in Boilerplate",
      "AWS CDK stack (VPC, ECS Fargate, ALB, RDS)",
      "Terraform AWS baseline",
      "Helm chart (deployment, service, ingress, HPA)",
      "DEPLOYMENT.md runbook — preflight, deploy, rollback",
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
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Pricing</span>
            <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
          </div>
          <h2 className="text-3xl font-bold text-white lg:text-4xl">
            ONE PAYMENT.{" "}
            <span className="text-accent">OWN IT FOREVER.</span>
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
                  ? "border-accent/50 bg-slate-700/50 shadow-[0_0_40px_rgba(59,130,246,0.12)]"
                  : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
              }`}
            >
              {tier.featured && (
                <>
                  <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-accent to-transparent rounded-t-xl" />
                  <div className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-accent px-3 py-1 text-xs font-medium text-white whitespace-nowrap">
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
                    <Check className="mt-0.5 h-4 w-4 shrink-0 text-accent" />
                    {feature}
                  </li>
                ))}
              </ul>

              <Link
                href={tier.href}
                className={`block text-center rounded-full py-3 text-sm font-medium transition-all duration-300 ${
                  tier.featured
                    ? "bg-accent text-white hover:bg-accent/90 hover:shadow-[0_0_20px_rgba(59,130,246,0.35)]"
                    : "border border-slate-500/30 bg-slate-700/30 text-slate-200 hover:border-accent/40 hover:bg-slate-700/50"
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
