import type { Metadata } from "next";
import Link from "next/link";
import { Check, Eye, Lock } from "lucide-react";
import Image from "next/image";
import { ContentHeader } from "@/components/content-header";
import { pricingProductJsonLd, faqPageJsonLd } from "@/lib/jsonld";
import { SITE_URL } from "@/lib/constants";

export const metadata: Metadata = {
  title: "Pricing — One Payment, Own It Forever",
  description:
    "Compare tiers: Spark (free), Blueprint $299, Boilerplate $599, Infrastructure $999. One-time payment, no subscription, full source ownership.",
  alternates: { canonical: "/pricing" },
};

// Spark is a fixed demo, not a generated codebase: the engine renders the
// V0-Spark-NextJs template with your project name substituted and makes no LLM
// call at all (GenerationOrchestrator.RenderTier0Preview). Copy here describes
// exactly that — the run, not an imagined generation.
const freeTier = {
  id: "spark",
  name: "Spark",
  tagline: "Take the Workflow for a Lap",
  price: "Free",
  description:
    "Run the whole pipeline before you pay a cent: describe your product, watch the build, and land on a delivery page with a real Next.js 15 app already running in your browser. Spark hands everyone the same fixed demo app — a working task tracker, renamed to your project — so it is instant, costs nothing, and always boots.",
  items: [
    "5 free builds a month — no card, ever",
    "A real Next.js 15 app running in-browser (StackBlitz WebContainers)",
    "Every file open in the embedded editor — read it, edit it, re-run it",
    "Entity wizard with a live ER canvas (Advanced Mode)",
    "The same delivery flow the paid tiers use",
  ],
  highlight: false,
  cta: "Start Free",
  href: "/",
  isFree: true,
};

const tiers = [
  {
    id: "blueprint",
    name: "Blueprint",
    tagline: "The Architecture",
    price: 299,
    description:
      "Your data model and API contract, written down. No code — the documents you hand to a stakeholder, drop into an RFP, or give to the engineer who is going to build it.",
    items: [
      "schema.json — the normalized entity-relationship model",
      "api-docs.md — the CRUD contract, endpoint by endpoint",
      "Every field with its type, key, nullability and default",
      "The relationship map between entities",
      "Stack-agnostic — nothing in it assumes .NET or Next.js",
    ],
    highlight: false,
    cta: "Get the Blueprint",
    href: "/advanced?step=4&tier=1",
    isFree: false,
  },
  {
    id: "boilerplate",
    name: "Boilerplate",
    tagline: "The Foundation",
    price: 599,
    description:
      "A complete, download-ready source repository shaped around your schema. Both halves are put through their real compilers before it ships — or we correct and rebuild, up to three times.",
    items: [
      ".NET 10 minimal API — records, Dapper repositories, CRUD endpoints per entity",
      "Next.js 15 frontend (App Router, TypeScript) with a typed API client",
      "PostgreSQL migration — UUID keys, foreign keys, row-level security enabled",
      "Docker Compose + multi-stage Dockerfile (web and engine targets)",
      "Supabase client and env wiring preinstalled (auth flows are yours to write)",
      "Compile Guarantee — .NET and Next.js both built before delivery",
      "build-report.json — every command, exit code and verdict, in the archive",
    ],
    highlight: true,
    cta: "Get the Boilerplate",
    href: "/advanced?step=4&tier=2",
    isFree: false,
  },
  {
    id: "infrastructure",
    name: "Infrastructure",
    tagline: "The Kingdom",
    price: 999,
    description:
      "The Boilerplate repository plus the infrastructure to put it in a cloud. Two IaC paths, a Kubernetes chart, and a runbook complete enough to hand to a junior engineer.",
    items: [
      "Everything in Boilerplate",
      "AWS CDK stack (VPC, ECS Fargate, ALB, RDS PostgreSQL)",
      "Terraform AWS baseline (ECS, ALB, RDS, networking, logs)",
      "Helm chart — deployment, service, ingress, HPA, config and secrets",
      "DEPLOYMENT.md runbook — preflight, deploy, rollback",
    ],
    highlight: false,
    cta: "Get the Infrastructure",
    href: "/advanced?step=4&tier=3",
    isFree: false,
  },
];

