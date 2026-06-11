import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** The single label token: 11px mono, uppercase, wide tracking, faint ink. */
const LABEL = "font-mono text-[0.6875rem] uppercase tracking-[0.15em] text-ink-faint";

export function Label({ className, ...props }: ComponentProps<"label">) {
  return <label className={cn(LABEL, className)} {...props} />;
}

/** Non-form section caption — same token, rendered as a paragraph. */
export function Eyebrow({ className, ...props }: ComponentProps<"p">) {
  return <p className={cn(LABEL, className)} {...props} />;
}
