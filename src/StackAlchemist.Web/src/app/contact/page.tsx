import type { Metadata } from "next";
import Link from "next/link";
import { Mail, MessageSquare, Bug } from "lucide-react";
import { ContentHeader } from "@/components/content-header";

export const metadata: Metadata = {
  title: "Contact",
  description:
    "Email StackAlchemist. Founder-operated — your message reaches Steve Ackley, not a ticketing queue.",
  alternates: { canonical: "/contact" },
};

const CONTACTS = [
  {
    icon: Mail,
    label: "General questions",
    email: "hello@stackalchemist.app",
    blurb: "Product questions, partnerships, press, or anything that doesn't fit the other buckets.",
  },
  {
    icon: Bug,
    label: "Bug reports & support",
    email: "support@stackalchemist.app",
    blurb: "Compile-guarantee failure, account issue, or generation error — include your project ID and we'll respond within one business day.",
  },
  {
    icon: MessageSquare,
    label: "Billing & refunds",
    email: "billing@stackalchemist.app",
    blurb: "Refund requests, invoice questions, or anything involving money.",
  },
];

export default function ContactPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-3xl mx-auto py-16 px-6">
          <header className="mb-14">
            <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
              Contact
            </span>
            <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
              Get in touch
            </h1>
            <p className="mt-4 text-slate-300 text-sm leading-relaxed max-w-2xl">
              StackAlchemist is founder-operated. Your message reaches me
              (<Link href="/about" className="text-electric hover:underline">Steve Ackley</Link>)
              directly, not a ticketing queue. Pick the address that matches what you need.
            </p>
          </header>

          <div className="space-y-4">
            {CONTACTS.map((c) => {
              const Icon = c.icon;
              return (
                <a
                  key={c.email}
                  href={`mailto:${c.email}`}
                  className="group flex items-start gap-4 p-6 border border-slate-700/50 rounded-lg hover:border-electric/50 hover:bg-slate-800/30 transition-all"
                >
                  <Icon className="h-5 w-5 text-electric mt-0.5 shrink-0" aria-hidden="true" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-baseline justify-between gap-2">
                      <h2 className="text-base font-semibold text-white group-hover:text-electric transition-colors">
                        {c.label}
                      </h2>
                      <span className="font-mono text-xs text-slate-300 group-hover:text-electric transition-colors">
                        {c.email}
                      </span>
                    </div>
                    <p className="mt-2 text-slate-400 text-sm leading-relaxed">{c.blurb}</p>
                  </div>
                </a>
              );
            })}
          </div>

          <section className="mt-14 p-6 border border-slate-700/40 rounded-lg bg-slate-800/20">
            <h3 className="text-sm font-semibold text-white uppercase tracking-widest mb-3">
              Before you email about a failed build
            </h3>
            <p className="text-slate-300 text-sm leading-relaxed">
              If you have a generated repo that won&apos;t compile, that is covered by the{" "}
              <Link href="/pricing" className="text-electric hover:underline">
                compile guarantee
              </Link>
              . Send us the project ID (in your dashboard) and the exact error from{" "}
              <code className="px-1.5 py-0.5 bg-slate-800 rounded text-xs text-electric">dotnet build</code>{" "}
              or{" "}
              <code className="px-1.5 py-0.5 bg-slate-800 rounded text-xs text-electric">npm run build</code>{" "}
              — that&apos;s enough for us to diagnose and either fix or refund.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
