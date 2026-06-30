import type { ReactNode } from "react";

/**
 * Shared page-title header for the content surfaces that all render an
 * identical eyebrow + h1 block: /blog, /solutions, /compare, /contact,
 * /terms and /privacy.
 *
 * This is distinct from {@link ./content-header.tsx ContentHeader}, which is
 * the sticky top navigation bar. This component is the in-`<article>` heading
 * block that previously lived inline (and duplicated) on each of those pages.
 *
 * The description/subtitle is intentionally passed as `children` rather than a
 * styled prop: the surfaces use three different subtitle treatments (slate-400,
 * slate-300, and a mono "Last updated" line), so rendering the caller's node
 * verbatim keeps the output byte-for-byte identical to the previous inline
 * markup. `className` overrides the wrapper margin (legal pages use `mb-12`).
 */
export interface PageHeaderProps {
  /** Small uppercase mono label rendered above the title. */
  eyebrow: string;
  /** Main heading. Accepts rich nodes for pages that need inline markup. */
  title: ReactNode;
  /** Optional subtitle/description block, rendered as-is below the title. */
  children?: ReactNode;
  /** Overrides the `<header>` wrapper classes. Defaults to `mb-14`. */
  className?: string;
}

export function PageHeader({
  eyebrow,
  title,
  children,
  className = "mb-14",
}: PageHeaderProps) {
  return (
    <header className={className}>
      <span className="font-mono text-[10px] tracking-[0.3em] uppercase text-electric">
        {eyebrow}
      </span>
      <h1 className="mt-3 text-3xl sm:text-4xl font-bold text-white tracking-tight">
        {title}
      </h1>
      {children}
    </header>
  );
}
