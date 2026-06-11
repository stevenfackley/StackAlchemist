"use client";

import { useEffect, useRef, type RefObject } from "react";

/**
 * Closes a floating element on Escape keypress or pointer-down outside.
 *
 * Pass containerRef=null for ESC-only mode (use when trigger and panel are
 * DOM siblings that can't share a single container ref).
 */
export function useDismissable(
  containerRef: RefObject<HTMLElement | null> | null,
  onClose: () => void,
  enabled = true,
) {
  const onCloseRef = useRef(onClose);
  useEffect(() => {
    onCloseRef.current = onClose;
  });

  useEffect(() => {
    if (!enabled) return;

    function handleKeydown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.stopPropagation();
        onCloseRef.current();
      }
    }

    function handlePointerDown(e: PointerEvent) {
      if (
        containerRef?.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        onCloseRef.current();
      }
    }

    document.addEventListener("keydown", handleKeydown);
    if (containerRef) document.addEventListener("pointerdown", handlePointerDown);
    return () => {
      document.removeEventListener("keydown", handleKeydown);
      if (containerRef) document.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [containerRef, enabled]);
}
