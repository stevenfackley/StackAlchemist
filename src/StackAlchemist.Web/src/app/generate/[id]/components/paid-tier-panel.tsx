"use client";

import Link from "next/link";
import { CheckCircle2, Download, Loader2 } from "lucide-react";
import type { Generation } from "@/lib/types";
import { TIER_NAMES } from "./status";

export function PaidTierPanel({ generation }: { generation: Generation }) {
  const tierName = TIER_NAMES[generation.tier] ?? "Unknown";

  return (
    <div data-testid="generate-paid-tier-panel" className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
      {/* Success icon */}
      <div className="relative">
        <div className="h-20 w-20 rounded-full bg-emerald-500/10 border-2 border-emerald-500/40 flex items-center justify-center">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
        </div>
        <div className="absolute -top-1 -right-1 h-6 w-6 rounded-full bg-blue-500 border-2 border-slate-800 flex items-center justify-center">
          <span className="font-mono text-[10px] text-white font-bold">{generation.tier}</span>
        </div>
      </div>

      {/* Heading */}
      <div className="text-center space-y-2 max-w-lg">
        <h2 className="text-2xl font-bold text-white tracking-tight">
          Your {tierName} is Ready
        </h2>
        <p className="text-slate-400 text-sm leading-relaxed">
          {generation.tier === 1
            ? "Your schema, API spec, and SQL scripts are packaged and ready to download."
            : generation.tier === 2
            ? "Your full-stack codebase has been compiled and verified. Download, run, and ship."
            : "Your production-ready stack — code + infrastructure + runbook — is packaged and verified."}
        </p>
      </div>

      {/* Info card */}
      <div className="w-full max-w-md rounded-xl border border-slate-600/30 bg-slate-700/20 divide-y divide-slate-700/40">
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Tier</span>
          <span className="font-mono text-xs text-blue-400 font-bold">{tierName}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Mode</span>
          <span className="font-mono text-xs text-white capitalize">{generation.mode}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Status</span>
          <span className="font-mono text-xs text-emerald-400 flex items-center gap-1.5">
            <CheckCircle2 className="h-3 w-3" /> Compile Verified
          </span>
        </div>
        {generation.completed_at && (
          <div className="px-5 py-3 flex items-center justify-between">
            <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Completed</span>
            <span className="font-mono text-xs text-white">
              {new Date(generation.completed_at).toLocaleString()}
            </span>
          </div>
        )}
      </div>

      {/* Download button */}
      {generation.download_url ? (
        <div className="flex flex-col items-center gap-3">
          <a
            href={generation.download_url}
            download
            className="flex items-center gap-3 rounded-full bg-emerald-500 hover:bg-emerald-400 text-white px-8 py-3.5 text-sm font-medium hover:shadow-[0_0_24px_rgba(16,185,129,0.35)] transition-all duration-300"
          >
            <Download className="h-5 w-5" />
            Download {tierName} Package
          </a>
          <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">
            Link expires in 168 hours (7 days)
          </p>
        </div>
      ) : (
        <div className="text-center space-y-2">
          <div className="flex items-center justify-center gap-2 text-slate-400">
            <Loader2 className="h-4 w-4 animate-spin" />
            <span className="font-mono text-xs">Preparing download link...</span>
          </div>
        </div>
      )}

      {/* Start over */}
      <div className="pt-4 border-t border-slate-700/50 w-full max-w-md text-center">
        <Link
          href="/"
          className="font-mono text-xs text-slate-500 hover:text-blue-400 transition-colors tracking-widest uppercase"
        >
          &larr; Generate Another
        </Link>
      </div>
    </div>
  );
}
