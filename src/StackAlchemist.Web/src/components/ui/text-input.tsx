import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** Shared control padding (also consumed by Select) — two densities, both on-scale. */
export const CONTROL_PAD = {
  md: "px-3 py-2 text-xs",
  sm: "px-2 py-1 text-[0.6875rem]",
} as const;

const base =
  "w-full rounded-control border border-border-strong bg-surface-0/60 font-mono text-ink placeholder:text-ink-faint focus:outline-none focus:border-accent/60";

export function TextInput({
  size = "md",
  className,
  ...props
}: Omit<ComponentProps<"input">, "size"> & { size?: keyof typeof CONTROL_PAD }) {
  return <input className={cn(base, CONTROL_PAD[size], className)} {...props} />;
}
