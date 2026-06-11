"use client";

import { Loader2 } from "lucide-react";
import type { Generation } from "@/lib/types";
import { BuildLogConsole } from "@/components/build-log-console";
import { SectionErrorBoundary } from "@/components/error-boundary";
import { STATUS_LABELS, STATUS_DESCRIPTIONS, TIER_NAMES, isFreeGeneration } from "./status";
import { ProgressStepper } from "./progress-stepper";

export function InProgressPanel({ generation }: { generation: Generation }) {
  const isFree = isFreeGeneration(generation);
  const description = STATUS_DESCRIPTIONS[generation.status];

  return (
    <div data-testid="generate-in-progress-panel" className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-10">
      {/* Spinner */}
      <div className="relative">
        <div
          className={`h-20 w-20 rounded-full border-2 flex items-center justify-center ${
            isFree
              ? "border-emerald-500/30 bg-emerald-500/5"
              : "border-blue-500/30 bg-blue-500/5"
          }`}
        >
          <Loader2
            className={`h-10 w-10 animate-spin ${isFree ? "text-emerald-400" : "text-blue-400"}`}
          />
        </div>
        {/* Tier badge */}
        <div
          className={`absolute -bottom-1 -right-1 rounded-full border-2 border-slate-800 px-2 py-0.5 text-[10px] font-mono font-bold ${
            isFree
              ? "bg-emerald-500 text-white"
              : "bg-blue-500 text-white"
          }`}
        >
          {isFree ? "SPARK" : TIER_NAMES[generation.tier]}
        </div>
      </div>

      {/* Status heading + description */}
      <div className="text-center space-y-2 max-w-lg">
        <p className="font-mono text-xs text-slate-500 tracking-[0.3em] uppercase">
          {generation.status.replace(/_/g, " ")}
        </p>
        <h2 className="text-xl font-bold text-white">
          {STATUS_LABELS[generation.status]}
        </h2>
        <p className="text-slate-400 text-sm leading-relaxed">{description}</p>
      </div>

      {/* Progress stepper */}
      <ProgressStepper status={generation.status} />

      {/* Build log stream */}
      <SectionErrorBoundary section="Build Log">
        <BuildLogConsole log={generation.build_log} />
      </SectionErrorBoundary>

      {/* Generation ID */}
      <div className="w-full max-w-sm rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 space-y-1">
        <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">Generation ID</p>
        <p className="font-mono text-xs text-blue-400 break-all">{generation.id}</p>
        <p className="font-mono text-[10px] text-slate-600">
          {isFree
            ? "Keep this page open — we'll embed the IDE when your preview is ready."
            : "Keep this page open — we'll show your download when complete."}
        </p>
      </div>
    </div>
  );
}
