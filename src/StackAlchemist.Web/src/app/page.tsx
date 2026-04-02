"use client";

import { useState, useRef } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Logo } from "@/components/logo";
import { cn } from "@/lib/utils";

export default function HomePage() {
  const router = useRouter();
  const [mode, setMode] = useState<"simple" | "advanced">("simple");
  const [prompt, setPrompt] = useState("");
  const [focused, setFocused] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  function handleGo() {
    if (mode === "advanced") {
      router.push("/advanced");
      return;
    }
    if (!prompt.trim()) return;
    router.push(`/simple?q=${encodeURIComponent(prompt.trim())}`);
  }

  return (
    <div className="min-h-screen flex flex-col bg-[#09090B] relative overflow-hidden">

      {/* Animated mesh gradient orbs */}
      <div className="absolute inset-0 pointer-events-none select-none" aria-hidden>
        <div
          className="absolute top-[-15%] right-[-5%] w-[700px] h-[700px] rounded-full animate-pulse-slow"
          style={{ background: "radial-gradient(circle, rgba(0,163,255,0.07) 0%, transparent 70%)" }}
        />
        <div
          className="absolute bottom-[-25%] left-[-8%] w-[600px] h-[600px] rounded-full animate-pulse-slower"
          style={{ background: "radial-gradient(circle, rgba(0,163,255,0.04) 0%, transparent 70%)", animationDelay: "4s" }}
        />
        <div
          className="absolute top-[45%] left-[35%] w-[400px] h-[400px] rounded-full animate-pulse-slow"
          style={{ background: "radial-gradient(circle, rgba(0,163,255,0.025) 0%, transparent 70%)", animationDelay: "2s" }}
        />
      </div>

      {/* Navbar */}
      <header className="border-b border-white/[0.04] bg-[#09090B]/80 backdrop-blur-md sticky top-0 z-50">
        <nav className="max-w-7xl mx-auto px-6 md:px-12 h-14 flex items-center justify-between">
          <Logo />
          <div className="flex items-center gap-8">
            <Link href="/about" className="text-[11px] font-mono tracking-widest text-zinc-500 hover:text-white transition-colors uppercase">
              About
            </Link>
            <Link href="/pricing" className="text-[11px] font-mono tracking-widest text-zinc-500 hover:text-white transition-colors uppercase">
              Pricing
            </Link>
            <Link href="/story" className="text-[11px] font-mono tracking-widest text-zinc-500 hover:text-white transition-colors uppercase">
              Story
            </Link>
            <Link
              href="/login"
              className="text-[11px] font-mono tracking-widest border border-white/[0.08] text-zinc-400 hover:border-[#00A3FF]/50 hover:text-[#00A3FF] transition-all duration-200 px-4 py-1.5 uppercase"
            >
              Login
            </Link>
          </div>
        </nav>
      </header>

      {/* Hero */}
      <main className="flex-1 flex flex-col justify-center px-6 md:px-12 lg:px-24 py-16 relative z-10">
        <div className="max-w-[1100px] ml-0 lg:ml-[4%]">

          {/* Eyebrow label */}
          <div className="flex items-center gap-3 mb-10">
            <div className="w-1.5 h-1.5 rounded-full bg-[#00A3FF]" />
            <p className="font-mono text-[10px] tracking-[0.28em] text-zinc-600 uppercase">
              Stack Alchemy · V1 · 100% Build-Guaranteed
            </p>
          </div>

          {/* Hero heading */}
          <h1 className="font-sans font-bold leading-[0.92] tracking-tight mb-8 select-none">
            <span className="block text-white" style={{ fontSize: "clamp(3.5rem, 10vw, 8rem)" }}>
              TRANSMUTE
            </span>
            <span className="block text-zinc-600" style={{ fontSize: "clamp(3.5rem, 10vw, 8rem)" }}>
              YOUR IDEA
            </span>
            <span className="block" style={{ fontSize: "clamp(3.5rem, 10vw, 8rem)" }}>
              <span className="text-white">INTO </span>
              <span className="text-[#00A3FF]">GOLD.</span>
            </span>
          </h1>

          {/* Subtext */}
          <p className="text-zinc-400 text-base md:text-lg leading-relaxed max-w-lg mb-12 tracking-tight">
            Describe your SaaS in plain language. Receive a compiled,
            production-ready codebase — guaranteed to build on the first try.
          </p>

          {/* Terminal input */}
          <div
            className={cn(
              "relative max-w-2xl transition-all duration-500 cursor-text",
              focused
                ? "shadow-[0_0_80px_rgba(0,163,255,0.16)]"
                : "shadow-[0_0_40px_rgba(0,163,255,0.06)]"
            )}
            onClick={() => inputRef.current?.focus()}
          >
            <div
              className={cn(
                "bg-white/[0.025] backdrop-blur-md transition-all duration-300",
                focused ? "border border-white/[0.10]" : "border border-white/[0.05]"
              )}
            >
              {/* Terminal chrome bar */}
              <div className="flex items-center gap-1.5 px-4 py-2.5 border-b border-white/[0.04]">
                <div className="w-2 h-2 rounded-full bg-zinc-800" />
                <div className="w-2 h-2 rounded-full bg-zinc-800" />
                <div className="w-2 h-2 rounded-full bg-zinc-800" />
                <span className="font-mono text-[10px] text-zinc-700 ml-3 tracking-[0.2em] uppercase">
                  {mode === "advanced" ? "stack-alchemy · advanced-mode" : "stack-alchemy · simple-mode"}
                </span>
              </div>

              {/* Input row */}
              <div className="flex items-center px-4 py-4 gap-3">
                <span className="font-mono text-[#00A3FF] text-sm select-none shrink-0 opacity-80">›_</span>
                <input
                  ref={inputRef}
                  type="text"
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && handleGo()}
                  onFocus={() => setFocused(true)}
                  onBlur={() => setFocused(false)}
                  placeholder={
                    mode === "advanced"
                      ? "Advanced mode active — click Open Editor to begin"
                      : "Describe your SaaS... e.g. a project management tool for teams"
                  }
                  className={cn(
                    "flex-1 bg-transparent font-mono text-sm text-white placeholder:text-zinc-700",
                    "focus:outline-none caret-[#00A3FF]",
                    mode === "advanced" && "opacity-30 pointer-events-none"
                  )}
                  disabled={mode === "advanced"}
                />
                <button
                  onClick={handleGo}
                  className="font-mono text-[11px] tracking-[0.12em] uppercase px-5 py-2 transition-all duration-300 bg-[#00A3FF] text-[#09090B] font-bold hover:-translate-y-0.5 hover:shadow-[0_6px_24px_rgba(0,163,255,0.35)] active:translate-y-0 active:shadow-none shrink-0"
                >
                  {mode === "simple" ? "Transmute →" : "Open Editor →"}
                </button>
              </div>

              {/* Mode selector */}
              <div className="flex items-center gap-6 px-4 py-3 border-t border-white/[0.04]">
                {(["simple", "advanced"] as const).map((m) => (
                  <button
                    key={m}
                    onClick={() => setMode(m)}
                    className={cn(
                      "flex items-center gap-2 font-mono text-[10px] tracking-[0.15em] uppercase transition-all duration-200",
                      mode === m ? "text-[#00A3FF]" : "text-zinc-700 hover:text-zinc-400"
                    )}
                  >
                    <div
                      className={cn(
                        "w-1.5 h-1.5 rounded-full transition-colors duration-200",
                        mode === m ? "bg-[#00A3FF]" : "bg-zinc-800"
                      )}
                    />
                    {m === "simple" ? "Simple · AI-First" : "Advanced · Visual Editor"}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Stack footnote */}
          <p className="font-mono text-[10px] text-zinc-700 tracking-[0.22em] uppercase mt-6">
            V1 Stack ·{" "}
            <span className="text-zinc-600">.NET 10</span>
            {" · "}
            <span className="text-zinc-600">Next.js 15</span>
            {" · "}
            <span className="text-zinc-600">PostgreSQL</span>
            {" · "}
            <span className="text-zinc-600">Supabase</span>
          </p>
        </div>
      </main>

      {/* Features strip */}
      <section className="relative z-10 border-t border-white/[0.04] py-16 px-6 md:px-12 lg:px-24">
        <div className="max-w-[1100px] ml-0 lg:ml-[4%] grid grid-cols-1 md:grid-cols-3 gap-10">
          {[
            {
              title: "Swiss Cheese Method",
              body: "Deterministic Handlebars templates carry the structure. LLM intelligence fills the holes with your business logic. Predictable scaffolding, dynamic domain code.",
            },
            {
              title: "Compile Guarantee",
              body: "Every repository is run through dotnet build before delivery. Three-attempt auto-correction loop. If it doesn't compile, you don't pay.",
            },
            {
              title: "Three Tiers",
              body: "Blueprint, Boilerplate, or full Infrastructure. One-time payment. No subscriptions. You own the architecture forever.",
            },
          ].map((f) => (
            <div key={f.title} className="space-y-3 group">
              <div className="flex items-center gap-3">
                <div className="w-0.5 h-5 bg-[#00A3FF]/30 group-hover:bg-[#00A3FF]/70 transition-colors duration-300" />
                <h3 className="font-mono text-[11px] font-bold tracking-[0.18em] text-zinc-300 uppercase">
                  {f.title}
                </h3>
              </div>
              <p className="text-zinc-600 text-sm leading-relaxed pl-3.5">{f.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Bottom bar */}
      <footer className="relative z-10 border-t border-white/[0.04] py-5 px-6 md:px-12 lg:px-24">
        <div className="max-w-[1100px] ml-0 lg:ml-[4%] flex items-center justify-between flex-wrap gap-4">
          <p className="font-mono text-[10px] text-zinc-700 tracking-[0.2em] uppercase">
            StackAlchemist · Transmutation Engine v1.0
          </p>
          <div className="flex items-center gap-6">
            <Link href="/pricing" className="font-mono text-[10px] text-zinc-700 hover:text-zinc-400 transition-colors uppercase tracking-widest">
              Pricing
            </Link>
            <Link href="/about" className="font-mono text-[10px] text-zinc-700 hover:text-zinc-400 transition-colors uppercase tracking-widest">
              About
            </Link>
            <Link href="/story" className="font-mono text-[10px] text-zinc-700 hover:text-zinc-400 transition-colors uppercase tracking-widest">
              Story
            </Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
