"use client";

import { useState } from "react";
import { ChevronLeft } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Vertical, collapsible wizard navigation. Replaces the horizontal header
 * stepper so "where am I / jump to step" is its own chunk, not a strip wedged
 * beside the logo.
 *
 * Test contract: the nav carries `testId` (default "advanced-stepper") and each
 * button carries `${testId}-button-${n}` (1-based) — the exact ids the deploy
 * smoke specs click. Buttons stay rendered and visible in both states.
 */
export function StepRail({
  steps,
  current,
  onSelect,
  testId = "advanced-stepper",
}: {
  steps: string[];
  current: number;
  onSelect: (step: number) => void;
  testId?: string;
}) {
  const [open, setOpen] = useState(true);

  return (
    <nav
      data-testid={testId}
      aria-label="Wizard steps"
      className={cn(
        "flex shrink-0 flex-col gap-1 overflow-hidden border-r border-border bg-surface-0 p-3 transition-[width] duration-300",
        open ? "w-16 lg:w-60" : "w-16",
      )}
    >
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-label={open ? "Collapse steps" : "Expand steps"}
        className="mb-1 hidden h-8 shrink-0 items-center justify-end rounded-control px-2 text-ink-faint transition-colors hover:text-accent lg:flex"
      >
        <ChevronLeft className={cn("h-4 w-4 transition-transform", !open && "rotate-180")} />
      </button>

      {steps.map((label, i) => {
        const n = i + 1;
        const active = current === n;
        const done = current > n;
        return (
          <button
            key={label}
            type="button"
            onClick={() => onSelect(n)}
            data-testid={`${testId}-button-${n}`}
            aria-current={active ? "step" : undefined}
            title={label}
            className={cn(
              "flex items-center gap-3 whitespace-nowrap rounded-control px-2 py-2 text-left font-mono text-xs uppercase tracking-[0.1em] transition-colors",
              active
                ? "bg-accent/10 text-accent"
                : done
                  ? "text-success hover:bg-surface-2/40"
                  : "text-ink-faint hover:bg-surface-2/40 hover:text-ink-muted",
            )}
          >
            <span
              className={cn(
                "flex h-6 w-6 shrink-0 items-center justify-center rounded-full border text-[0.6875rem]",
                active
                  ? "border-accent text-accent"
                  : done
                    ? "border-success text-success"
                    : "border-border-strong text-ink-faint",
              )}
            >
              {done ? "✓" : n}
            </span>
            <span className={cn("transition-opacity opacity-0", open && "lg:opacity-100")}>
              {label}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
