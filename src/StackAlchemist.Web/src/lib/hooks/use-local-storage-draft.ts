"use client";

import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from "react";

interface DraftEnvelope<T> {
  v: number;
  savedAt: number;
  data: T;
}

interface UseLocalStorageDraftOptions<T> {
  /** Envelope version — bump on shape changes; mismatched drafts are discarded. */
  version: number;
  /** Drafts older than this are discarded on restore. Default 7 days. */
  ttlMs?: number;
  /** Debounce between state change and storage write. Default 800ms. */
  debounceMs?: number;
  /** Skip persisting pristine state so empty visits don't litter storage. */
  isDefault?: (value: T) => boolean;
  /**
   * Adjust the restored draft before it is applied — e.g. re-impose explicit
   * URL params over drafted values (deep links must beat stale drafts).
   */
  transformOnRestore?: (data: T) => T;
  enabled?: boolean;
}

export interface LocalStorageDraft<T> {
  value: T;
  setValue: Dispatch<SetStateAction<T>>;
  /** A stored draft was applied after mount. */
  restored: boolean;
  restoredAt: Date | null;
  /** restored && not yet dismissed — drive a "draft restored" notice off this. */
  noticeVisible: boolean;
  dismissNotice: () => void;
  /** Remove the stored draft and stop showing the notice (state is untouched). */
  clearDraft: () => void;
}

const DEFAULT_TTL_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * useState with a debounced localStorage mirror, restored on mount.
 *
 * Design constraints this encodes:
 * - Restore happens in an effect, NOT lazy state init: these pages are
 *   SSR-prerendered, and reading localStorage during the first render would
 *   produce a hydration mismatch. The one post-mount re-render is the cost,
 *   and the `restored` flag makes it legible to the user.
 * - The debounce buffer flushes on pagehide/visibilitychange(hidden): every
 *   terminal navigation in this app is a hard window.location.href (COOP
 *   isolation for StackBlitz), so an unflushed buffer would be lost exactly
 *   when the user finishes the flow.
 * - All storage access is try/caught: quota errors, disabled storage, and
 *   private-browsing must degrade to plain in-memory state, never throw.
 */
export function useLocalStorageDraft<T>(
  key: string,
  defaultValue: T | (() => T),
  options: UseLocalStorageDraftOptions<T>,
): LocalStorageDraft<T> {
  const { version, ttlMs = DEFAULT_TTL_MS, debounceMs = 800, isDefault, transformOnRestore, enabled = true } = options;

  const [value, setValue] = useState<T>(defaultValue);
  const [restoredAt, setRestoredAt] = useState<Date | null>(null);
  const [noticeVisible, setNoticeVisible] = useState(false);

  // Latest snapshot for the synchronous pagehide flush.
  const latestRef = useRef<{ value: T; dirty: boolean }>({ value, dirty: false });
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const restoredOnceRef = useRef(false);

  const writeNow = useCallback(
    (v: T) => {
      try {
        const envelope: DraftEnvelope<T> = { v: version, savedAt: Date.now(), data: v };
        window.localStorage.setItem(key, JSON.stringify(envelope));
      } catch {
        /* storage unavailable/full — in-memory state still works */
      }
    },
    [key, version],
  );

  // ── Restore on mount ───────────────────────────────────────────────────────
  useEffect(() => {
    if (!enabled || restoredOnceRef.current) return;
    restoredOnceRef.current = true;

    let cancelled = false;
    try {
      const raw = window.localStorage.getItem(key);
      if (!raw) return;

      const envelope = JSON.parse(raw) as DraftEnvelope<T>;
      const expired = Date.now() - envelope.savedAt > ttlMs;
      if (envelope.v !== version || expired) {
        window.localStorage.removeItem(key);
        return;
      }

      const data = transformOnRestore ? transformOnRestore(envelope.data) : envelope.data;
      // Microtask: applying the draft is a one-shot post-hydration update, not
      // part of this render pass (also keeps setState out of the sync effect body).
      queueMicrotask(() => {
        if (cancelled) return;
        setValue(data);
        setRestoredAt(new Date(envelope.savedAt));
        setNoticeVisible(true);
      });
    } catch {
      /* corrupt entry — treat as absent */
      try {
        window.localStorage.removeItem(key);
      } catch {
        /* ignore */
      }
    }
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one-shot restore
  }, [enabled, key]);

  // ── Debounced persist + flush on page teardown ─────────────────────────────
  useEffect(() => {
    latestRef.current = { value, dirty: true };
    if (!enabled) return;
    if (isDefault?.(value)) return; // don't litter storage with pristine state

    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      debounceRef.current = null;
      latestRef.current.dirty = false;
      writeNow(latestRef.current.value);
    }, debounceMs);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
        debounceRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- isDefault intentionally unbound
  }, [value, enabled, debounceMs, writeNow]);

  useEffect(() => {
    if (!enabled) return;

    const flush = () => {
      if (!latestRef.current.dirty) return;
      if (isDefault?.(latestRef.current.value)) return;
      latestRef.current.dirty = false;
      writeNow(latestRef.current.value);
    };
    const onVisibility = () => {
      if (document.visibilityState === "hidden") flush();
    };

    window.addEventListener("pagehide", flush);
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      window.removeEventListener("pagehide", flush);
      document.removeEventListener("visibilitychange", onVisibility);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- isDefault intentionally unbound
  }, [enabled, writeNow]);

  const dismissNotice = useCallback(() => setNoticeVisible(false), []);

  const clearDraft = useCallback(() => {
    try {
      window.localStorage.removeItem(key);
    } catch {
      /* ignore */
    }
    // The just-cleared value must not be re-persisted by the pending debounce.
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
      debounceRef.current = null;
    }
    latestRef.current.dirty = false;
    setNoticeVisible(false);
  }, [key]);

  return { value, setValue, restored: restoredAt !== null, restoredAt, noticeVisible, dismissNotice, clearDraft };
}
