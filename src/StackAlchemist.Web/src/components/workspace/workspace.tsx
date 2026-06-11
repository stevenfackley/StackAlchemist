import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Primary work surface: the one vertical scroll region, capped at a readable
 * measure so content stops sprawling edge-to-edge ("container creep"), with a
 * single padding rhythm.
 */
export function Workspace({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={cn("min-h-0 flex-1 overflow-y-auto", className)}>
      <div className="mx-auto w-full max-w-3xl px-5 py-6 sm:px-8">{children}</div>
    </section>
  );
}
