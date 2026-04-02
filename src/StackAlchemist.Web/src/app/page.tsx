"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import {
  Shield,
  Zap,
  Code2,
  Database,
  Layers,
  Terminal,
  Check,
  ShoppingCart,
  BarChart3,
  Heart,
  ClipboardList,
  Home,
  Utensils,
  BookOpen,
  Ticket,
  Briefcase,
  Package,
  Users,
  MessageSquare,
  Dumbbell,
  Calendar,
  CreditCard,
  Gamepad2,
  Truck,
  Hotel,
  Menu,
  X,
} from "lucide-react";
import { AlchemyInput } from "@/components/alchemy-input";

const EXAMPLE_APPS = [
  {
    icon: ShoppingCart,
    name: "E-commerce Platform",
    description: "Multi-vendor marketplace with inventory, orders, product catalog, and Stripe payments.",
    tags: ["Products", "Orders", "Vendors"],
  },
  {
    icon: BarChart3,
    name: "SaaS Analytics Dashboard",
    description: "Real-time KPI tracking, custom report builder, team workspaces, and alert pipelines.",
    tags: ["Metrics", "Reports", "Teams"],
  },
  {
    icon: Heart,
    name: "Healthcare Patient Portal",
    description: "Patient records, appointment scheduling, prescription management, and provider directory.",
    tags: ["Patients", "Appointments", "EHR"],
  },
  {
    icon: ClipboardList,
    name: "Project Management Tool",
    description: "Sprints, tasks, boards, time tracking, and team collaboration with Kanban views.",
    tags: ["Tasks", "Sprints", "Teams"],
  },
  {
    icon: Home,
    name: "Real Estate Platform",
    description: "Property listings, agent profiles, virtual tours, lead management, and MLS sync.",
    tags: ["Listings", "Agents", "Leads"],
  },
  {
    icon: Utensils,
    name: "Food Delivery App",
    description: "Restaurant catalog, menu management, order tracking, and driver dispatch system.",
    tags: ["Orders", "Drivers", "Menus"],
  },
  {
    icon: BookOpen,
    name: "Learning Management System",
    description: "Courses, video lessons, quizzes, certificates, progress tracking, and cohorts.",
    tags: ["Courses", "Quizzes", "Certs"],
  },
  {
    icon: Ticket,
    name: "Event Ticketing Platform",
    description: "Event creation, seat maps, QR ticket issuance, capacity management, and check-in.",
    tags: ["Events", "Tickets", "Check-in"],
  },
  {
    icon: Briefcase,
    name: "Freelancer Marketplace",
    description: "Talent profiles, project bids, contract management, escrow payments, and reviews.",
    tags: ["Profiles", "Bids", "Escrow"],
  },
  {
    icon: Package,
    name: "Inventory Management",
    description: "Stock levels, supplier management, purchase orders, warehouses, and reorder alerts.",
    tags: ["Stock", "Suppliers", "POs"],
  },
  {
    icon: Users,
    name: "HR Onboarding Tool",
    description: "Employee records, onboarding flows, document signing, org charts, and payroll sync.",
    tags: ["Employees", "Docs", "Payroll"],
  },
  {
    icon: MessageSquare,
    name: "Customer Support Platform",
    description: "Ticket routing, live chat, knowledge base, SLA tracking, and CSAT reporting.",
    tags: ["Tickets", "Chat", "SLA"],
  },
  {
    icon: Dumbbell,
    name: "Fitness & Wellness App",
    description: "Workout plans, nutrition logs, progress charts, coach assignments, and challenges.",
    tags: ["Workouts", "Nutrition", "Coach"],
  },
  {
    icon: Calendar,
    name: "Appointment Booking System",
    description: "Service catalog, availability calendar, automated reminders, and payment collection.",
    tags: ["Bookings", "Calendar", "Payments"],
  },
  {
    icon: CreditCard,
    name: "FinTech Dashboard",
    description: "Multi-account management, transaction history, budget categories, and investment tracking.",
    tags: ["Accounts", "Budgets", "Investments"],
  },
  {
    icon: Gamepad2,
    name: "Gaming Leaderboard",
    description: "Player profiles, match history, ELO ratings, achievement system, and tournament brackets.",
    tags: ["Players", "Matches", "Rankings"],
  },
  {
    icon: Truck,
    name: "Logistics Tracker",
    description: "Shipment management, route optimization, driver tracking, POD capture, and billing.",
    tags: ["Shipments", "Routes", "Billing"],
  },
  {
    icon: Hotel,
    name: "Hotel Management System",
    description: "Room inventory, reservations, housekeeping schedules, POS billing, and guest profiles.",
    tags: ["Rooms", "Reservations", "POS"],
  },
];

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
    href: "/advanced?step=3&tier=1",
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
    href: "/advanced?step=3&tier=2",
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
    href: "/advanced?step=3&tier=3",
  },
];

