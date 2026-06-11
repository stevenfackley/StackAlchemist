import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";
import { GAP, type Gap } from "./spacing";

/** Vertical flow with a fixed-scale gap. Replaces ad-hoc `space-y-*`. */
export function Stack({
  gap = "md",
  className,
  ...props
}: ComponentProps<"div"> & { gap?: Gap }) {
  return <div className={cn("flex flex-col", GAP[gap], className)} {...props} />;
}
