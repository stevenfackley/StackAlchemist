"use client";

import Link from "next/link";
import { CheckCircle2, Download, Loader2, MinusCircle, XCircle } from "lucide-react";
import type { Generation } from "@/lib/types";
import { parseBuildReportSummary, type BuildHalfStatus } from "@/lib/build-report";
import { TIER_NAMES } from "./status";

/**
 * Tier 1 (Blueprint) is schema + API docs. Nothing in it is compiled, so it is not sold
 * under the Compile Guarantee and must never be badged as verified.
 */
const COMPILE_GUARANTEED_TIERS = [2, 3];

const HALF_ICONS: Record<BuildHalfStatus, typeof CheckCircle2> = {
  passed: CheckCircle2,
  failed: XCircle,
  skipped: MinusCircle,
  not_run: MinusCircle,
};

const HALF_STYLES: Record<BuildHalfStatus, string> = {
  passed: "text-emerald-400",
  failed: "text-red-400",
  skipped: "text-slate-500",
  not_run: "text-slate-500",
};

const HALF_LABELS: Record<BuildHalfStatus, string> = {
  passed: "compiled",
  failed: "failed to compile",
  skipped: "not included",
  not_run: "not built",
};

export function PaidTierPanel({ generation }: { generation: Generation }) {
  const tierName = TIER_NAMES[generation.tier] ?? "Unknown";

  // The badge is driven by what the compile worker actually recorded, not by the fact that
  // a row reached "success". It used to read "Compile Verified" unconditionally — on
  // Blueprints that are never compiled, and on archives whose Next.js half was never built.
  const recordedVerdict = COMPILE_GUARANTEED_TIERS.includes(generation.tier)
    ? parseBuildReportSummary(generation.build_log)
    : null;

  // A verdict that names no halves states nothing about what compiled, so it is not one:
  // showing "compiled and verified" over an empty badge row would be the same unearned claim
  // in a new shape. Halves are per stack (.NET/Next.js, FastAPI/React) and come from the
  // report — the panel never assumes which stack it is looking at.
  const buildReport = recordedVerdict && recordedVerdict.halves.length > 0 ? recordedVerdict : null;

  // Say "verified" only where the recorded verdict says so; otherwise say what is true —
  // the archive is packaged and downloadable.
  const verifiedBlurb = (verified: string, unverified: string) =>
    buildReport?.status === "verified" ? verified : unverified;

  return (
    <div data-testid="generate-paid-tier-panel" className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
      {/* Success icon */}
      <div className="relative">
        <div className="h-20 w-20 rounded-full bg-emerald-500/10 border-2 border-emerald-500/40 flex items-center justify-center">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
        </div>
        <div className="absolute -top-1 -right-1 h-6 w-6 rounded-full bg-accent border-2 border-slate-800 flex items-center justify-center">
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
            ? verifiedBlurb(
                "Your full-stack codebase has been compiled and verified. Download, run, and ship.",
                "Your full-stack codebase is packaged and ready to download.",
              )
            : verifiedBlurb(
                "Your production-ready stack — code + infrastructure + runbook — is packaged and verified.",
                "Your production-ready stack — code + infrastructure + runbook — is packaged and ready to download.",
              )}
        </p>
      </div>

      {/* Info card */}
      <div className="w-full max-w-md rounded-xl border border-slate-600/30 bg-slate-700/20 divide-y divide-slate-700/40">
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Tier</span>
          <span className="font-mono text-xs text-accent font-bold">{tierName}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Mode</span>
          <span className="font-mono text-xs text-white capitalize">{generation.mode}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between gap-4">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">
            {buildReport ? "Compiles" : "Status"}
          </span>
          {buildReport ? (
            <span data-testid="compile-verdict" className="flex items-center gap-3 flex-wrap justify-end">
              {buildReport.halves.map((half) => {
                const Icon = HALF_ICONS[half.status];
                return (
                  <span
                    key={half.half}
                    title={`${half.label} ${HALF_LABELS[half.status]}`}
                    className={`font-mono text-xs flex items-center gap-1.5 ${HALF_STYLES[half.status]}`}
                  >
                    <Icon className="h-3 w-3" aria-hidden="true" />
                    {half.label}
                    <span className="sr-only"> {HALF_LABELS[half.status]}</span>
                  </span>
                );
              })}
            </span>
          ) : (
            <span data-testid="compile-verdict" className="font-mono text-xs text-slate-400">
              {COMPILE_GUARANTEED_TIERS.includes(generation.tier)
                ? "Build record unavailable"
                : "Packaged"}
            </span>
          )}
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
          className="font-mono text-xs text-slate-500 hover:text-accent transition-colors tracking-widest uppercase"
        >
          &larr; Generate Another
        </Link>
      </div>
    </div>
  );
}
