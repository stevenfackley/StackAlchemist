"use client";

import { useState, useTransition } from "react";
import { retryGeneration } from "@/lib/actions";
import { useGenerationStatus } from "@/lib/hooks/use-generation-status";
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

  // ── Status watcher ──────────────────────────────────────────────────────────
  // Polls getGeneration every 3s while the tab is visible (paused when hidden,
  // never stacked) — see useGenerationStatus.
  // Terminal states: redirect/panels are handled by render, we just stop watching.
  const isTerminal = generation.status === "success" || generation.status === "failed";
  useGenerationStatus({
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
