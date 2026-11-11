"use client";

import Link from "next/link";
import { AlertCircle, Loader2, RefreshCw } from "lucide-react";
import { failureOutcome, MAX_RETRIES, type FailureOutcome, type PaymentOutcome } from "@/lib/generation-outcome";
import type { GenerationErrorCategory, PaymentStatus, Tier } from "@/lib/types";

// ─── What happened ───────────────────────────────────────────────────────────
// One entry per error_category: the heading and a plain statement of what went
// wrong. What the customer can do next (retry, refund, support) is not said
// here: it depends on the payment and the retries left, and is worked out by
// failureOutcome() so the page never offers a Retry that will be refused or
// stays silent about a refund.

interface CategoryCopy {
  title: string;
  body: string;
  /** Encouragement to retry. Shown only when a Retry is actually on offer. */
  retryHint?: string;
  /** Extra links shown after the buttons. */
  extraLinks?: Array<{ label: string; href: string }>;
}

const CATEGORY_COPY: Record<GenerationErrorCategory, CategoryCopy> = {
  quota: {
    title: "Free build limit reached",
    body:
      "You've used all your free builds for this month. Upgrade to a paid tier to keep generating, or come back when your quota resets.",
    extraLinks: [{ label: "View Paid Tiers →", href: "/#pricing" }],
  },
  schema: {
    title: "Couldn't turn your idea into an app plan",
    body: "The AI couldn't turn that description into a usable plan for the app.",
    retryHint: "This sometimes works on a second try.",
  },
  build: {
    title: "The generated code did not compile",
    body:
      "The generated code still failed to compile after 3 automatic correction attempts, so it was not delivered.",
  },
  rate_limit: {
    title: "AI service temporarily busy",
    body: "The AI service was handling too many requests and couldn't process yours.",
    retryHint: "This usually clears within a minute.",
  },
  network: {
    title: "Connection problem",
    body: "A network or timeout problem interrupted the build.",
    retryHint: "Retrying usually resolves this.",
  },
  internal: {
    title: "Something went wrong on our side",
    body: "An error on our side stopped the build, and it could not be finished.",
  },
};

const SUPPORT_EMAIL = "support@stackalchemist.app";

function Support() {
  return (
    <a href={`mailto:${SUPPORT_EMAIL}`} className="underline underline-offset-2 hover:text-accent">
      {SUPPORT_EMAIL}
    </a>
  );
}

function GenerationId({ id }: { id?: string }) {
  if (!id) return null;
  return (
    <span className="mt-2 block text-slate-400">
      Generation ID:{" "}
      <code className="font-mono text-[11px] text-accent px-1 rounded bg-slate-800/60 break-all">{id}</code>
    </span>
  );
}

// ─── What happened to the payment ────────────────────────────────────────────

const PAYMENT_HEADING: Partial<Record<PaymentOutcome, string>> = {
  kept: "Your payment",
  refunded: "You are being refunded",
  refund_expected: "You will be refunded",
  disputed: "Payment under dispute",
  unpaid: "No completed payment",
};

