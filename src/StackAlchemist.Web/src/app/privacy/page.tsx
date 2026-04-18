import type { Metadata } from "next";
import Link from "next/link";
import { ContentHeader } from "@/components/content-header";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description:
    "What StackAlchemist collects, what we do not, and who else touches your data. Plain-English summary followed by the formal policy.",
  alternates: { canonical: "/privacy" },
};

const LAST_UPDATED = "April 18, 2026";

export default function PrivacyPage() {
  return (
    <div className="min-h-screen flex flex-col bg-void">
      <ContentHeader />
      <main className="flex-1">
        <article className="max-w-3xl mx-auto py-16 px-6">
          <header className="mb-12">
            <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
              Legal
            </span>
            <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
              Privacy Policy
            </h1>
            <p className="mt-4 font-mono text-xs text-slate-400">
              Last updated {LAST_UPDATED}
            </p>
          </header>

          <section className="prose prose-invert prose-slate max-w-none prose-headings:text-white prose-p:text-slate-300 prose-li:text-slate-300 prose-strong:text-white prose-a:text-electric">
            <h2>The short version</h2>
            <ul>
              <li>We collect the minimum needed to run the product: your email, payment, and the spec you submit.</li>
              <li>We do not sell your data, ever. We do not use it to train any AI model.</li>
              <li>Analytics is <strong>Plausible</strong> — cookieless, no personal profiles, no cross-site tracking.</li>
              <li>Payments are handled by <strong>Stripe</strong>. Auth is handled by <strong>Supabase</strong>.</li>
              <li>The generated code belongs to you. We keep a copy only long enough to deliver it.</li>
            </ul>

            <h2>What we collect</h2>
            <p>
              When you sign up we collect your email address through Supabase Auth. When you pay,
              Stripe collects the card details directly — we never see or store them. When you submit
              a spec, we store the spec text and the generated repository long enough to deliver the
              download to you; these are deleted after 30 days.
            </p>
            <p>
              Our server logs record request paths, IP addresses, and user-agent strings for security
              and abuse detection. Logs are retained for 30 days.
            </p>

            <h2>What we do not do</h2>
            <p>
              We do not sell, rent, or trade your personal information. We do not share your specs
              or generated code with anyone outside the subprocessors below. We do not use your spec
              or generated code to train any large language model — not ours, not a provider&apos;s.
            </p>

            <h2>Subprocessors</h2>
            <ul>
              <li><strong>Stripe</strong> — payment processing.</li>
              <li><strong>Supabase</strong> — authentication and database.</li>
              <li><strong>Anthropic</strong> — the LLM that generates your code. Requests are sent
                through our API key or yours (BYOK tier). Anthropic&apos;s policy: prompts are not
                used for training on the commercial API.</li>
              <li><strong>Plausible</strong> — cookieless web analytics.</li>
              <li><strong>Cloudflare</strong> — DNS, CDN, DDoS protection.</li>
              <li><strong>AWS</strong> — compute and storage.</li>
            </ul>

            <h2>Your rights</h2>
            <p>
              You can request a copy of your data, request deletion, or object to processing at any
              time by emailing <a href="mailto:privacy@stackalchemist.app">privacy@stackalchemist.app</a>.
              We&apos;ll respond within 30 days. If you are in the EU/UK or California, you have
              additional rights under GDPR/UK-GDPR and CCPA — the process is the same.
            </p>

            <h2>Cookies</h2>
            <p>
              We use a small number of first-party cookies for authentication (session + refresh
              tokens from Supabase) and for preserving your cart during checkout. That&apos;s it.
              No advertising cookies. No cross-site tracking.
            </p>

            <h2>Changes</h2>
            <p>
              Material changes will be announced on this page with a new &ldquo;last updated&rdquo;
              date and, if you have an account, by email.
            </p>

            <h2>Contact</h2>
            <p>
              Questions? <Link href="/contact">Contact us</Link> or email{" "}
              <a href="mailto:privacy@stackalchemist.app">privacy@stackalchemist.app</a>.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
