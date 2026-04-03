import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import { DocsSidebar } from "@/components/docs-sidebar";
import "highlight.js/styles/github-dark.css";

export const metadata: Metadata = {
  title: "Docs — StackAlchemist",
  description:
    "Documentation for StackAlchemist: user guides, advanced topics, and self-hosting instructions.",
};

export default function DocsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-800 flex flex-col">

      {/* ── Top Nav ─────────────────────────────────────────────────── */}
      <header className="sticky top-0 z-50 border-b border-slate-600/30 bg-slate-800/90 backdrop-blur-md">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex h-14 items-center justify-between gap-4">
            {/* Logo */}
            <Link href="/" className="flex items-center gap-2.5 shrink-0 transition-opacity hover:opacity-80">
              <Image
                src="/logo.svg"
                alt="StackAlchemist"
                width={26}
                height={26}
                className="drop-shadow-[0_0_8px_rgba(77,166,255,0.4)]"
              />
              <span className="hidden sm:block text-sm font-semibold tracking-[0.16em] text-slate-100 uppercase">
                Stack&nbsp;<span className="text-[#4da6ff]">Al</span>chemist
              </span>
            </Link>

            {/* Breadcrumb label */}
            <div className="flex items-center gap-2 text-xs font-mono text-slate-500">
              <span className="hidden sm:inline text-slate-600">/</span>
              <span className="text-blue-400 tracking-widest uppercase">Docs</span>
            </div>

            {/* Nav links */}
            <div className="flex items-center gap-5">
              <Link
                href="/"
                className="hidden sm:block font-mono text-xs tracking-widest text-slate-400 hover:text-white transition-colors uppercase"
              >
                Build
              </Link>
              <Link
                href="/about"
                className="hidden sm:block font-mono text-xs tracking-widest text-slate-400 hover:text-white transition-colors uppercase"
              >
                About
              </Link>
              <Link
                href="/pricing"
                className="hidden sm:block font-mono text-xs tracking-widest text-slate-400 hover:text-white transition-colors uppercase"
              >
                Pricing
              </Link>
              <a
                href="https://github.com/stevenfackley/StackAlchemist"
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-xs border border-slate-600/40 text-slate-400 hover:border-blue-400/50 hover:text-blue-400 transition-colors px-3 py-1.5 uppercase tracking-widest"
              >
                GitHub
              </a>
            </div>
          </div>
        </div>
      </header>

      {/* ── Body: Sidebar + Content ─────────────────────────────────── */}
      <div className="flex flex-1 mx-auto w-full max-w-7xl">

        {/* Sidebar — hidden on mobile, sticky on md+ */}
        <aside className="hidden md:block w-60 lg:w-64 shrink-0 border-r border-slate-700/40">
          <div className="sticky top-14 h-[calc(100vh-3.5rem)] overflow-y-auto py-8 px-4">
            {/* Docs label */}
            <div className="mb-6 px-3">
              <p className="font-mono text-[10px] tracking-[0.35em] text-slate-600 uppercase">
                Documentation
              </p>
            </div>
            <DocsSidebar />
          </div>
        </aside>

        {/* Mobile: horizontal scrollable nav */}
        <div className="md:hidden w-full border-b border-slate-700/40 bg-slate-800/80 px-4 py-3 overflow-x-auto">
          <MobileDocNav />
        </div>

        {/* Main content */}
        <main className="flex-1 min-w-0 px-4 sm:px-8 lg:px-12 py-10">
          {children}
        </main>
      </div>

      {/* ── Footer ──────────────────────────────────────────────────── */}
      <footer className="border-t border-slate-700/40 py-6 px-4">
        <div className="mx-auto max-w-7xl flex flex-col sm:flex-row items-center justify-between gap-4">
          <p className="font-mono text-xs text-slate-600 tracking-widest">
            STACKALCHEMIST · DOCS
          </p>
          <div className="flex items-center gap-6">
            <Link href="/docs/getting-started" className="font-mono text-xs text-slate-600 hover:text-slate-400 transition-colors uppercase tracking-widest">
              Getting Started
            </Link>
            <Link href="/docs/faq" className="font-mono text-xs text-slate-600 hover:text-slate-400 transition-colors uppercase tracking-widest">
              FAQ
            </Link>
            <a
              href="https://github.com/stevenfackley/StackAlchemist"
              target="_blank"
              rel="noopener noreferrer"
              className="font-mono text-xs text-slate-600 hover:text-slate-400 transition-colors uppercase tracking-widest"
            >
              GitHub
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}

// Mobile horizontal nav — server-renderable, just links
function MobileDocNav() {
  const links = [
    { slug: "getting-started", label: "Get Started" },
    { slug: "simple-mode", label: "Simple Mode" },
    { slug: "advanced-mode", label: "Advanced" },
    { slug: "tiers-and-pricing", label: "Pricing" },
    { slug: "your-output", label: "Output" },
    { slug: "faq", label: "FAQ" },
    { slug: "swiss-cheese-method", label: "Swiss Cheese" },
    { slug: "compile-guarantee", label: "Compile Guarantee" },
    { slug: "architecture-overview", label: "Architecture" },
    { slug: "self-hosting", label: "Self-Hosting" },
  ];

  return (
    <div className="flex gap-3 whitespace-nowrap">
      {links.map((link) => (
        <Link
          key={link.slug}
          href={`/docs/${link.slug}`}
          className="font-mono text-xs text-slate-400 hover:text-blue-400 transition-colors uppercase tracking-wider shrink-0"
        >
          {link.label}
        </Link>
      ))}
    </div>
  );
}