const faqs = [
  {
    q: "What does the free Spark tier include?",
    a: "Spark runs the full workflow — describe your product, watch the build, land on a delivery page — and hands back a working Next.js 15 app that boots and runs inside your browser via StackBlitz WebContainers. You get five free builds a month, no card required.",
  },
  {
    q: "Is the Spark app generated from my description?",
    a: "No, and we would rather say so than let you find out. Spark renders one fixed demo app — a small task tracker — with your project name substituted in. It makes no AI call, which is why it is free and always boots. Code generated from your own schema starts at Blueprint.",
  },
  {
    q: "Then what is Spark actually for?",
    a: "Seeing the machine run before you pay for it. You confirm the flow works in your browser, see exactly what the delivery page looks like, read a real Next.js 15 App Router project file by file, and — in Advanced Mode — model your entities on the ER canvas and keep that schema for a paid run later.",
  },
  {
    q: "Is this a subscription?",
    a: "No. Every paid tier is a one-time payment. You pay once, you own the architecture forever. No monthly fees, no lock-in.",
  },
  {
    q: "What is the Compile Guarantee?",
    a: "Before a Boilerplate or Infrastructure archive is packed, both halves are put through their real toolchains: dotnet restore and dotnet build for the API, npm ci, tsc --noEmit and next build for the frontend. If either fails, the compiler output goes back to the LLM and the failing files are regenerated — up to three times, after which the charge is refunded automatically. The archive ships a build-report.json recording every command and its verdict.",
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
    a: "The Blueprint tier is stack-agnostic — the schema and the CRUD contract transfer to any stack you like. Additional templates are on the roadmap.",
  },
];

// Every row here is a claim about what the engine actually emits. Sources:
// Spark = V0-Spark-NextJs (fixed template, no LLM call); Blueprint =
// Tier1ArtifactBuilder (schema.json + api-docs.md, and nothing else);
// Boilerplate = the V1-DotNet-NextJs tree filled by the generation prompt;
// Infrastructure = that tree plus Tier3-Infrastructure. Do not add a row you
// cannot point at a rendered file for.
const comparison = [
  { label: "Live demo app running in-browser",        spark: true,  bp: false, bb: false, infra: false },
  { label: "Entity wizard + live ER canvas",          spark: true,  bp: true,  bb: true,  infra: true },
  { label: "Built from your own schema",              spark: false, bp: true,  bb: true,  infra: true },
  { label: "Downloadable archive",                    spark: false, bp: true,  bb: true,  infra: true },
  { label: "Schema + API contract documents",         spark: false, bp: true,  bb: false, infra: false },
  { label: ".NET 10 minimal API source",              spark: false, bp: false, bb: true,  infra: true },
  { label: "Next.js 15 frontend + typed API client",  spark: false, bp: false, bb: true,  infra: true },
  { label: "PostgreSQL migration (UUID keys, RLS)",   spark: false, bp: false, bb: true,  infra: true },
  { label: "Docker Compose + Dockerfile",             spark: false, bp: false, bb: true,  infra: true },
  { label: "Compile Guarantee (both halves built)",   spark: false, bp: false, bb: true,  infra: true },
  { label: "build-report.json in the archive",        spark: false, bp: false, bb: true,  infra: true },
  { label: "AWS CDK stack",                           spark: false, bp: false, bb: false, infra: true },
  { label: "Terraform AWS baseline",                  spark: false, bp: false, bb: false, infra: true },
  { label: "Helm chart (Kubernetes)",                 spark: false, bp: false, bb: false, infra: true },
  { label: "Deployment runbook",                      spark: false, bp: false, bb: false, infra: true },
];


