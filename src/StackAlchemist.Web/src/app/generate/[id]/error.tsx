"use client";

export default function GenerationError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-800 px-4">
      <div className="w-full max-w-md rounded-2xl border border-slate-600/30 bg-slate-900/60 p-6 text-center space-y-4">
        <p className="font-mono text-xs tracking-widest uppercase text-slate-500">Generation</p>
        <h1 className="text-2xl font-bold text-white">Build output is unavailable</h1>
        <p className="text-sm text-slate-400">{error.message}</p>
        <button
          type="button"
          onClick={reset}
          className="rounded-full border border-blue-500/40 bg-blue-500/10 px-4 py-2 font-mono text-xs tracking-widest uppercase text-blue-300 hover:bg-blue-500/20 transition-colors"
        >
          Reload
        </button>
      </div>
    </div>
  );
}
