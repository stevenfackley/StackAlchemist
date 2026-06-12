"use client";

import Link from "next/link";
import Image from "next/image";
import { Loader2, AlertCircle, CheckCircle2 } from "lucide-react";
import type { Generation } from "@/lib/types";
import { TIER_NAMES, isFreeGeneration } from "./status";

interface GenerateHeaderProps {
  generation: Generation;
  generationId: string;
  isComplete: boolean;
  isFailed: boolean;
}

export function GenerateHeader({ generation, generationId, isComplete, isFailed }: GenerateHeaderProps) {
  const isFree = isFreeGeneration(generation);

  return (
    <>
      <header className="shrink-0 border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-header">
        <div className="max-w-7xl mx-auto px-4 h-14 flex items-center gap-4">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80 shrink-0">
            <Image
              src="/logo.svg"
              alt="Stack Alchemist"
              width={28}
              height={28}
              className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]"
            />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-accent">AL</span>CHEMIST
            </span>
          </Link>

          <span className="text-slate-600 font-mono text-xs">|</span>

          {/* Generation info */}
          <div className="flex items-center gap-2 flex-1 min-w-0">
            {isFree ? (
              <span className="rounded-full bg-emerald-500/10 border border-emerald-500/30 px-2 py-0.5 font-mono text-[10px] text-emerald-400 uppercase tracking-widest shrink-0">
                Spark
              </span>
            ) : (
              <span className="rounded-full bg-accent/10 border border-accent/30 px-2 py-0.5 font-mono text-[10px] text-accent uppercase tracking-widest shrink-0">
                {TIER_NAMES[generation.tier]}
              </span>
            )}
            <span className="font-mono text-xs text-slate-500 truncate hidden sm:block">
              {generationId}
            </span>
          </div>

          {/* Status badge */}
          <div className="shrink-0">
            {isComplete ? (
              <span className="flex items-center gap-1.5 font-mono text-[10px] text-emerald-400 uppercase tracking-widest">
                <CheckCircle2 className="h-3.5 w-3.5" />
                Complete
              </span>
            ) : isFailed ? (
              <span className="flex items-center gap-1.5 font-mono text-[10px] text-rose-400 uppercase tracking-widest">
                <AlertCircle className="h-3.5 w-3.5" />
                Failed
              </span>
            ) : (
              <span className="flex items-center gap-1.5 font-mono text-[10px] text-accent uppercase tracking-widest">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Processing
              </span>
            )}
          </div>
        </div>
      </header>

      {/* Prompt banner */}
      {generation.prompt && (
        <div className="shrink-0 border-b border-slate-700/50 bg-slate-700/10 px-4 py-2">
          <div className="max-w-7xl mx-auto flex items-center gap-3">
            <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest shrink-0">Prompt</p>
            <p className="font-mono text-xs text-slate-400 truncate">{generation.prompt}</p>
          </div>
        </div>
      )}
    </>
  );
}
