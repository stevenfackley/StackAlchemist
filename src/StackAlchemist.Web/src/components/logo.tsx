import Link from "next/link";
import Image from "next/image";
import { cn } from "@/lib/utils";

export type LogoVariant = "wordmark" | "mono";

/**
 * Per-variant lockup styling. "wordmark" is the sans-serif brand mark used on
 * marketing surfaces (ContentHeader, etc.); "mono" is the compact monospace
 * lockup used across the app-flow/workspace chrome (AppHeader and friends).
 */
const VARIANT_STYLES: Record<LogoVariant, { gap: string; shadow: string; text: string }> = {
  wordmark: {
    gap: "gap-3",
    shadow: "drop-shadow-[0_0_10px_rgba(59,152,255,0.5)]",
    text: "text-sm font-semibold tracking-[0.18em] text-slate-100 uppercase",
  },
  mono: {
    gap: "gap-2.5",
    shadow: "drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]",
    text: "font-mono text-sm font-medium tracking-widest text-slate-200",
  },
};

export function Logo({
  className,
  variant = "wordmark",
  size = 34,
  hideWordmarkOnMobile = false,
}: {
  className?: string;
  /** "wordmark" (default, marketing) or "mono" (app-flow/workspace chrome). */
  variant?: LogoVariant;
  /** Logo mark (image) size in px. */
  size?: number;
  /** Hide the text wordmark below the `sm` breakpoint, keeping just the mark. */
  hideWordmarkOnMobile?: boolean;
}) {
  const styles = VARIANT_STYLES[variant];

  return (
    <Link
      href="/"
      className={cn("flex items-center transition-opacity hover:opacity-80", styles.gap, className)}
    >
      <Image
        src="/logo.svg"
        alt="Stack Alchemist"
        width={size}
        height={size}
        className={styles.shadow}
        // Only the wordmark (marketing) lockup opts in to eager loading, matching
        // the prior per-callsite markup where `priority` was set only there.
        priority={variant === "wordmark" ? true : undefined}
      />
      <span className={cn(styles.text, hideWordmarkOnMobile && "hidden sm:block")}>
        {variant === "mono" ? (
          <>STACK <span className="text-accent">AL</span>CHEMIST</>
        ) : (
          <>Stack&nbsp;&nbsp;<span className="text-accent">Al</span>chemist</>
        )}
      </span>
    </Link>
  );
}
