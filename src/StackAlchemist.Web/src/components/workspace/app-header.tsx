import Link from "next/link";
import Image from "next/image";

/** Static top chrome: brand lockup + optional mode eyebrow. Server component. */
export function AppHeader({ eyebrow }: { eyebrow?: string }) {
  return (
    <header className="flex h-14 items-center gap-4 border-b border-border bg-surface-0/80 px-4 backdrop-blur-md sm:px-6">
      <Link
        href="/"
        className="flex shrink-0 items-center gap-2.5 transition-opacity hover:opacity-80"
      >
        <Image
          src="/logo.svg"
          alt="Stack Alchemist"
          width={28}
          height={28}
          className="drop-shadow-[0_0_6px_rgba(77,166,255,0.4)]"
        />
        <span className="hidden font-mono text-sm font-medium tracking-[0.15em] text-ink sm:block">
          STACK <span className="text-accent">AL</span>CHEMIST
        </span>
      </Link>
      {eyebrow ? (
        <>
          <span className="font-mono text-xs text-ink-faint" aria-hidden>
            /
          </span>
          <span className="font-mono text-[0.6875rem] uppercase tracking-[0.15em] text-ink-muted">
            {eyebrow}
          </span>
        </>
      ) : null}
    </header>
  );
}
