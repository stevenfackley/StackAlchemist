"use client";

import { useState, useTransition } from "react";
import { retryGeneration } from "@/lib/actions";
import { useGenerationRealtime } from "@/lib/hooks/use-generation-realtime";
import type { Generation } from "@/lib/types";
import { isDemoMode } from "@/lib/runtime-config";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import { isFreeGeneration } from "./components/status";
import { GenerateHeader } from "./components/generate-header";
import { InProgressPanel } from "./components/in-progress-panel";
import { FreeTierPanel } from "./components/free-tier-panel";
import { PaidTierPanel } from "./components/paid-tier-panel";

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

  // ── Realtime watcher ────────────────────────────────────────────────────────
  // Resilient transport (catch-up fetch on subscribe, re-subscribe on channel
  // errors, polling fallback while down) — replaces the old subscription that
  // had no error handling AND the unconditional 5s poll that hammered the
  // server action throughout every healthy build.
  // Terminal states: redirect/panels are handled by render, we just stop watching.
  const isTerminal = generation.status === "success" || generation.status === "failed";
  useGenerationRealtime({
    generationId,
    enabled: !isDemoMode && !isTerminal,
    onUpdate: setGeneration,
  });

  // ── Retry handler ──────────────────────────────────────────────────────────
  function handleRetry() {
    startTransition(async () => {
      const result = await retryGeneration(generationId);
      if (result.success) {
        // Reset local state to pending so progress bar reappears
        setGeneration((g) => ({
          ...g,
          status: "pending",
          error_message: null,
        }));
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
          />
        )}

        {isComplete && isFree && <FreeTierPanel generation={generation} />}

        {isComplete && !isFree && <PaidTierPanel generation={generation} />}
      </main>
    </div>
  );
}
