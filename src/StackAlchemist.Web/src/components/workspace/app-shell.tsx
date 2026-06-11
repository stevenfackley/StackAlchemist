import type { ReactNode } from "react";

/**
 * Full-viewport application frame: a fixed header row over a single
 * scroll-bounded body row (`min-h-0` so descendants own their own overflow).
 * Generic chrome — composed by /advanced, and reusable by /simple, /dashboard.
 */
export function AppShell({
  header,
  children,
}: {
  header: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="grid h-dvh grid-rows-[auto_1fr] overflow-hidden bg-surface-1 font-sans text-ink antialiased">
      {header}
      <div className="min-h-0">{children}</div>
    </div>
  );
}
