"use client";

import { useEffect, useRef, useState } from "react";
import { supabase } from "@/lib/supabase";
import { getGeneration } from "@/lib/actions";
import type { Generation } from "@/lib/types";

export type RealtimeTransport = "realtime" | "polling" | "off";

interface UseGenerationRealtimeOptions {
  generationId: string | null;
  /** Callers flip this off on terminal status / demo mode. */
  enabled?: boolean;
  /** Latest-ref'd — changing it never re-subscribes. */
  onUpdate: (row: Generation) => void;
  /** Fallback poll cadence while realtime is down. */
  pollMs?: number;
}

const RESUBSCRIBE_BASE_MS = 2_000;
const RESUBSCRIBE_MAX_MS = 30_000;

/**
 * Watches a generation row for updates with a resilient transport:
 * Supabase Realtime when healthy, with (1) a one-shot catch-up fetch after every
 * SUBSCRIBED (postgres_changes never replays events missed during the WS
 * handshake — a fast tier-0 build can finish before it completes), (2) capped
 * exponential re-subscribe on CHANNEL_ERROR/TIMED_OUT/CLOSED, and (3) a polling
 * fallback while the channel is down — a JWT expiring mid-build used to kill the
 * subscription silently and strand the UI on a stale status forever.
 *
 * Both catch-up and polling go through the getGeneration server action (not a
 * client-side select): one code path, full row shape, works when the browser
 * client is unconfigured (fail-closed `supabase === null` → polling-only).
 */
export function useGenerationRealtime({
  generationId,
  enabled = true,
  onUpdate,
  pollMs = 30_000,
}: UseGenerationRealtimeOptions): { transport: RealtimeTransport } {
  // Only the realtime⇄polling flips are stateful (they happen in async
  // subscribe callbacks); "off" and the no-client case are pure derivations,
  // which also keeps setState out of the synchronous effect body.
  const [liveTransport, setLiveTransport] = useState<RealtimeTransport>("polling");

  const onUpdateRef = useRef(onUpdate);
  useEffect(() => {
    onUpdateRef.current = onUpdate;
  });

  useEffect(() => {
    if (!enabled || !generationId) return;

    let disposed = false;
    let pollTimer: ReturnType<typeof setInterval> | null = null;
    let resubscribeTimer: ReturnType<typeof setTimeout> | null = null;
    let resubscribeDelay = RESUBSCRIBE_BASE_MS;
    let channel: ReturnType<NonNullable<typeof supabase>["channel"]> | null = null;

    const fetchLatest = async () => {
      try {
        const latest = await getGeneration(generationId);
        if (!disposed && latest) onUpdateRef.current(latest as Generation);
      } catch {
        /* transient fetch error — the next poll/event recovers */
      }
    };

    const startPolling = () => {
      if (disposed || pollTimer) return;
      setLiveTransport("polling");
      pollTimer = setInterval(() => void fetchLatest(), pollMs);
    };

    const stopPolling = () => {
      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }
    };

    // No browser client (unconfigured env): polling is the only transport
    // (reflected by derivation below, no state write needed here).
    if (!supabase) {
      pollTimer = setInterval(() => void fetchLatest(), pollMs);
      void fetchLatest();
      return () => {
        disposed = true;
        stopPolling();
      };
    }

    const client = supabase;

    const connect = () => {
      if (disposed) return;

      channel = client
        .channel(`gen:${generationId}`)
        .on(
          "postgres_changes",
          { event: "UPDATE", schema: "public", table: "generations", filter: `id=eq.${generationId}` },
          (payload) => {
            if (!disposed) onUpdateRef.current(payload.new as Generation);
          },
        )
        .subscribe((status) => {
          if (disposed) return;

          if (status === "SUBSCRIBED") {
            resubscribeDelay = RESUBSCRIBE_BASE_MS;
            stopPolling();
            setLiveTransport("realtime");
            void fetchLatest(); // catch up on anything missed during the handshake
            return;
          }

          if (status === "CHANNEL_ERROR" || status === "TIMED_OUT" || status === "CLOSED") {
            // CLOSED also fires for our own teardown — the disposed guard above
            // keeps cleanup from scheduling a zombie reconnect.
            startPolling();
            if (channel) void client.removeChannel(channel);
            channel = null;
            if (!resubscribeTimer) {
              resubscribeTimer = setTimeout(() => {
                resubscribeTimer = null;
                connect();
              }, resubscribeDelay);
              resubscribeDelay = Math.min(resubscribeDelay * 2, RESUBSCRIBE_MAX_MS);
            }
          }
        });
    };

    connect();

    return () => {
      disposed = true;
      stopPolling();
      if (resubscribeTimer) clearTimeout(resubscribeTimer);
      if (channel) void client.removeChannel(channel);
    };
  }, [generationId, enabled, pollMs]);

  const transport: RealtimeTransport =
    !enabled || !generationId ? "off" : !supabase ? "polling" : liveTransport;

  return { transport };
}
