import Link from "next/link"
import { ArrowLeft, Cpu, FileCode, GitBranch, Lock, Rocket, Workflow } from "lucide-react"

export default function AboutPage() {
  return (
    <main className="min-h-screen bg-slate-800">
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
          <Link href="/about" className="text-sm text-white">About</Link>
          <Link href="/#pricing" className="text-sm text-slate-300 transition-colors duration-300 hover:text-white">Pricing</Link>
          <span className="text-sm text-slate-300 transition-colors duration-300 hover:text-white cursor-pointer">Docs</span>
          <button className="rounded-full border border-slate-500/25 bg-slate-600/50 px-4 py-2 text-sm text-slate-100 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:bg-slate-600/70 hover:shadow-[0_0_20px_rgba(59,130,246,0.2)]">
            Sign In
          </button>
        </div>
      </nav>

      {/* Content */}
      <div className="relative z-10 mx-auto max-w-4xl px-8 py-16 lg:px-16">
        <Link 
          href="/" 
          className="mb-8 inline-flex items-center gap-2 text-sm text-slate-400 transition-colors hover:text-blue-400"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Home
        </Link>

        <h1 className="mb-8 text-4xl font-bold tracking-tight text-white lg:text-5xl">
          About Stack <span className="text-blue-400">Al</span>chemist
        </h1>

        <div className="space-y-8 text-lg leading-relaxed text-slate-300">
          <p>
            Stack Alchemist is a B2B and B2C micro-SaaS platform that transmutes natural language 
            prompts or visual schemas into fully functional software repositories. We deliver a 
            complete PostgreSQL database, .NET backend, and Next.js frontend - all with a{" "}
            <span className="text-blue-400 font-medium">compile guarantee</span>.
          </p>

          <div className="rounded-xl border border-slate-600/30 bg-slate-700/30 p-6">
            <h2 className="mb-4 text-xl font-semibold text-white">The Problem We Solve</h2>
            <ul className="space-y-3 text-slate-400">
              <li className="flex items-start gap-3">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-blue-400" />
                Developers lose momentum configuring database contexts, routing, and authentication
              </li>
              <li className="flex items-start gap-3">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-blue-400" />
                Non-technical founders face prohibitive upfront costs to build baseline infrastructure
              </li>
              <li className="flex items-start gap-3">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-blue-400" />
                Existing static boilerplates require significant manual refactoring to fit specific data models
              </li>
              <li className="flex items-start gap-3">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-blue-400" />
                General AI coding agents generate unstructured, unscalable code and struggle with context limits
              </li>
            </ul>
          </div>

          <h2 className="pt-6 text-2xl font-semibold text-white">The Swiss Cheese Method</h2>
          
          <p>
            We use a hybrid Retrieval-Augmented Generation (RAG) pipeline. Core plumbing is handled 
            via static Handlebars templates - deterministic and battle-tested. The LLM only generates 
            dynamic business logic: Dapper queries, C# controllers, and Next.js pages. This minimizes 
            token usage while maximizing reliability.
          </p>

          <h2 className="pt-6 text-2xl font-semibold text-white">The Compile Guarantee</h2>

          <p>
            Every generated repository goes through automated build verification. The backend reconstructs 
            the physical directory structure, executes a CLI build command (<code className="rounded bg-slate-700 px-2 py-0.5 text-sm text-blue-400">dotnet build</code>), 
            and only delivers upon success. If it fails, the system parses the error output and 
            auto-retries with the LLM for correction.
          </p>

          <div className="grid gap-6 pt-8 md:grid-cols-2">
            <ProcessCard 
              icon={<FileCode className="h-5 w-5" />}
              title="Dual Mode Intake"
              description="Simple Mode: natural language prompt that generates an editable visual schema. Advanced Mode: manual entity definition with a stepper wizard."
            />
            <ProcessCard 
              icon={<Cpu className="h-5 w-5" />}
              title="V1 Stack"
              description=".NET Web API with Dapper micro-ORM, Next.js frontend, and PostgreSQL. V1.5 adds Entity Framework toggle. V2 introduces Node.js/Prisma/Nuxt alternatives."
            />
            <ProcessCard 
              icon={<Lock className="h-5 w-5" />}
              title="Security First"
              description="All prompts are sanitized. BYOK API keys are encrypted at rest in Supabase. Row Level Security restricts generation history access."
            />
            <ProcessCard 
              icon={<Workflow className="h-5 w-5" />}
              title="Real-Time Progress"
              description="Watch generation unfold via Supabase WebSockets. See logs for template injection, LLM generation, and compile verification."
            />
            <ProcessCard 
              icon={<GitBranch className="h-5 w-5" />}
              title="IaC Export"
              description="Tier 3 unlocks AWS CDK, Terraform, Helm Charts, Docker Compose, and step-by-step deployment runbooks customized to your project."
            />
            <ProcessCard 
              icon={<Rocket className="h-5 w-5" />}
              title="Zero Egress Storage"
              description="Compiled archives are stored on Cloudflare R2 with 24-hour presigned URLs. No egress fees, fast global delivery."
            />
          </div>

          <div className="mt-12 rounded-xl border border-blue-500/30 bg-blue-500/5 p-6">
            <h3 className="mb-3 font-mono text-sm tracking-wider text-blue-400">OUR MISSION</h3>
            <p className="text-slate-300">
              Reduce the time required to stand up a scalable software architecture from weeks to minutes. 
              We de-risk deployments by selling Infrastructure as Code and runbooks - not by executing 
              code on customer infrastructure.
            </p>
          </div>

          <div className="flex justify-center pt-8">
            <Link 
              href="/#pricing"
              className="rounded-full bg-blue-500 px-8 py-3 text-base font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_30px_rgba(59,130,246,0.4)]"
            >
              View Pricing
            </Link>
          </div>
        </div>
      </div>

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
    </main>
  )
}

function ProcessCard({ icon, title, description }: { icon: React.ReactNode; title: string; description: string }) {
  return (
    <div className="rounded-lg border border-slate-600/30 bg-slate-700/20 p-5">
      <div className="mb-3 flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-500/10 text-blue-400">
          {icon}
        </div>
        <h3 className="font-semibold text-white">{title}</h3>
      </div>
      <p className="text-sm leading-relaxed text-slate-400">{description}</p>
    </div>
  )
}
