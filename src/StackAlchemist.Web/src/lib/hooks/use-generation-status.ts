"use client";

import { useEffect, useRef } from "react";
import { getGeneration } from "@/lib/actions";
import type { Generation } from "@/lib/types";

/** Poll cadence while a generation is being watched and the tab is visible. */
export const GENERATION_POLL_MS = 3_000;

interface UseGenerationStatusOptions {
  generationId: string | null;
  /** Callers flip this off on terminal status / demo mode. */
  enabled?: boolean;
  /** Latest-ref'd — changing it never restarts the poll. */
  onUpdate: (row: Generation) => void;
  pollMs?: number;
}

/**
 * Watches a generation row by polling the owner-scoped getGeneration server
 * action: one fetch on mount, then every `pollMs` while the tab is visible.
 * Hidden tabs pause; returning to the tab fetches at once and resumes. A slow
 * fetch is never stacked under the next tick.
 *
 * Polling is the only transport (re-platform phase F): generation rows live in
 * qavren-db, which has no change feed.
 */
export function useGenerationStatus({
  generationId,
  enabled = true,
  onUpdate,
  pollMs = GENERATION_POLL_MS,
}: UseGenerationStatusOptions): void {
  const onUpdateRef = useRef(onUpdate);
  useEffect(() => {
    onUpdateRef.current = onUpdate;
  });

  useEffect(() => {
    if (!enabled || !generationId) return;

    let disposed = false;
    let inFlight = false;
    let timer: ReturnType<typeof setInterval> | null = null;

    const fetchLatest = async () => {
      if (inFlight) return;
      inFlight = true;
      try {
        const latest = await getGeneration(generationId);
        if (!disposed && latest) onUpdateRef.current(latest as Generation);
      } catch {
        /* transient fetch error — the next tick recovers */
      } finally {
        inFlight = false;
      }
    };

    const start = () => {
      if (!timer) timer = setInterval(() => void fetchLatest(), pollMs);
    };
    const stop = () => {
      if (timer) {
        clearInterval(timer);
        timer = null;
      }
    };
    const onVisibilityChange = () => {
      if (document.visibilityState === "hidden") {
        stop();
      } else {
        void fetchLatest();
        start();
      }
    };

    document.addEventListener("visibilitychange", onVisibilityChange);
    void fetchLatest();
    if (document.visibilityState !== "hidden") start();

    return () => {
      disposed = true;
      stop();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [generationId, enabled, pollMs]);
}
