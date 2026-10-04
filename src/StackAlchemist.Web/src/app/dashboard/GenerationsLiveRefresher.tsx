"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";

/** Re-render cadence while at least one of the user's generations is in progress. */
export const DASHBOARD_REFRESH_MS = 10_000;

/**
 * Null-rendering island that keeps the server-rendered dashboard current:
 * `router.refresh()` every 10s while `active` (a build is in progress) and the
 * tab is visible, plus once on every return to the tab (a build may have been
 * started elsewhere). The server component stays the source of truth for rows;
 * when the last build finishes, the refreshed page passes `active={false}` and
 * the timer stops.
 */
export function GenerationsLiveRefresher({ active }: { active: boolean }) {
  const router = useRouter();
  const routerRef = useRef(router);
  useEffect(() => {
    routerRef.current = router;
  });

  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | null = null;
    const refresh = () => routerRef.current.refresh();
    const start = () => {
      if (active && !timer) timer = setInterval(refresh, DASHBOARD_REFRESH_MS);
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
        refresh();
        start();
      }
    };

    document.addEventListener("visibilitychange", onVisibilityChange);
    if (document.visibilityState !== "hidden") start();

    return () => {
      stop();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [active]);

  return null;
}
