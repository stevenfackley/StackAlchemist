"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { supabase } from "@/lib/supabase";

const MIN_REFRESH_MS = 5_000; // throttle — build_log updates storm the channel

export function GenerationsLiveRefresher({ userId }: { userId: string }) {
  const router = useRouter();
  const lastRef = useRef(0);

  function tryRefresh() {
    const now = Date.now();
    if (now - lastRef.current >= MIN_REFRESH_MS) {
      lastRef.current = now;
      router.refresh();
    }
  }

  useEffect(() => {
    if (!supabase) return;

    function onVisible() {
      if (document.visibilityState === "visible") tryRefresh();
    }
    document.addEventListener("visibilitychange", onVisible);

    const channel = supabase
      .channel(`gen-feed:${userId}`)
      .on(
        "postgres_changes",
        {
          event: "*",
          schema: "public",
          table: "generations",
          filter: `user_id=eq.${userId}`,
        },
        (payload) => {
          // Client-side guard in case the server filter is not enforced (test site).
          const row = payload.new as Record<string, unknown> | undefined;
          if (row && "user_id" in row && row.user_id !== userId) return;
          tryRefresh();
        }
      )
      .subscribe();

    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      void supabase?.removeChannel(channel);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  return null;
}
