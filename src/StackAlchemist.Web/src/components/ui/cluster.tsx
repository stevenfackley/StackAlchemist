import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";
import { GAP, type Gap } from "./spacing";

const ALIGN = {
  start: "items-start",
  center: "items-center",
  end: "items-end",
} as const;

/** Horizontal wrap with a fixed-scale gap. Replaces ad-hoc `flex gap-* flex-wrap`. */
export function Cluster({
  gap = "sm",
  align = "center",
  className,
  ...props
}: ComponentProps<"div"> & { gap?: Gap; align?: keyof typeof ALIGN }) {
  return (
    <div className={cn("flex flex-wrap", GAP[gap], ALIGN[align], className)} {...props} />
  );
}
