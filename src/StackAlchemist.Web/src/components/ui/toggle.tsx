import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** Inline checkbox + label (e.g. the PK toggle). */
export function Toggle({
  label,
  className,
  ...props
}: Omit<ComponentProps<"input">, "type"> & { label: string }) {
  return (
    <label className={cn("flex cursor-pointer items-center gap-1", className)}>
      <input type="checkbox" className="accent-accent" {...props} />
      <span className="font-mono text-[0.6875rem] uppercase tracking-[0.1em] text-ink-faint">
        {label}
      </span>
    </label>
  );
}
