"use client"

import { useState, useEffect } from "react"
import Link from "next/link"
import { AlchemyInput } from "./alchemy-input"
import { Check, Zap, Shield, Code2, Database, Layers, Terminal } from "lucide-react"

export function Hero() {
  const [prompt, setPrompt] = useState("")
  const [isMac, setIsMac] = useState(true)
  const [mode, setMode] = useState<"simple" | "advanced">("simple")

  useEffect(() => {
    setIsMac(navigator.platform.toUpperCase().indexOf("MAC") >= 0)
  }, [])

  return (
    <div className="min-h-screen bg-slate-800">
      {/* Background effects */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none">
        <div 
          className="absolute top-1/4 -right-1/4 h-[900px] w-[900px] animate-pulse-glow rounded-full"
          style={{
            backgroundImage: "radial-gradient(circle, rgba(59, 130, 246, 0.15) 0%, rgba(59, 130, 246, 0.05) 45%, transparent 70%)",
            backgroundSize: "100% 100%",
            backgroundRepeat: "no-repeat",
          }}
        />
        <div 
          className="absolute -bottom-1/4 -left-1/4 h-[700px] w-[700px] animate-pulse-glow rounded-full"
          style={{
            backgroundImage: "radial-gradient(circle, rgba(99, 102, 241, 0.1) 0%, transparent 55%)",
            backgroundSize: "100% 100%",
            backgroundRepeat: "no-repeat",
            animationDelay: "2s",
          }}
        />
        <div 
          className="absolute -top-1/3 left-1/3 h-[500px] w-[700px] rounded-full opacity-50"
          style={{
            backgroundImage: "radial-gradient(ellipse, rgba(148, 163, 184, 0.08) 0%, transparent 60%)",
            backgroundSize: "100% 100%",
            backgroundRepeat: "no-repeat",
          }}
        />
        <div 
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage: "linear-gradient(rgba(255,255,255,0.2) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.2) 1px, transparent 1px)",
            backgroundSize: "80px 80px",
            backgroundPosition: "0 0",
          }}
        />
      </div>

      {/* Navigation */}
      <nav className="relative z-10 flex items-center justify-between px-8 py-6 lg:px-16">
        <Link href="/" className="flex items-center gap-3 transition-opacity hover:opacity-80">
          <img 
            src="/logo.svg" 
            alt="Stack Alchemist" 
            className="h-9 w-9 drop-shadow-[0_0_8px_rgba(59,130,246,0.4)]"
          />
          <span className="font-mono text-sm font-medium tracking-widest text-slate-200">
            STACK <span className="text-blue-400">AL</span>CHEMIST
          </span>
        </Link>
        <div className="flex items-center gap-8">
          <Link href="/about" className="text-sm text-slate-300 transition-colors duration-300 hover:text-white">About</Link>
          <a href="#pricing" className="text-sm text-slate-300 transition-colors duration-300 hover:text-white">Pricing</a>
          <span className="text-sm text-slate-300 transition-colors duration-300 hover:text-white cursor-pointer">Docs</span>
          <button className="rounded-full border border-slate-500/25 bg-slate-600/50 px-4 py-2 text-sm text-slate-100 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:bg-slate-600/70 hover:shadow-[0_0_20px_rgba(59,130,246,0.2)]">
            Sign In
          </button>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="relative z-10 flex min-h-[calc(100vh-88px)] flex-col items-center justify-center px-8 lg:px-16">
        <div className="w-full max-w-4xl" style={{ marginLeft: "5%" }}>
          {/* Eyebrow */}
          <div className="mb-6 flex items-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-slate-300">ARCHITECTURE SYNTHESIS ENGINE</span>
          </div>

          {/* Main headline */}
          <h1 className="mb-6 text-5xl font-bold tracking-tight text-white lg:text-7xl">
            <span className="block">SYNTHESIZE</span>
            <span className="block mt-1">YOUR <span className="text-blue-400">PLATFORM</span>.</span>
          </h1>

          {/* Subtext */}
          <p className="mb-8 max-w-xl text-lg leading-relaxed text-slate-300">
            Transform natural language into deployable software repositories. 
            PostgreSQL schema, .NET backend, Next.js frontend - all with a{" "}
            <span className="text-blue-400 font-medium">compile guarantee</span>.
          </p>

          {/* Mode Toggle */}
          <div className="mb-6 flex items-center gap-2">
            <button
              onClick={() => setMode("simple")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-all duration-300 ${
                mode === "simple"
                  ? "bg-blue-500 text-white shadow-[0_0_20px_rgba(59,130,246,0.3)]"
                  : "bg-slate-700/50 text-slate-400 hover:text-slate-200"
              }`}
            >
              Simple Mode
            </button>
            <button
              onClick={() => setMode("advanced")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-all duration-300 ${
                mode === "advanced"
                  ? "bg-blue-500 text-white shadow-[0_0_20px_rgba(59,130,246,0.3)]"
                  : "bg-slate-700/50 text-slate-400 hover:text-slate-200"
              }`}
            >
              Advanced Mode
            </button>
            <span className="ml-3 text-xs text-slate-500">
              {mode === "simple" ? "Natural language prompt" : "Visual entity wizard"}
            </span>
          </div>

          {/* The Alchemy Input */}
          <AlchemyInput 
            value={prompt}
            onChange={setPrompt}
            onSubmit={() => console.log("Synthesizing:", prompt)}
          />

          {/* Helper text */}
          <div className="mt-6 flex items-center gap-4">
            <span className="text-xs text-slate-400">Press</span>
            <kbd className="rounded border border-slate-500/40 bg-slate-600/50 px-2 py-0.5 font-mono text-xs text-slate-300">
              {isMac ? "⌘" : "Ctrl"} + Enter
            </kbd>
            <span className="text-xs text-slate-400">to synthesize</span>
          </div>

          {/* V1 Stack Badge */}
          <div className="mt-8 flex flex-wrap items-center gap-3">
            <span className="text-xs text-slate-500 uppercase tracking-wider">V1 Stack:</span>
            <div className="flex flex-wrap gap-2">
              {[".NET Web API", "Dapper", "Next.js", "PostgreSQL"].map((tech) => (
                <span 
                  key={tech}
                  className="rounded-full border border-slate-600/40 bg-slate-700/30 px-3 py-1 text-xs text-slate-400"
                >
                  {tech}
                </span>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="relative z-10 border-t border-slate-700/50 py-24 px-8 lg:px-16">
        <div className="mx-auto max-w-6xl">
          <div className="mb-16 text-center">
            <h2 className="mb-4 text-3xl font-bold text-white lg:text-4xl">
              The Swiss Cheese Method
            </h2>
            <p className="mx-auto max-w-2xl text-slate-400">
              We combine battle-tested Handlebars templates with targeted LLM generation.
              Core plumbing is deterministic. Business logic is synthesized. Result: reliable, scalable code.
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-2 lg:grid-cols-3">
            <FeatureCard 
              icon={<Shield className="h-6 w-6" />}
              title="Compile Guarantee"
              description="Every repository passes automated build verification before delivery. If it fails, we auto-retry with the LLM until it compiles."
            />
            <FeatureCard 
              icon={<Zap className="h-6 w-6" />}
              title="Real-Time Progress"
              description="Watch your architecture materialize with live WebSocket updates. See each phase: template injection, LLM generation, build verification."
            />
            <FeatureCard 
              icon={<Code2 className="h-6 w-6" />}
              title="Dual Mode Intake"
              description="Simple Mode: describe in plain English. Advanced Mode: visually define entities, relationships, and API endpoints with precision."
            />
            <FeatureCard 
              icon={<Database className="h-6 w-6" />}
              title="Full Stack Output"
              description="Receive a complete PostgreSQL schema, .NET backend with Dapper ORM, and Next.js frontend - all production-ready."
            />
            <FeatureCard 
              icon={<Layers className="h-6 w-6" />}
              title="IaC & Runbooks"
              description="Tier 3 unlocks AWS CDK, Terraform scripts, Helm Charts, and step-by-step deployment runbooks for your infrastructure."
            />
            <FeatureCard 
              icon={<Terminal className="h-6 w-6" />}
              title="Bring Your Own Key"
              description="Use our Claude 3.5 Sonnet by default, or bring your own API key for Anthropic, OpenAI, or OpenRouter."
            />
          </div>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="relative z-10 border-t border-slate-700/50 py-24 px-8 lg:px-16">
        <div className="mx-auto max-w-5xl">
          <div className="mb-16 text-center">
            <h2 className="mb-4 text-3xl font-bold text-white lg:text-4xl">
              Choose Your Tier
            </h2>
            <p className="mx-auto max-w-2xl text-slate-400">
              One-time generation fee. No subscriptions. High margin, high value.
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            <PricingCard 
              tier="Tier 1"
              name="Blueprint"
              price={299}
              features={[
                "Database schema JSON export",
                "API documentation",
                "Entity relationship diagrams",
              ]}
            />
            <PricingCard 
              tier="Tier 2"
              name="Boilerplate"
              price={599}
              featured
              features={[
                "Everything in Tier 1",
                "Full downloadable ZIP archive",
                ".NET + Next.js codebase",
                "PostgreSQL migrations",
              ]}
            />
            <PricingCard 
              tier="Tier 3"
              name="Infrastructure"
              price={999}
              features={[
                "Everything in Tier 2",
                "AWS CDK / Terraform scripts",
                "Helm Charts for Kubernetes",
                "Docker Compose files",
                "Deployment runbooks",
              ]}
            />
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="relative z-10 border-t border-slate-700/50 py-24 px-8 lg:px-16">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="mb-6 text-3xl font-bold text-white lg:text-4xl">
            Ready to transmute your ideas?
          </h2>
          <p className="mb-8 text-lg text-slate-400">
            Stop configuring boilerplate. Start building features.
          </p>
          <button 
            onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
            className="rounded-full bg-blue-500 px-8 py-3 text-base font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_30px_rgba(59,130,246,0.4)]"
          >
            Start Synthesizing
          </button>
        </div>
      </section>

      {/* Footer */}
      <footer className="relative z-10 border-t border-slate-700/50 py-12 px-8 lg:px-16">
        <div className="mx-auto flex max-w-6xl items-center justify-between">
          <div className="flex items-center gap-3">
            <img 
              src="/logo.svg" 
              alt="Stack Alchemist" 
              className="h-7 w-7 opacity-60"
            />
            <span className="font-mono text-xs tracking-widest text-slate-500">
              STACK <span className="text-blue-400/60">AL</span>CHEMIST
            </span>
          </div>
          <div className="text-xs text-slate-500">
            &copy; {new Date().getFullYear()} Stack Alchemist. All rights reserved.
          </div>
        </div>
      </footer>

      {/* Bottom accent line */}
      <div className="fixed bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-blue-500/30 to-transparent z-50" />
    </div>
  )
}

function FeatureCard({ icon, title, description }: { icon: React.ReactNode; title: string; description: string }) {
  return (
    <div className="group rounded-xl border border-slate-600/30 bg-slate-700/30 p-6 transition-all duration-300 hover:border-blue-500/30 hover:bg-slate-700/50">
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-lg bg-blue-500/10 text-blue-400 transition-colors group-hover:bg-blue-500/20">
        {icon}
      </div>
      <h3 className="mb-2 text-lg font-semibold text-white">{title}</h3>
      <p className="text-sm leading-relaxed text-slate-400">{description}</p>
    </div>
  )
}

function PricingCard({ 
  tier, 
  name, 
  price, 
  features, 
  featured = false 
}: { 
  tier: string
  name: string
  price: number
  features: string[]
  featured?: boolean 
}) {
  return (
    <div className={`relative rounded-xl border p-8 transition-all duration-300 ${
      featured 
        ? "border-blue-500/50 bg-slate-700/50 shadow-[0_0_30px_rgba(59,130,246,0.15)]" 
        : "border-slate-600/30 bg-slate-700/30 hover:border-slate-500/50"
    }`}>
      {featured && (
        <div className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-blue-500 px-3 py-1 text-xs font-medium text-white">
          Most Popular
        </div>
      )}
      <div className="mb-2 font-mono text-xs tracking-wider text-slate-500 uppercase">{tier}</div>
      <div className="mb-4 text-2xl font-bold text-white">{name}</div>
      <div className="mb-6">
        <span className="text-4xl font-bold text-white">${price}</span>
        <span className="ml-2 text-slate-500">one-time</span>
      </div>
      <ul className="mb-8 space-y-3">
        {features.map((feature) => (
          <li key={feature} className="flex items-start gap-3 text-sm text-slate-300">
            <Check className="mt-0.5 h-4 w-4 shrink-0 text-blue-400" />
            {feature}
          </li>
        ))}
      </ul>
      <button className={`w-full rounded-full py-3 text-sm font-medium transition-all duration-300 ${
        featured
          ? "bg-blue-500 text-white hover:bg-blue-400 hover:shadow-[0_0_20px_rgba(59,130,246,0.3)]"
          : "border border-slate-500/30 bg-slate-600/30 text-slate-200 hover:border-blue-500/40 hover:bg-slate-600/50"
      }`}>
        Get Started
      </button>
    </div>
  )
}
