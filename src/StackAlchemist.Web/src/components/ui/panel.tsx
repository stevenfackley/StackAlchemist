import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

const PAD = { md: "p-4", lg: "p-6" } as const;

/** Card surface: one radius (panel), one border, two padding options. */
export function Panel({
  padding = "md",
  className,
  ...props
}: ComponentProps<"div"> & { padding?: keyof typeof PAD }) {
  return (
    <div
      className={cn(
        "rounded-panel border border-border bg-surface-2/30",
        PAD[padding],
        className,
      )}
      {...props}
    />
  );
}
