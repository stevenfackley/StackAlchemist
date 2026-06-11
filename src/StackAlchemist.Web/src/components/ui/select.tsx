import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";
import { CONTROL_PAD } from "./text-input";

const base =
  "rounded-control border border-border-strong bg-surface-0/60 font-mono text-ink focus:outline-none focus:border-accent/60";

export function Select({
  size = "md",
  className,
  ...props
}: Omit<ComponentProps<"select">, "size"> & { size?: keyof typeof CONTROL_PAD }) {
  return <select className={cn(base, CONTROL_PAD[size], className)} {...props} />;
}