function CheckCell({ value }: { value: boolean }) {
  return value ? (
    <span className="text-emerald-400">✓</span>
  ) : (
    <span className="text-slate-700">—</span>
  );
}

export default function PricingPage() {
  const productLdJson = JSON.stringify(
    pricingProductJsonLd(SITE_URL, tiers.map((t) => ({ name: t.name, price: t.price, href: t.href }))),
  );
  // FAQPage schema on pricing — the highest-intent page deserves rich results
  // for free-tier / subscription / commercial-use questions. Distinct from the
  // /faq page's 17-entry FAQPage; both are valid because the questions differ.
  const faqLdJson = JSON.stringify(
    faqPageJsonLd(faqs.map((f) => ({ question: f.q, answer: f.a }))),
  );
  return (
    <div data-testid="pricing-page" className="min-h-screen flex flex-col bg-slate-800">
      <script
        key="ld-pricing-product"
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: productLdJson }}
      />
      <script
        key="ld-pricing-faq"
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: faqLdJson }}
      />
      {/* Background */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none select-none" aria-hidden>
        <div
          className="absolute top-1/4 -right-1/4 h-[300px] w-[300px] md:h-[600px] md:w-[600px] lg:h-[700px] lg:w-[700px] rounded-full animate-pulse-glow"
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

      {/* Shared content-surface header — same nav as /blog, /compare, /solutions, /faq */}
      <ContentHeader />

      <main className="relative z-10 flex-1">
        {/* Hero */}
        <section className="py-20 px-4 sm:px-8 text-center border-b border-slate-700/50">
          <div className="max-w-2xl mx-auto space-y-5">
            <div className="flex items-center justify-center gap-3">
              <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
              <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Pricing</span>
              <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
            </div>
            <h1 data-testid="pricing-hero-title" className="font-bold text-4xl md:text-5xl text-white tracking-tight leading-tight">
              TRY FREE.
              <br />
              <span className="text-accent">OWN IT FOREVER.</span>
            </h1>
            <p className="text-slate-400 text-sm leading-relaxed max-w-lg mx-auto">
              Run the workflow free — a demo app boots in your browser, no credit card.
              Pay once when you want the codebase generated from your own schema, downloaded,
              and yours to keep, modify, and ship.
            </p>
          </div>
        </section>

        {/* Free Tier Banner */}
        <section className="py-12 px-4 sm:px-8 border-b border-slate-700/50">
          <div className="max-w-6xl mx-auto">
            <div data-testid="pricing-tier-spark" className="relative rounded-2xl border border-emerald-500/30 bg-gradient-to-br from-emerald-500/5 via-slate-700/20 to-slate-700/10 p-8 overflow-hidden">
              {/* Glow */}
              <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-emerald-500/60 to-transparent" />
              <div
                className="absolute -top-20 -left-20 h-64 w-64 rounded-full pointer-events-none"
                style={{ backgroundImage: "radial-gradient(circle, rgba(16,185,129,0.08) 0%, transparent 70%)" }}
              />

              <div className="relative flex flex-col lg:flex-row items-start lg:items-center justify-between gap-8">
                {/* Left: info */}
                <div className="flex-1 space-y-4">
                  <div className="flex items-center gap-3">
                    <div className="rounded-full bg-emerald-500/10 border border-emerald-500/30 px-3 py-1 flex items-center gap-1.5">
                      <Eye className="h-3 w-3 text-emerald-400" />
                      <span className="font-mono text-[10px] tracking-[0.3em] text-emerald-400 uppercase">Free • No Card Required</span>
                    </div>
                  </div>
                  <div>
                    <p className="font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase mb-1">Spark</p>
                    <h2 className="text-3xl font-bold text-white">
                      Free Demo Run
                      <span className="ml-3 font-mono text-lg text-emerald-400">$0</span>
                    </h2>
                  </div>
                  <p className="text-slate-400 text-sm leading-relaxed max-w-xl">
                    {freeTier.description}
                  </p>
                  <ul className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                    {freeTier.items.map((item) => (
                      <li key={item} className="flex items-start gap-2 text-sm text-slate-300">
                        <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
                        {item}
                      </li>
                    ))}
                  </ul>
                  <div className="flex items-start gap-2 pt-1">
                    <Lock className="mt-0.5 h-3.5 w-3.5 text-slate-500 shrink-0" />
                    <span className="font-mono text-xs text-slate-500 leading-relaxed">
                      The demo app is the same for everyone and is not generated from your
                      description. There is no download and no .NET half at this tier — code
                      built from your schema starts at Blueprint.
                    </span>
                  </div>
                </div>

                {/* Right: CTA */}
                <div className="shrink-0 flex flex-col items-center gap-4 text-center">
                  <div>
                    <p className="font-mono text-4xl font-bold text-white">$0</p>
                    <p className="font-mono text-xs text-slate-500 mt-1">forever free</p>
                  </div>
                  <Link
                    href="/"
                    className="rounded-full bg-emerald-500 text-white px-8 py-3 text-sm font-medium hover:bg-emerald-400 hover:shadow-[0_0_24px_rgba(16,185,129,0.35)] transition-all duration-300 whitespace-nowrap"
                  >
                    Start Free &rarr;
                  </Link>
                  <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">5 builds a month &middot; no card</p>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Paid Tier Cards */}
        <section className="py-20 px-4 sm:px-8">
          <div className="max-w-6xl mx-auto">
            <div className="mb-10 text-center">
              <div className="flex items-center justify-center gap-3">
                <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
                <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Paid Tiers</span>
                <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
              </div>
              <p className="mt-3 text-slate-500 text-sm font-mono">One-time payment. Download and own your codebase forever.</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {tiers.map((tier) => (
                <div
                  key={tier.id}
                  data-testid={`pricing-tier-${tier.id}`}
                  className={`relative rounded-xl border p-8 flex flex-col transition-all duration-300 ${
                    tier.highlight
                      ? "border-accent/50 bg-slate-700/50 shadow-[0_0_40px_rgba(77,166,255,0.12)]"
                      : "border-slate-600/30 bg-slate-700/20 hover:border-slate-500/50"
                  }`}
                >
                  {tier.highlight && (
                    <>
                      <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-accent to-transparent rounded-t-xl" />
                      <div className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-accent px-3 py-1 text-xs font-medium text-white whitespace-nowrap">
                        Most Popular
                      </div>
                    </>
                  )}
                  <div className="mb-1 font-mono text-[10px] tracking-[0.3em] text-slate-500 uppercase">{tier.tagline}</div>
                  <div className="mb-4 text-2xl font-bold text-white">{tier.name}</div>
                  <div className="mb-5">
                    <span data-testid={`pricing-price-${tier.id}`} className="text-4xl font-bold text-white">${tier.price}</span>
                    <span className="ml-2 text-slate-500 text-sm font-mono">one-time</span>
                  </div>
                  <p className="text-slate-400 text-sm leading-relaxed mb-6">{tier.description}</p>
                  <ul className="space-y-2.5 flex-1 mb-8">
                    {tier.items.map((item) => (
                      <li key={item} className="flex items-start gap-2.5 text-sm text-slate-300">
                        <Check className="mt-0.5 h-4 w-4 shrink-0 text-accent" />
                        {item}
                      </li>
                    ))}
                  </ul>
                  <Link
                    href={tier.href}
                    className={`block text-center rounded-full py-3 text-sm font-medium transition-all duration-300 ${
                      tier.highlight
                        ? "bg-accent text-white hover:bg-accent/90 hover:shadow-[0_0_20px_rgba(77,166,255,0.35)]"
                        : "border border-slate-500/30 bg-slate-700/30 text-slate-200 hover:border-accent/40 hover:bg-slate-700/50"
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
          <div className="max-w-5xl mx-auto">
            <div className="mb-10 text-center">
              <div className="flex items-center justify-center gap-3">
                <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
                <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Full Comparison</span>
                <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
              </div>
            </div>
            <div className="overflow-x-auto rounded-xl border border-slate-700/50">
              <table className="w-full font-mono text-xs">
                <thead>
                  <tr className="border-b border-slate-700/50 bg-slate-700/20">
                    <th className="text-left py-4 px-6 text-slate-500 font-normal tracking-widest uppercase w-2/5">Feature</th>
                    <th className="py-4 px-3 text-center text-emerald-400 font-bold tracking-widest uppercase bg-emerald-500/5 whitespace-nowrap">Spark<br/><span className="text-[10px] font-normal text-slate-500">Free</span></th>
                    <th className="py-4 px-3 text-center text-slate-400 font-bold tracking-widest uppercase">Blueprint</th>
                    <th className="py-4 px-3 text-center text-accent font-bold tracking-widest uppercase bg-accent/5">Boilerplate</th>
                    <th className="py-4 px-3 text-center text-slate-400 font-bold tracking-widest uppercase">Infra</th>
                  </tr>
                </thead>
                <tbody>
                  {comparison.map((row) => (
                    <tr key={row.label} className="border-b border-slate-700/30 hover:bg-slate-700/20 transition-colors">
                      <td className="py-3 px-6 text-slate-300">{row.label}</td>
                      <td className="py-3 px-3 text-center bg-emerald-500/5"><CheckCell value={row.spark} /></td>
                      <td className="py-3 px-3 text-center"><CheckCell value={row.bp} /></td>
                      <td className="py-3 px-3 text-center bg-accent/5"><CheckCell value={row.bb} /></td>
                      <td className="py-3 px-3 text-center"><CheckCell value={row.infra} /></td>
                    </tr>
                  ))}
                  <tr className="border-t-2 border-slate-600/50 bg-slate-700/10">
                    <td className="py-4 px-6 text-white font-bold">Price</td>
                    <td className="py-4 px-3 text-center text-emerald-400 font-bold bg-emerald-500/5">Free</td>
                    <td className="py-4 px-3 text-center text-white font-bold">$299</td>
                    <td className="py-4 px-3 text-center text-accent font-bold bg-accent/5">$599</td>
                    <td className="py-4 px-3 text-center text-white font-bold">$999</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p className="mt-4 text-center text-xs text-slate-500 leading-relaxed max-w-2xl mx-auto">
              Boilerplate and Infrastructure ship the code instead of the Blueprint documents:
              your schema arrives as the SQL migration, the C# records, and the TypeScript
              types rather than as <span className="font-mono">schema.json</span>. Spark&apos;s
              in-browser app is a fixed demo, not a build of your schema.
            </p>
          </div>
        </section>

        {/* FAQ */}
        <section className="border-t border-slate-700/50 py-20 px-4 sm:px-8">
          <div className="max-w-3xl mx-auto">
            <div className="mb-12 text-center">
              <div className="flex items-center justify-center gap-3">
                <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
                <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Frequently Asked</span>
                <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
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
              Watch the machine run first — Spark is free, instant, and needs no card. Pay when
              you want it pointed at your own schema.
            </p>
            <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
              <Link
                href="/"
                className="w-full sm:w-auto rounded-full bg-emerald-500 text-white px-6 py-2.5 text-sm font-medium hover:bg-emerald-400 hover:shadow-[0_0_20px_rgba(16,185,129,0.35)] transition-all duration-300 text-center"
              >
                Try Spark Free &rarr;
              </Link>
              <Link
                href="/about"
                className="w-full sm:w-auto rounded-full border border-slate-500/30 bg-slate-700/30 text-slate-300 hover:border-accent/40 hover:text-accent px-6 py-2.5 text-sm font-medium transition-all duration-300 text-center"
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
              STACK <span className="text-accent/60">AL</span>CHEMIST
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
