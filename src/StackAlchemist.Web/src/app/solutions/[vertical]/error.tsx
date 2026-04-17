"use client";

import { useEffect } from "react";
import Link from "next/link";

export default function SolutionPageError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="min-h-screen bg-void flex flex-col items-center justify-center px-4 text-center">
      <p className="font-mono text-xs tracking-[0.3em] text-rose-400 uppercase mb-4">Error</p>
      <h1 className="font-mono text-2xl font-bold text-white mb-3">Couldn&apos;t load this solution</h1>
      <p className="text-slate-400 text-sm mb-8 max-w-md leading-relaxed">
        Something went wrong rendering this solution page. Try again or see all solutions.
      </p>
      <div className="flex gap-3">
        <button
          onClick={reset}
          className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-5 py-2.5 uppercase tracking-widest transition-colors"
        >
          Try again
        </button>
        <Link
          href="/solutions"
          className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-5 py-2.5 uppercase tracking-widest transition-colors"
        >
          All solutions
        </Link>
      </div>
    </div>
  );
}
