import type { Metadata } from "next";
import Link from "next/link";
import { ContentHeader } from "@/components/content-header";

export const metadata: Metadata = {
  title: "Terms of Service",
  description:
    "The contract between you and StackAlchemist. Short version: you buy once, you own the code, we make no uptime promises you didn't pay for.",
  alternates: { canonical: "/terms" },
};

const LAST_UPDATED = "April 18, 2026";

export default function TermsPage() {
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
              Terms of Service
            </h1>
            <p className="mt-4 font-mono text-xs text-slate-400">
              Last updated {LAST_UPDATED}
            </p>
          </header>

          <section className="prose prose-invert prose-slate max-w-none prose-headings:text-white prose-p:text-slate-300 prose-li:text-slate-300 prose-strong:text-white prose-a:text-electric">
            <h2>The short version</h2>
            <ul>
              <li>You pay once, you download your repo, you own the code. No subscription, no lock-in.</li>
              <li>Don&apos;t use StackAlchemist to generate code that violates the law or third-party IP.</li>
              <li>We make no guarantees that the generated code fits any particular business purpose — you still need to review it before shipping.</li>
              <li>If something on our side breaks, our liability is capped at what you paid.</li>
            </ul>

            <h2>1. Who we are</h2>
            <p>
              StackAlchemist (&ldquo;we,&rdquo; &ldquo;us&rdquo;) is a product of Steve Ackley,
              operating in the United States. By using the service you (&ldquo;you,&rdquo; the
              customer) agree to these terms.
            </p>

            <h2>2. What you get</h2>
            <p>
              StackAlchemist accepts a natural-language specification and returns a compiled .NET +
              Next.js + PostgreSQL repository as a downloadable artifact. The tier you purchase
              determines how much of the infrastructure and deployment story is included.
            </p>

            <h2>3. Ownership of generated code</h2>
            <p>
              The code we ship to you is yours. You can use it, modify it, relicense it, sell the
              resulting application — whatever you want. We retain no rights over the generated
              artifact. The underlying templates and the StackAlchemist engine remain our property.
            </p>

            <h2>4. Payment and refunds</h2>
            <p>
              Pricing is one-time per generated project, shown on the{" "}
              <Link href="/pricing">pricing page</Link>. Payments are processed by Stripe. If the
              compile guarantee fails — that is, we hand you a repository that does not build on a
              clean checkout — request a refund within 14 days at{" "}
              <a href="mailto:billing@stackalchemist.app">billing@stackalchemist.app</a> and we&apos;ll
              refund in full.
            </p>

            <h2>5. Acceptable use</h2>
            <p>You agree not to use StackAlchemist to generate code intended to:</p>
            <ul>
              <li>Violate any law or regulation, including export controls and sanctions.</li>
              <li>Infringe on a third party&apos;s intellectual property.</li>
              <li>Target or harass individuals, or produce illegal content.</li>
              <li>Scrape, reverse-engineer, or stress-test our generator at scale without a written agreement.</li>
            </ul>

            <h2>6. Service availability</h2>
            <p>
              The service is provided &ldquo;as-is.&rdquo; We do our best to keep it up, but we do
              not offer an SLA unless you have a separate written agreement. Planned maintenance will
              be announced in advance when possible.
            </p>

            <h2>7. Warranty and liability</h2>
            <p>
              The compile guarantee is the only warranty we make: if your generated repo does not
              build, we fix it or refund you. Beyond that, we disclaim all implied warranties,
              including merchantability and fitness for a particular purpose. Our aggregate liability
              to you is capped at the amount you paid for the generation that gave rise to the claim.
            </p>

            <h2>8. Termination</h2>
            <p>
              We may suspend or terminate accounts that violate these terms, issue refunds for any
              undelivered generations, and require you to stop using the service. You may delete
              your account at any time via your dashboard or by emailing support.
            </p>

            <h2>9. Changes</h2>
            <p>
              We may update these terms. Material changes will be announced with a new &ldquo;last
              updated&rdquo; date and, if you have an account, by email. Continued use after changes
              take effect means you accept the revised terms.
            </p>

            <h2>10. Governing law</h2>
            <p>
              These terms are governed by the laws of the State of New York, United States, without
              regard to conflict-of-law principles. Disputes will be resolved in the state and
              federal courts located in New York County, New York.
            </p>

            <h2>11. Contact</h2>
            <p>
              Questions? <Link href="/contact">Contact us</Link> or email{" "}
              <a href="mailto:legal@stackalchemist.app">legal@stackalchemist.app</a>.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
