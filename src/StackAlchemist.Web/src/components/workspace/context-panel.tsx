"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Deferred reference surface (e.g. the live ER diagram). A persistent side
 * column on wide viewports; below `xl` it collapses to a Show/Hide drawer so it
 * never competes with the primary workspace on small screens.
 *
 * The `xl:` overrides come after the base utilities in the generated sheet, so
 * the panel is always expanded at `xl` regardless of toggle state — no
 * `important` needed (same ordering trick the legacy preview used).
 */
export function ContextPanel({
  title,
  action,
  children,
  defaultOpen = false,
  className,
}: {
  title: string;
  action?: ReactNode;
  children: ReactNode;
  defaultOpen?: boolean;
  className?: string;
}) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <aside
      className={cn(
        "flex min-h-0 flex-col border-t border-border bg-surface-0/40 xl:border-l xl:border-t-0",
        className,
      )}
    >
      <div className="flex shrink-0 items-center justify-between gap-3 border-b border-border bg-surface-0/60 px-4 py-2">
        <p className="font-mono text-[0.6875rem] uppercase tracking-[0.15em] text-ink-faint">
          {title}
        </p>
        <div className="flex items-center gap-2">
          {action}
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            className="rounded-full border border-border-strong px-3 py-1 font-mono text-[0.625rem] uppercase tracking-[0.15em] text-ink-muted transition-colors hover:border-accent/40 hover:text-accent xl:hidden"
          >
            {open ? "Hide" : "Show"}
          </button>
        </div>
      </div>
      <div
        className={cn(
          "min-h-0 overflow-hidden transition-all duration-300 xl:max-h-none xl:flex-1 xl:opacity-100",
          open ? "max-h-[60vh] opacity-100" : "max-h-0 opacity-0",
        )}
      >
        {children}
      </div>
    </aside>
  );
}
