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

export function FaqSection() {
  return (
    <section className="relative z-10 border-t border-slate-600/30 py-24 px-6 sm:px-8 lg:px-16">
      <div className="mx-auto max-w-3xl">
        <div className="mb-12 text-center">
          <div className="mb-4 flex items-center justify-center gap-3">
            <div className="h-px w-12 bg-gradient-to-r from-transparent via-accent/60 to-transparent" />
            <span className="font-mono text-xs tracking-[0.3em] text-accent uppercase">Frequently Asked</span>
            <div className="h-px w-12 bg-gradient-to-r from-accent/60 via-transparent to-transparent" />
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
  );
}
