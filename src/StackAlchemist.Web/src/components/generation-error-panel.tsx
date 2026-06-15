"use client";

import Link from "next/link";
import { AlertCircle, Loader2, RefreshCw } from "lucide-react";
import type { GenerationErrorCategory } from "@/lib/types";

// ─── Category configuration table ────────────────────────────────────────────
// Each entry defines the copy and CTA set for one error_category value.
// The null-fallback branch (below) handles pre-B1 rows and unrecognised values.

interface CategoryConfig {
  title: string;
  body: (ctx: { attemptCount: number; generationId?: string }) => React.ReactNode;
  /** Extra links shown BELOW the retry/start-over buttons */
  extraLinks?: Array<{ label: string; href: string }>;
  showRetry: boolean;
}

const CATEGORY_CONFIG: Record<NonNullable<GenerationErrorCategory>, CategoryConfig> = {
  quota: {
    title: "Free build limit reached",
    body: () => (
      <>
        You&apos;ve used all your free builds for this month. Upgrade to a paid tier to keep
        generating, or come back when your quota resets.
      </>
    ),
    extraLinks: [{ label: "View Paid Tiers →", href: "/#pricing" }],
    showRetry: false,
  },
  schema: {
    title: "Couldn't understand your idea",
    body: () => (
      <>
        The AI couldn&apos;t turn that description into a usable schema. Try editing your prompt
        to be more specific, or simplify the entities.
      </>
    ),
    showRetry: true,
  },
  build: {
    title: "Build failed",
    body: ({ attemptCount, generationId }) => (
      <>
        Compilation failed on attempt {attemptCount} of 3.
        {attemptCount >= 3 && (
          <>
            {" "}You&apos;re entitled to a full refund — contact{" "}
            <a href="mailto:support@stackalchemist.app" className="underline underline-offset-2 hover:text-accent">
              support
            </a>
            {" "}with your generation ID:{" "}
            {generationId && (
              <code className="font-mono text-[10px] text-accent px-1 rounded bg-slate-800/60">
                {generationId}
              </code>
            )}
          </>
        )}
      </>
    ),
    showRetry: true,
  },
  rate_limit: {
    title: "AI service temporarily busy",
    body: () => (
      <>
        The AI service received too many requests and couldn&apos;t process yours right now.
        This usually clears within a minute — try again shortly.
      </>
    ),
    showRetry: true,
  },
  network: {
    title: "Connection problem",
    body: () => (
      <>
        A network or timeout issue interrupted the build. The error has been logged.
        Retrying usually resolves this.
      </>
    ),
    showRetry: true,
  },
  internal: {
    title: "Unexpected error",
    body: ({ generationId }) => (
      <>
        An unexpected error occurred on our end. Please contact{" "}
        <a href="mailto:support@stackalchemist.app" className="underline underline-offset-2 hover:text-accent">
          support
        </a>
        {generationId && (
          <>
            {" "}with your generation ID:{" "}
            <code className="font-mono text-[10px] text-accent px-1 rounded bg-slate-800/60">
              {generationId}
            </code>
          </>
        )}
      </>
    ),
    showRetry: false,
  },
};

// ─── Component ────────────────────────────────────────────────────────────────

export interface GenerationErrorPanelProps {
  /** Full generation object — preferred; individual props below are for simple/advanced pages
   *  that don't have the full object in their error state. */
  generation?: {
    id: string;
    error_category?: GenerationErrorCategory | null;
    error_message: string | null;
    attempt_count: number;
  };
  /** Override or standalone category (used by simple/advanced pages pre-B1) */
  category?: GenerationErrorCategory | null;
  /** Standalone error message when no generation object is available */
  errorMessage?: string | null;
  /** Standalone generation id for the support note */
  generationId?: string;
  /** Called when the user clicks Retry. If absent, the retry button is hidden. */
  onRetry?: () => void;
  isRetrying?: boolean;
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
  testId,
}: GenerationErrorPanelProps) {
  const category = generation?.error_category ?? categoryProp ?? null;
  const errorMessage = generation?.error_message ?? errorMessageProp ?? null;
  const generationId = generation?.id ?? generationIdProp;
  const attemptCount = generation?.attempt_count ?? 0;
  const canRetry = !!onRetry;

  const config = category ? CATEGORY_CONFIG[category] : null;
  const showRetry = canRetry && (config ? config.showRetry : true);

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
          {config?.title ?? "Something went wrong"}
        </h2>
        <p className="text-slate-400 text-sm leading-relaxed">
          {config
            ? config.body({ attemptCount, generationId })
            : (errorMessage ?? "An unexpected error occurred. Please try again.")}
        </p>
      </div>

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
          Start Over
        </Link>
        {config?.extraLinks?.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className="flex items-center gap-2 rounded-full bg-accent hover:bg-accent/90 text-white px-5 py-2.5 text-sm font-medium transition-all duration-300"
          >
            {link.label}
          </Link>
        ))}
      </div>
    </div>
  );
}
