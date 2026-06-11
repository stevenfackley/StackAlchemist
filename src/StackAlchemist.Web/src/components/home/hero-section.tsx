export function HeroSection() {
  return (
    <section className="relative z-10 min-h-[calc(100vh-72px)] px-6 py-12 sm:px-8 sm:py-16 lg:px-16 lg:py-0">
      <div className="mx-auto flex min-h-[calc(100vh-120px)] max-w-7xl items-center justify-center">
        <div className="w-full max-w-5xl">
          <div className="mb-6 flex items-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-slate-400 uppercase">
              Architecture Synthesis Engine · V1 · 100% Build-Guaranteed
            </span>
          </div>

          <h1 data-testid="home-hero-title" className="mb-6 font-bold tracking-tight leading-[0.95] text-white">
            <span className="block" style={{ fontSize: "clamp(2.8rem, 8vw, 6.5rem)" }}>AI SaaS Generator</span>
            <span className="block mt-1" style={{ fontSize: "clamp(2.8rem, 8vw, 6.5rem)" }}>
              with a <span className="text-blue-400">Compile Guarantee.</span>
            </span>
          </h1>

          <p className="mb-8 max-w-2xl text-base leading-relaxed text-slate-300 sm:text-lg">
            Synthesize a deployable .NET 10 + Next.js 15 + PostgreSQL repository from natural
            language. Every generation passes a real compiler before delivery — no hallucinated
            imports, no half-finished scaffolds.
          </p>

          <div className="rounded-[28px] border border-slate-600/30 bg-slate-700/20 px-5 py-5 shadow-[0_0_40px_rgba(15,23,42,0.2)] backdrop-blur-sm sm:px-6 sm:py-6">
            <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr] lg:gap-8">
              <div>
                <div className="font-mono text-[11px] uppercase tracking-[0.22em] text-blue-400">
                  Built For Fast Handoffs
                </div>
                <p className="mt-2 max-w-2xl text-sm leading-relaxed text-slate-300 sm:text-base">
                  Start from a prompt or open the entity wizard, review the architecture shape, then choose
                  whether you want planning artifacts, a generated codebase, or the full infrastructure handoff.
                </p>
              </div>
              <div className="flex flex-wrap content-start gap-2 lg:justify-end">
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
      </div>
    </section>
  );
}