const FAQS = [
  {
    q: "Is this a subscription?",
    a: "No. Every tier is a one-time payment. You pay once, you own the architecture forever. No monthly fees, no lock-in.",
  },
  {
    q: "What is the Compile Guarantee?",
    a: "Your generated Boilerplate or Infrastructure package is run through dotnet build before delivery. If it fails, an automatic correction loop re-runs the LLM and retries — up to three times. If it still fails, you get a full refund.",
  },
  {
    q: "What stack does V1 generate?",
    a: ".NET 10 Web API + Next.js 15 (App Router, TypeScript, Tailwind CSS) + PostgreSQL + Supabase. Additional stacks are planned for V2.",
  },
  {
    q: "Can I use the generated code commercially?",
    a: "Yes. The generated code is entirely yours. No attribution required, no licensing restrictions. Build your SaaS, sell it, scale it.",
  },
  {
    q: "How long does generation take?",
    a: "Simple schemas generate in under 30 seconds. Complex multi-entity systems typically take 60–90 seconds. You'll see real-time progress throughout.",
  },
];

export default function HomePage() {
  const router = useRouter();
  const [mode, setMode] = useState<"simple" | "advanced">("simple");
  const [prompt, setPrompt] = useState("");
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  function handleSubmit() {
    if (mode === "advanced") {
      router.push("/advanced");
      return;
    }
    if (!prompt.trim()) return;
    router.push(`/simple?q=${encodeURIComponent(prompt.trim())}`);
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-800 relative overflow-x-hidden">

      {/* ─── Background Effects ─────────────────────────────────────── */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none select-none" aria-hidden>
        <div
          className="absolute top-1/4 -right-1/4 h-[800px] w-[800px] rounded-full animate-pulse-glow"
          style={{
            backgroundImage: "radial-gradient(circle, rgba(59, 130, 246, 0.15) 0%, rgba(59, 130, 246, 0.05) 45%, transparent 70%)",
          }}
        />
        <div
          className="absolute -bottom-1/4 -left-1/4 h-[600px] w-[600px] rounded-full animate-pulse-glow"
          style={{
            backgroundImage: "radial-gradient(circle, rgba(99, 102, 241, 0.08) 0%, transparent 55%)",
            animationDelay: "2s",
          }}
        />
        <div
          className="absolute top-1/2 left-1/3 h-[400px] w-[600px] rounded-full opacity-40"
          style={{
            backgroundImage: "radial-gradient(ellipse, rgba(148, 163, 184, 0.06) 0%, transparent 60%)",
          }}
        />
        {/* Subtle grid */}
        <div
          className="absolute inset-0 opacity-[0.025]"
          style={{
            backgroundImage:
              "linear-gradient(rgba(255,255,255,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.3) 1px, transparent 1px)",
            backgroundSize: "80px 80px",
          }}
        />
      </div>

      {/* ─── Navigation ─────────────────────────────────────────────── */}
      <nav className="relative z-50 flex items-center justify-between px-6 sm:px-8 lg:px-16 py-5 border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0">
        {/* Logo */}
        <Link href="/" className="flex items-center gap-3 transition-opacity hover:opacity-80">
          <Image
            src="/logo.svg"
            alt="Stack Alchemist"
            width={34}
            height={34}
            className="drop-shadow-[0_0_10px_rgba(77,166,255,0.5)]"
            priority
          />
          <span className="text-sm font-semibold tracking-[0.18em] text-slate-100 uppercase">
            Stack&nbsp;&nbsp;<span className="text-[#4da6ff]">Al</span>chemist
          </span>
        </Link>

        {/* Desktop nav */}
        <div className="hidden md:flex items-center gap-8">
          <Link href="/about" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">About</Link>
          <a href="#pricing" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Pricing</a>
          <Link href="/story" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Story</Link>
          <Link href="/docs" className="text-sm text-slate-300 hover:text-white transition-colors duration-300">Docs</Link>
          <button className="rounded-full border border-slate-500/30 bg-slate-700/50 px-4 py-2 text-sm text-slate-100 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-blue-500/40 hover:shadow-[0_0_20px_rgba(59,130,246,0.2)]">
            Sign In
          </button>
        </div>

        {/* Mobile hamburger */}
        <button
          className="md:hidden p-2 text-slate-300 hover:text-white transition-colors"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-label="Toggle menu"
        >
          {mobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
        </button>
      </nav>

      {/* Mobile menu */}
      {mobileMenuOpen && (
        <div className="relative z-40 md:hidden bg-slate-800/95 backdrop-blur-md border-b border-slate-600/30 px-6 py-4 flex flex-col gap-4">
          <Link href="/about" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>About</Link>
          <a href="#pricing" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Pricing</a>
          <Link href="/story" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Story</Link>
          <Link href="/docs" className="text-sm text-slate-300 hover:text-white" onClick={() => setMobileMenuOpen(false)}>Docs</Link>
          <button className="w-full rounded-full border border-slate-500/30 bg-slate-700/50 px-4 py-2 text-sm text-slate-100">
            Sign In
          </button>
        </div>
      )}

      {/* ─── Hero Section ───────────────────────────────────────────── */}
      <section className="relative z-10 min-h-[calc(100vh-72px)] px-6 py-12 sm:px-8 sm:py-16 lg:px-16 lg:py-0">
        <div className="mx-auto grid min-h-[calc(100vh-120px)] max-w-7xl items-center gap-10 lg:grid-cols-[minmax(0,1.05fr)_minmax(420px,0.95fr)] lg:gap-14">
          <div className="max-w-3xl">
            <div className="mb-6 flex items-center gap-3">
              <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
              <span className="font-mono text-xs tracking-[0.3em] text-slate-400 uppercase">
                Architecture Synthesis Engine · V1 · 100% Build-Guaranteed
              </span>
            </div>

            <h1 className="mb-6 font-bold tracking-tight leading-[0.95] text-white">
              <span className="block" style={{ fontSize: "clamp(2.8rem, 8vw, 6.5rem)" }}>SYNTHESIZE</span>
              <span className="block mt-1" style={{ fontSize: "clamp(2.8rem, 8vw, 6.5rem)" }}>
                YOUR <span className="text-blue-400">PLATFORM.</span>
              </span>
            </h1>

            <p className="mb-8 max-w-2xl text-base leading-relaxed text-slate-300 sm:text-lg">
              Transform natural language into deployable software repositories.
              PostgreSQL schema, .NET backend, Next.js frontend, and a handoff path built around a{" "}
              <span className="text-blue-400 font-medium">compile guarantee</span>.
            </p>

            <div className="rounded-2xl border border-slate-600/30 bg-slate-700/20 px-4 py-4 backdrop-blur-sm">
              <div className="grid gap-4 sm:grid-cols-[1.2fr_0.8fr]">
                <div>
                  <div className="font-mono text-[11px] uppercase tracking-[0.22em] text-blue-400">
                    Built For Fast Handoffs
                  </div>
                  <p className="mt-2 text-sm leading-relaxed text-slate-300">
                    Start from a prompt or open the entity wizard, review the architecture shape, then choose
                    whether you want planning artifacts, a generated codebase, or the full infrastructure handoff.
                  </p>
                </div>
                <div className="flex flex-wrap content-start gap-2">
                  {[".NET 10", "Next.js 15", "PostgreSQL", "Supabase", "Dapper", "Compile Guarantee"].map((tech) => (
                    <span
                      key={tech}
                      className="rounded-full border border-slate-600/40 bg-slate-800/50 px-3 py-1 text-[11px] text-slate-400 font-mono"
                    >
                      {tech}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </div>

          <div className="w-full">
            <div className="rounded-[28px] border border-slate-600/30 bg-slate-800/60 p-4 shadow-[0_0_40px_rgba(15,23,42,0.35)] backdrop-blur-md sm:p-6 lg:p-7">
              <div className="mb-5 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="font-mono text-xs tracking-[0.28em] text-blue-400 uppercase">Launch Console</div>
                  <h2 className="mt-2 text-xl font-semibold text-white sm:text-2xl">
                    Shape the product brief without leaving the front page.
                  </h2>
                </div>
                <div className="text-xs text-slate-500 font-mono sm:max-w-[220px] sm:text-right">
                  {mode === "simple" ? "Prompt-first synthesis path" : "Structured wizard path"}
                </div>
              </div>

              <div className="mb-5 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <div className="flex items-center gap-2 flex-wrap">
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
                </div>
                <span className="text-xs text-slate-500 font-mono md:text-right">
                  {mode === "simple" ? "Natural language prompt" : "Visual entity wizard"}
                </span>
              </div>

              {mode === "simple" ? (
                <>
                  <AlchemyInput
                    value={prompt}
                    onChange={setPrompt}
                    onSubmit={handleSubmit}
                    className="max-w-none"
                  />
                  <div className="mt-4 grid gap-3 lg:grid-cols-[1.1fr_0.9fr]">
                    <div className="rounded-2xl border border-slate-600/30 bg-slate-900/40 px-4 py-4">
                      <div className="font-mono text-[11px] uppercase tracking-[0.24em] text-blue-400">
                        What You Walk Away With
                      </div>
                      <div className="mt-3 grid gap-3 sm:grid-cols-3">
                        <div>
                          <div className="text-sm font-semibold text-white">Blueprint</div>
                          <div className="mt-1 text-xs leading-relaxed text-slate-400">
                            Schema, API surface, and planning artifacts.
                          </div>
                        </div>
                        <div>
                          <div className="text-sm font-semibold text-white">Boilerplate</div>
                          <div className="mt-1 text-xs leading-relaxed text-slate-400">
                            Generated .NET and Next.js application structure.
                          </div>
                        </div>
                        <div>
                          <div className="text-sm font-semibold text-white">Infrastructure</div>
                          <div className="mt-1 text-xs leading-relaxed text-slate-400">
                            IaC, charts, and deployment guidance.
                          </div>
                        </div>
                      </div>
                    </div>
                    <div className="rounded-2xl border border-slate-600/30 bg-slate-900/40 px-4 py-4">
                      <div className="font-mono text-[11px] uppercase tracking-[0.24em] text-blue-400">
                        Flow
                      </div>
                      <div className="mt-3 space-y-2">
                        {[
                          "1. Define the idea or model the entities",
                          "2. Review the architecture shape",
                          "3. Choose the handoff depth",
                        ].map((step) => (
                          <div key={step} className="text-xs leading-relaxed text-slate-400">
                            {step}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                  <div className="mt-4 flex flex-col gap-3 border-t border-slate-700/40 pt-4 text-xs text-slate-500 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-center gap-2">
                      <span>Press</span>
                      <kbd className="rounded border border-slate-500/40 bg-slate-700/50 px-2 py-0.5 font-mono text-xs text-slate-300">
                        Ctrl + Enter
                      </kbd>
                      <span>to synthesize</span>
                    </div>
                    <div className="font-mono uppercase tracking-[0.2em] text-slate-600">
                      Prompt first, details second
                    </div>
                  </div>
                </>
              ) : (
                <div className="rounded-2xl border border-blue-500/35 bg-slate-700/60 p-6 backdrop-blur-md">
                  <div className="mb-3 flex items-center gap-3">
                    <Terminal className="h-5 w-5 text-blue-400" />
                    <span className="font-mono text-xs tracking-wider text-slate-400 uppercase">Advanced Mode Active</span>
                  </div>
                  <p className="mb-5 text-sm leading-relaxed text-slate-300 sm:text-base">
                    Use the visual entity wizard to define your schema with precision across entities,
                    relationships, API endpoints, and tier selection without leaving the main flow.
                  </p>
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="text-xs uppercase tracking-[0.2em] text-slate-500">
                      Matches the `/advanced` workspace structure
                    </div>
                    <button
                      onClick={handleSubmit}
                      className="rounded-full bg-blue-500 px-6 py-2.5 text-sm font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_24px_rgba(59,130,246,0.45)]"
                    >
                      Open Entity Wizard →
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* ─── What Can You Build? ─────────────────────────────────────── */}
      <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
        <div className="mx-auto max-w-7xl">
          <div className="mb-14 text-center">
            <div className="mb-4 flex items-center justify-center gap-3">
              <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
              <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">What you can build</span>
              <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
            </div>
            <h2 className="text-3xl font-bold text-white lg:text-4xl mb-4">
              One Platform. Infinite Applications.
            </h2>
            <p className="mx-auto max-w-2xl text-slate-400 leading-relaxed">
              StackAlchemist turns a paragraph into a production-ready codebase in under 90 seconds.
              Here&rsquo;s a taste of what you can synthesize today.
            </p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {EXAMPLE_APPS.map((app) => {
              const Icon = app.icon;
              return (
                <div
                  key={app.name}
                  className="group rounded-xl border border-slate-600/30 bg-slate-700/20 p-5 transition-all duration-300 hover:border-blue-500/30 hover:bg-slate-700/40 hover:shadow-[0_0_20px_rgba(59,130,246,0.08)] cursor-default"
                >
                  <div className="mb-3 flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-500/10 text-blue-400 transition-colors group-hover:bg-blue-500/20 shrink-0">
                      <Icon className="h-5 w-5" />
                    </div>
                    <h3 className="text-sm font-semibold text-white leading-tight">{app.name}</h3>
                  </div>
                  <p className="text-xs leading-relaxed text-slate-400 mb-3">{app.description}</p>
                  <div className="flex flex-wrap gap-1.5">
                    {app.tags.map((tag) => (
                      <span
                        key={tag}
                        className="rounded-full border border-slate-600/30 bg-slate-800/50 px-2 py-0.5 text-[10px] font-mono text-slate-500"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>

          <div className="mt-10 text-center">
            <p className="text-slate-400 text-sm mb-4">
              Don&rsquo;t see your use case? If you can describe it, StackAlchemist can synthesize it.
            </p>
            <button
              onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
              className="rounded-full border border-blue-500/30 bg-blue-500/10 px-6 py-2.5 text-sm text-blue-400 font-medium transition-all duration-300 hover:bg-blue-500/20 hover:border-blue-500/50"
            >
              Start Synthesizing ↑
            </button>
          </div>
        </div>
      </section>

      {/* ─── Features ────────────────────────────────────────────────── */}
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

      {/* ─── Pricing ─────────────────────────────────────────────────── */}
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

      {/* ─── FAQ ─────────────────────────────────────────────────────── */}
      <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
        <div className="mx-auto max-w-3xl">
          <div className="mb-12 text-center">
            <div className="mb-4 flex items-center justify-center gap-3">
              <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
              <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Frequently Asked</span>
              <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
            </div>
            <h2 className="text-2xl font-bold text-white">Got Questions?</h2>
          </div>

          <div className="space-y-0 divide-y divide-slate-700/50">
            {FAQS.map((faq) => (
              <div key={faq.q} className="py-6 grid grid-cols-1 md:grid-cols-5 gap-3 md:gap-6">
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

      {/* ─── CTA ─────────────────────────────────────────────────────── */}
      <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="mb-4 text-3xl font-bold text-white lg:text-4xl">
            Ready to transmute your idea?
          </h2>
          <p className="mb-8 text-lg text-slate-400 leading-relaxed">
            Stop configuring boilerplate. Start building features.
            Your next codebase is 90 seconds away.
          </p>
          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <button
              onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
              className="w-full sm:w-auto rounded-full bg-blue-500 px-8 py-3 text-base font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-blue-400 hover:shadow-[0_0_30px_rgba(59,130,246,0.4)]"
            >
              Start Synthesizing
            </button>
            <Link
              href="/about"
              className="w-full sm:w-auto rounded-full border border-slate-500/30 bg-slate-700/30 px-8 py-3 text-base font-medium text-slate-200 transition-all duration-300 hover:border-blue-500/40 hover:bg-slate-700/50 text-center"
            >
              Learn More
            </Link>
          </div>
        </div>
      </section>

      {/* ─── Footer ──────────────────────────────────────────────────── */}
      <footer className="relative z-10 border-t border-slate-600/30 py-10 px-6 sm:px-8 lg:px-16">
        <div className="mx-auto max-w-6xl">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-6">
            <div className="flex items-center gap-3">
              <Image
                src="/logo.svg"
                alt="Stack Alchemist"
                width={28}
                height={28}
                className="opacity-60"
              />
              <span className="font-mono text-xs tracking-widest text-slate-500">
                STACK <span className="text-blue-400/60">AL</span>CHEMIST
              </span>
            </div>
            <div className="flex flex-wrap items-center justify-center gap-6">
              <Link href="/pricing" className="font-mono text-xs text-slate-500 hover:text-slate-300 transition-colors uppercase tracking-widest">Pricing</Link>
              <Link href="/about" className="font-mono text-xs text-slate-500 hover:text-slate-300 transition-colors uppercase tracking-widest">About</Link>
              <Link href="/story" className="font-mono text-xs text-slate-500 hover:text-slate-300 transition-colors uppercase tracking-widest">Story</Link>
              <Link href="/docs" className="font-mono text-xs text-slate-500 hover:text-slate-300 transition-colors uppercase tracking-widest">Docs</Link>
            </div>
            <p className="font-mono text-xs text-slate-600">
              &copy; {new Date().getFullYear()} StackAlchemist
            </p>
          </div>
        </div>
      </footer>

      {/* Bottom accent line */}
      <div className="fixed bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-blue-500/30 to-transparent z-50" />
    </div>
  );
}
