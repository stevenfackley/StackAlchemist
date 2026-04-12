"use client";

import { useEffect } from "react";
import Link from "next/link";

export default function GlobalError({
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
    <div className="min-h-screen bg-slate-800 flex flex-col items-center justify-center px-4">
      <div className="w-full max-w-md text-center space-y-4">
        <p className="font-mono text-xs tracking-[0.3em] text-rose-400 uppercase">Application Error</p>
        <h1 className="text-3xl font-bold text-white">Something went wrong</h1>
        <p className="text-slate-400 text-sm leading-relaxed">
          The page failed to render cleanly. This usually means a route reached a server-only code path.
        </p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <button
            onClick={reset}
            className="rounded-full border border-slate-600/50 text-slate-300 hover:border-blue-500/40 hover:text-blue-400 px-5 py-2.5 text-sm transition-colors"
          >
            Try again
          </button>
          <Link
            href="/"
            className="rounded-full bg-blue-500 text-white px-5 py-2.5 text-sm hover:bg-blue-400 transition-colors"
          >
            Go home
          </Link>
        </div>
      </div>
    </div>
  );
}
