import { Shield, Zap, Code2, Database, Layers, Terminal } from "lucide-react";

const FEATURES = [
  {
    icon: Shield,
    title: "Compile Guarantee",
    description:
      "Every repository passes automated dotnet build verification before delivery. Three-attempt auto-correction loop. If it fails, you don't pay.",
  },
  {
    icon: Zap,
    title: "Real-Time Progress",
    description:
      "Watch your architecture materialize with live WebSocket updates. See each phase: Handlebars template injection, LLM generation, and build verification.",
  },
  {
    icon: Code2,
    title: "Dual Mode Intake",
    description:
      "Simple Mode: describe in plain English. Advanced Mode: visually define entities, relationships, and API endpoints with the entity wizard.",
  },
  {
    icon: Database,
    title: "Full Stack Output",
    description:
      "Receive a complete PostgreSQL schema, .NET 10 backend with Dapper ORM, and Next.js 15 frontend — all production-ready and pre-wired together.",
  },
  {
    icon: Layers,
    title: "IaC & Deployment Runbooks",
    description:
      "Tier 3 unlocks AWS CDK, Terraform scripts, Helm Charts for Kubernetes, and a step-by-step runbook you can hand to any engineer.",
  },
  {
    icon: Terminal,
    title: "Swiss Cheese Method",
    description:
      "Deterministic Handlebars templates carry the structure. LLM fills the holes with your domain logic. Predictable scaffolding, dynamic business code.",
  },
];

export function FeaturesSection() {
  return (
    <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
      <div className="mx-auto max-w-6xl">
        <div className="mb-14 text-center">
          <div className="mb-4 flex items-center justify-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">How it works</span>
            <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
          </div>
          <h2 className="text-3xl font-bold text-white lg:text-4xl">
            The Swiss Cheese Method
          </h2>
          <p className="mx-auto mt-4 max-w-2xl text-slate-400 leading-relaxed">
            Deterministic Handlebars templates carry the structure. Claude 3.5 Sonnet fills the holes with
            your business logic. Predictable scaffolding — dynamic domain code.
          </p>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {FEATURES.map((f) => {
            const Icon = f.icon;
            return (
              <div
                key={f.title}
                className="group rounded-xl border border-slate-600/30 bg-slate-700/20 p-6 transition-all duration-300 hover:border-blue-500/30 hover:bg-slate-700/40"
              >
                <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-lg bg-blue-500/10 text-blue-400 transition-colors group-hover:bg-blue-500/20">
                  <Icon className="h-6 w-6" />
                </div>
                <h3 className="mb-2 text-base font-semibold text-white">{f.title}</h3>
                <p className="text-sm leading-relaxed text-slate-400">{f.description}</p>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
