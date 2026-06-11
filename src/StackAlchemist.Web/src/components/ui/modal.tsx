"use client";

import {
  useCallback,
  useEffect,
  useId,
  useRef,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Accessible modal shell: portal, role=dialog/aria-modal, focus trap with focus
 * restore, Escape + backdrop-mousedown close, and counter-based body scroll lock
 * (stacked modals don't fight over document.body.style.overflow). No Radix —
 * the app deliberately avoids a dialog dependency.
 */

let scrollLockCount = 0;

function lockBodyScroll() {
  if (scrollLockCount === 0) document.body.style.overflow = "hidden";
  scrollLockCount++;
}

function unlockBodyScroll() {
  scrollLockCount = Math.max(0, scrollLockCount - 1);
  if (scrollLockCount === 0) document.body.style.overflow = "";
}

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), iframe, [tabindex]:not([tabindex="-1"])';

export interface ModalProps {
  onClose: () => void;
  /** Rendered in the built-in header with a close button; omit for fully custom content. */
  title?: ReactNode;
  /** Point at your own heading id when not using `title`. */
  labelledById?: string;
  size?: "md" | "lg";
  /** overlay = z-overlay (e.g. upgrade prompt); modal = z-modal (top-level flows). */
  zLayer?: "overlay" | "modal";
  testId?: string;
  footer?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Modal({
  onClose,
  title,
  labelledById,
  size = "md",
  zLayer = "modal",
  testId,
  footer,
  children,
  className,
}: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  // Latest-callback ref so the mount-once keydown listener never goes stale;
  // assigned in an effect (not render) per react-hooks/refs.
  const onCloseRef = useRef(onClose);
  useEffect(() => {
    onCloseRef.current = onClose;
  });

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    lockBodyScroll();
    panelRef.current?.focus();

    const handleKeydown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onCloseRef.current();
        return;
      }
      if (e.key !== "Tab" || !panelRef.current) return;

      const focusables = panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
      if (focusables.length === 0) {
        e.preventDefault();
        return;
      }
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement;

      if (e.shiftKey && (active === first || active === panelRef.current)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeydown);
    return () => {
      document.removeEventListener("keydown", handleKeydown);
      unlockBodyScroll();
      opener?.focus();
    };
  }, []);

  // mousedown (not click) on the overlay itself: a drag that starts inside an
  // input and ends outside must not dismiss the modal.
  const handleBackdropMouseDown = useCallback((e: ReactMouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) onCloseRef.current();
  }, []);

  const modal = (
    <div
      className={cn(
        "fixed inset-0 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm",
        zLayer === "modal" ? "z-modal" : "z-overlay",
      )}
      onMouseDown={handleBackdropMouseDown}
      data-testid={testId ? `${testId}-backdrop` : undefined}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={title ? titleId : labelledById}
        tabIndex={-1}
        data-testid={testId}
        className={cn(
          "flex max-h-[90vh] w-full flex-col rounded-2xl border border-border bg-surface-1 shadow-2xl outline-none",
          size === "lg" ? "max-w-2xl" : "max-w-md",
          className,
        )}
      >
        {title !== undefined && (
          <div className="flex items-start justify-between gap-4 border-b border-border px-6 py-4">
            <h2 id={titleId} className="font-mono text-sm font-semibold uppercase tracking-[0.15em] text-ink">
              {title}
            </h2>
            <button
              type="button"
              aria-label="Close"
              onClick={onClose}
              className="rounded-full p-1 text-ink-faint transition-colors hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60"
            >
              <X aria-hidden className="h-4 w-4" />
            </button>
          </div>
        )}
        <div className="min-h-0 flex-1 overflow-y-auto px-6 py-4">{children}</div>
        {footer !== undefined && (
          <div className="border-t border-border px-6 py-4">{footer}</div>
        )}
      </div>
    </div>
  );

  return createPortal(modal, document.body);
}
