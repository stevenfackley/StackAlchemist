"use client";

import Link from "next/link";

export function CtaSection() {
  return (
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
            className="w-full sm:w-auto rounded-full bg-accent px-8 py-3 text-base font-medium text-white transition-all duration-300 hover:-translate-y-0.5 hover:bg-accent/90 hover:shadow-[0_0_30px_rgba(59,130,246,0.4)]"
          >
            Start Synthesizing
          </button>
          <Link
            href="/about"
            className="w-full sm:w-auto rounded-full border border-slate-500/30 bg-slate-700/30 px-8 py-3 text-base font-medium text-slate-200 transition-all duration-300 hover:border-accent/40 hover:bg-slate-700/50 text-center"
          >
            Learn More
          </Link>
        </div>
      </div>
    </section>
  );
}
