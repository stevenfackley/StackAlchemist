"use client";

import { useEffect, useState, useTransition } from "react";
import { retryGeneration } from "@/lib/actions";
import { failureOutcome } from "@/lib/generation-outcome";
import { useGenerationStatus } from "@/lib/hooks/use-generation-status";
import type { Generation } from "@/lib/types";
import { isDemoMode } from "@/lib/runtime-config";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import { isFreeGeneration } from "./components/status";
import { GenerateHeader } from "./components/generate-header";
import { InProgressPanel } from "./components/in-progress-panel";
import { FreeTierPanel } from "./components/free-tier-panel";
import { PaidTierPanel } from "./components/paid-tier-panel";

/** How long a failed paid build is watched for its refund to be recorded. */
const REFUND_WATCH_MS = 3 * 60 * 1000;

// ─── Main Client Page ─────────────────────────────────────────────────────────
interface Props {
  initialGeneration: Generation;
  generationId: string;
}

export function GenerateClientPage({ initialGeneration, generationId }: Props) {
  const [generation, setGeneration] = useState<Generation>(() =>
    isDemoMode && initialGeneration.status !== "success"
      ? { ...initialGeneration, status: "success" }
      : initialGeneration
  );
  const [isPending, startTransition] = useTransition();
  const [retryError, setRetryError] = useState<string | null>(null);

  // ── Status watcher ──────────────────────────────────────────────────────────
  // Polls getGeneration every 3s while the tab is visible (paused when hidden,
  // never stacked) — see useGenerationStatus.
  // Terminal states: redirect/panels are handled by render, we just stop watching.
  const isTerminal = generation.status === "success" || generation.status === "failed";

  // One exception. The Engine writes the failed status first and starts the refund a moment
  // later, so the poll that sees "failed" still sees the payment in place. Stopping there left
  // the page saying a refund was coming and never saying it had started. Keep watching, for a
  // bounded time, until the refund shows up.
  const awaitingRefund =
    generation.status === "failed" && failureOutcome(generation).payment === "refund_expected";
  const [refundWatchOver, setRefundWatchOver] = useState(false);
  useEffect(() => {
    if (!awaitingRefund) return;
    const timer = setTimeout(() => setRefundWatchOver(true), REFUND_WATCH_MS);
    return () => clearTimeout(timer);
  }, [awaitingRefund]);

  useGenerationStatus({
    generationId,
    enabled: !isDemoMode && (!isTerminal || (awaitingRefund && !refundWatchOver)),
    onUpdate: setGeneration,
  });

  // ── Retry handler ──────────────────────────────────────────────────────────
  function handleRetry() {
    setRetryError(null);
    setRefundWatchOver(false);
    startTransition(async () => {
      let result: Awaited<ReturnType<typeof retryGeneration>>;
      try {
        result = await retryGeneration(generationId);
      } catch {
        // The action itself did not answer: offline, or the site is mid-deploy.
        result = { success: false };
      }

      if (result.success) {
        // Reset local state to pending so progress bar reappears
        setGeneration((g) => ({
          ...g,
          status: "pending",
          error_message: null,
        }));
      } else {
        // Without this the spinner stops and nothing else changes: a retry refused
        // for a refunded payment or an expired session looked like a dead button.
        setRetryError(result.error ?? "The retry could not be started. Please try again in a moment.");
      }
    });
  }

  const isFree = isFreeGeneration(generation);
  const isComplete = generation.status === "success";
  const isFailed = generation.status === "failed";
  const isInProgress = !isComplete && !isFailed;

  return (
    <div
      data-testid="generate-page"
      className={`min-h-screen flex flex-col bg-slate-800 ${
        isComplete && isFree ? "h-screen overflow-hidden" : ""
      }`}
    >
      <GenerateHeader
        generation={generation}
        generationId={generationId}
        isComplete={isComplete}
        isFailed={isFailed}
      />

      <main
        className={`flex-1 flex flex-col min-h-0 ${
          isComplete && isFree ? "overflow-hidden" : ""
        }`}
      >
        {isInProgress && <InProgressPanel generation={generation} />}

        {isFailed && (
          <GenerationErrorPanel
            testId="generate-failed-panel"
            generation={generation}
            onRetry={handleRetry}
            isRetrying={isPending}
            retryError={retryError}
          />
        )}

        {isComplete && isFree && <FreeTierPanel generation={generation} />}

        {isComplete && !isFree && <PaidTierPanel generation={generation} />}
      </main>
    </div>
  );
}
