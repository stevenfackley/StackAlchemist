"use client";

import { Loader2, AlertCircle, CheckCircle2 } from "lucide-react";
import type { Generation } from "@/lib/types";
import { STATUS_STEPS, stepIndex } from "./status";

export function ProgressStepper({ status }: { status: Generation["status"] }) {
  const current = stepIndex(status);
  const isFailed = status === "failed";

  return (
    <div className="w-full max-w-2xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        {STATUS_STEPS.map((s, i) => {
          const done = current > i || status === "success";
          const active = current === i && status !== "success";
          const failed = isFailed && i === current;
          return (
            <div key={s} className="flex-1 flex flex-col items-center gap-2">
              {/* Connector line left */}
              <div className="relative w-full flex items-center">
                {i > 0 && (
                  <div
                    className={`absolute left-0 right-1/2 h-px transition-colors duration-500 ${
                      done || active ? "bg-blue-500" : "bg-slate-700"
                    }`}
                  />
                )}
                {/* Dot */}
                <div
                  className={`relative z-10 mx-auto h-7 w-7 rounded-full border-2 flex items-center justify-center transition-all duration-500 ${
                    failed
                      ? "border-rose-500 bg-rose-500/20"
                      : done
                      ? "border-emerald-500 bg-emerald-500/20"
                      : active
                      ? "border-blue-500 bg-blue-500/20 animate-pulse"
                      : "border-slate-700 bg-slate-800"
                  }`}
                >
                  {failed ? (
                    <AlertCircle className="h-3.5 w-3.5 text-rose-400" />
                  ) : done ? (
                    <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400" />
                  ) : active ? (
                    <Loader2 className="h-3.5 w-3.5 text-blue-400 animate-spin" />
                  ) : (
                    <span className="font-mono text-[10px] text-slate-600">{i + 1}</span>
                  )}
                </div>
                {/* Connector line right */}
                {i < STATUS_STEPS.length - 1 && (
                  <div
                    className={`absolute left-1/2 right-0 h-px transition-colors duration-500 ${
                      done ? "bg-blue-500" : "bg-slate-700"
                    }`}
                  />
                )}
              </div>
              {/* Label */}
              <p
                className={`font-mono text-[9px] tracking-widest uppercase text-center leading-tight hidden sm:block ${
                  failed
                    ? "text-rose-400"
                    : done
                    ? "text-emerald-400"
                    : active
                    ? "text-blue-400"
                    : "text-slate-600"
                }`}
              >
                {s.replace(/_/g, " ")}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
