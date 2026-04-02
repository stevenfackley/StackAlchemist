"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Navbar } from "@/components/navbar";
import { cn } from "@/lib/utils";

export default function HomePage() {
  const router = useRouter();
  const [mode, setMode] = useState<"simple" | "advanced">("simple");
  const [prompt, setPrompt] = useState("");

  function handleGo() {
    if (mode === "advanced") {
      router.push("/advanced");
      return;
    }
    if (!prompt.trim()) return;
    router.push(`/simple?q=${encodeURIComponent(prompt.trim())}`);
  }

  return (
    <div className="min-h-screen flex flex-col">
      <Navbar />

      <main className="flex-1 flex flex-col items-center justify-center px-4 py-24">
        <div className="w-full max-w-2xl space-y-8">
          <div className="space-y-2 text-center">
            <p className="font-mono text-xs tracking-[0.3em] text-electric uppercase">
              Stack Alchemy v1
            </p>
            <h1 className="font-mono text-3xl md:text-4xl font-bold tracking-tight text-white leading-tight">
              TRANSMUTE PROMPTS
              <br />
              INTO ARCHITECTURE
            </h1>
            <p className="text-slate-400 text-sm mt-3">
              The 100% Build-Guaranteed SaaS Scaffolder.
              <br />
              Natural language &rarr; compiled, downloadable code repository.
            </p>
          </div>

          <div className="flex gap-0">
            <input
              type="text"
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleGo()}
              placeholder="Enter natural language prompt..."
              className={cn(
                "flex-1 bg-slate-surface border border-slate-border border-r-0 px-4 py-3",
                "font-mono text-sm text-white placeholder:text-slate-500",
                "focus:outline-none focus:border-electric transition-colors",
                mode === "advanced" && "opacity-50 pointer-events-none"
              )}
              disabled={mode === "advanced"}
            />
            <button
              onClick={handleGo}
              className="bg-electric hover:bg-blue-600 active:bg-blue-700 text-white font-mono text-xs font-bold tracking-widest px-5 py-3 uppercase transition-colors border border-electric"
            >
              {mode === "simple" ? "GO" : "START"}
            </button>
          </div>

          <div className="flex items-center gap-8">
            {(["simple", "advanced"] as const).map((m) => (
              <label
                key={m}
                className="flex items-center gap-2 cursor-pointer group"
              >
                <div
                  onClick={() => setMode(m)}
                  className={cn(
                    "w-4 h-4 border flex items-center justify-center transition-colors",
                    mode === m
                      ? "border-electric bg-electric"
                      : "border-slate-500 group-hover:border-blue-400"
                  )}
                >
                  {mode === m && <div className="w-2 h-2 bg-white" />}
                </div>
                <span className="font-mono text-xs text-slate-300 group-hover:text-white transition-colors">
                  {m === "simple" ? "Simple Mode (AI-First)" : "Advanced Mode (Manual)"}
                </span>
              </label>
            ))}
          </div>

          <div className="border-t border-slate-surface pt-4">
            <p className="font-mono text-xs text-center text-slate-500 tracking-widest uppercase">
              V1 Stack:{" "}
              <span className="text-electric">.NET 10</span>
              {" + "}
              <span className="text-electric">Next.js 15</span>
              {" + "}
              <span className="text-electric">PostgreSQL</span>
              {" + "}
              <span className="text-electric">Supabase</span>
            </p>
          </div>
        </div>
      </main>

      <section id="features" className="border-t border-slate-surface py-20 px-4">
        <div className="max-w-5xl mx-auto grid grid-cols-1 md:grid-cols-3 gap-px bg-slate-surface">
          {[
            {
              title: "Swiss Cheese Method",
              body: "Static Handlebars templates + LLM-injected business logic. Deterministic structure, dynamic intelligence.",
            },
            {
              title: "Compile Guarantee",
              body: "Every generated repository is run through dotnet build before delivery. 3-retry auto-correction loop on failure.",
            },
            {
              title: "Three Tiers",
              body: "Blueprint, Boilerplate, or full Infrastructure. Pay once, own the architecture.",
            },
          ].map((f) => (
            <div key={f.title} className="bg-void p-6 space-y-2">
              <h3 className="font-mono text-xs font-bold tracking-widest text-electric uppercase">
                {f.title}
              </h3>
              <p className="text-slate-400 text-sm leading-relaxed">{f.body}</p>
            </div>
          ))}
        </div>
      </section>

      <section id="pricing" className="border-t border-slate-surface py-20 px-4">
        <div className="max-w-md mx-auto text-center space-y-4">
          <h2 className="font-mono text-xs tracking-[0.3em] text-electric uppercase">
            Pricing
          </h2>
          <p className="text-slate-400 text-sm leading-relaxed">
            Three tiers. One-time payment. You own the architecture.
          </p>
          <a
            href="/advanced?step=3"
            className="inline-block border border-electric text-electric hover:bg-electric hover:text-white font-mono text-xs tracking-widest uppercase py-2 px-6 transition-colors"
          >
            See Pricing &rarr;
          </a>
        </div>
      </section>

      <footer className="border-t border-slate-surface py-6 px-4">
        <p className="font-mono text-xs text-slate-600 text-center tracking-widest uppercase">
          StackAlchemist &middot; Transmutation Engine v1.0
        </p>
      </footer>
    </div>
  );
}