function PaymentNote({ outcome, generationId }: { outcome: FailureOutcome; generationId?: string }) {
  const heading = PAYMENT_HEADING[outcome.payment];
  if (!heading) return null;

  return (
    <div
      data-testid="payment-outcome"
      data-outcome={outcome.payment}
      className="w-full max-w-md rounded-xl border border-slate-600/40 bg-slate-700/20 px-5 py-4 text-left"
    >
      <p className="font-mono text-[10px] uppercase tracking-widest text-slate-400">{heading}</p>
      <p className="mt-1.5 text-sm leading-relaxed text-slate-300">
        {outcome.payment === "kept" && (
          <>
            Your payment is still in place, and retrying costs nothing. If the build still fails
            after your last retry, it is refunded automatically.
          </>
        )}
        {outcome.payment === "refunded" && (
          <>
            A full refund for this build has been started. It reaches your original payment method
            in 5–10 business days. You don&apos;t need to do anything.
          </>
        )}
        {outcome.payment === "refund_expected" && (
          <>
            A full refund for this build starts automatically, usually within a few minutes, and
            we email you when it does. If no email arrives within an hour, write to <Support />.
            <GenerationId id={generationId} />
          </>
        )}
        {outcome.payment === "disputed" && (
          <>
            This payment is under dispute with your card issuer, so the build can&apos;t be
            retried. To sort it out, write to <Support />.
            <GenerationId id={generationId} />
          </>
        )}
        {outcome.payment === "unpaid" && (
          <>
            We have no completed payment on record for this build, so it was not built. If you
            were charged, write to <Support />.
            <GenerationId id={generationId} />
          </>
        )}
      </p>
    </div>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export interface GenerationErrorPanelProps {
  /** The failed generation. Preferred: with it the panel knows the tier, the retries used and
   *  the payment, and says what is true about each. The individual props below are for the
   *  simple/advanced pages, whose error state has no row. */
  generation?: {
    id: string;
    error_category?: GenerationErrorCategory | null;
    error_message: string | null;
    attempt_count: number;
    tier?: Tier;
    payment_status?: PaymentStatus | null;
  };
  /** Override or standalone category (simple/advanced pages). */
  category?: GenerationErrorCategory | null;
  /** Standalone error message when no generation object is available */
  errorMessage?: string | null;
  /** Standalone generation id for the support note */
  generationId?: string;
  /** Called when the user clicks Retry. If absent, the retry button is hidden. */
  onRetry?: () => void;
  isRetrying?: boolean;
  /** Why the last click on Retry did not start a build, shown under the buttons. */
  retryError?: string | null;
  /** data-testid forwarded to the root element */
  testId?: string;
}

export function GenerationErrorPanel({
  generation,
  category: categoryProp,
  errorMessage: errorMessageProp,
  generationId: generationIdProp,
  onRetry,
  isRetrying = false,
  retryError,
  testId,
}: GenerationErrorPanelProps) {
  const category = generation?.error_category ?? categoryProp ?? null;
  const errorMessage = generation?.error_message ?? errorMessageProp ?? null;
  const generationId = generation?.id ?? generationIdProp;

  // Without a row there is no payment and no retry count to reason about: the caller's own
  // onRetry (go back to the form) is offered wherever the category allows one.
  const outcome = failureOutcome({
    tier: generation?.tier ?? 0,
    error_category: category,
    attempt_count: generation?.attempt_count ?? 0,
    payment_status: generation?.payment_status,
  });

  // A paid build that was refused for want of a completed payment did not fail: it never ran.
  // The payment note says why, so the category's "what went wrong" copy would be untrue here.
  const neverBuilt = outcome.payment === "unpaid" || outcome.payment === "disputed";
  const copy: CategoryCopy | null = neverBuilt
    ? { title: "This build was not started", body: "It has no completed payment behind it." }
    : category ? CATEGORY_COPY[category] : null;
  const showRetry = !!onRetry && outcome.canRetry;
  // Retries are counted only for a real row; the standalone pages retry by resubmitting.
  const countsRetries = !!generation;
  // A free build with nothing left to press, and no payment note to carry the next step.
  const needsSupportLine = outcome.payment === "none" && category === "internal";

  return (
    <div
      data-testid={testId}
      className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-6"
    >
      <div className="h-16 w-16 rounded-full bg-rose-500/10 border-2 border-rose-500/30 flex items-center justify-center">
        <AlertCircle className="h-8 w-8 text-rose-400" />
      </div>

      <div className="text-center space-y-2 max-w-md">
        <h2 className="text-xl font-bold text-white">
          {copy?.title ?? "Something went wrong"}
        </h2>
        <p className="text-slate-400 text-sm leading-relaxed">
          {copy ? copy.body : (errorMessage ?? "An unexpected error occurred. Please try again.")}
          {showRetry && copy?.retryHint && <> {copy.retryHint}</>}
          {needsSupportLine && (
            <>
              {" "}If it keeps happening, write to <Support />.
              <GenerationId id={generationId} />
            </>
          )}
        </p>
        {countsRetries && outcome.retriesUsedUp && (
          <p data-testid="retries-used-up" className="text-slate-400 text-sm leading-relaxed">
            You have used all {MAX_RETRIES} retries for this build.
            {outcome.payment === "none" && " Start a new one to try again."}
          </p>
        )}
      </div>

      <PaymentNote outcome={outcome} generationId={generationId} />

      <div className="flex flex-col sm:flex-row gap-3">
        {showRetry && (
          <button
            onClick={onRetry}
            disabled={isRetrying}
            className="flex items-center gap-2 rounded-full border border-accent/30 text-accent hover:bg-accent/10 px-5 py-2.5 text-sm font-medium transition-all duration-300 disabled:opacity-60"
          >
            {isRetrying ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
            {isRetrying ? "Retrying..." : "Retry"}
          </button>
        )}
        <Link
          href="/"
          className="flex items-center gap-2 rounded-full bg-slate-700/50 border border-slate-600/50 text-slate-300 hover:border-accent/40 hover:text-accent px-5 py-2.5 text-sm font-medium transition-all duration-300"
        >
          {countsRetries && !showRetry ? "Start a New Build" : "Start Over"}
        </Link>
        {copy?.extraLinks?.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className="flex items-center gap-2 rounded-full bg-accent hover:bg-accent/90 text-white px-5 py-2.5 text-sm font-medium transition-all duration-300"
          >
            {link.label}
          </Link>
        ))}
      </div>

      {showRetry && countsRetries && !isRetrying && (
        <p data-testid="retries-left" className="text-xs text-slate-500">
          {outcome.retriesLeft} of {MAX_RETRIES} {outcome.retriesLeft === 1 ? "retry" : "retries"} left
        </p>
      )}

      {retryError && !isRetrying && (
        <p role="alert" className="max-w-md text-center text-sm text-rose-300">
          {retryError}
        </p>
      )}
    </div>
  );
}
