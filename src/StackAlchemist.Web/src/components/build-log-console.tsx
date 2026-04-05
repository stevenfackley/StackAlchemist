"use client";

import { TerminalSquare } from "lucide-react";

interface BuildLogConsoleProps {
  log: string | null | undefined;
  title?: string;
  className?: string;
}

export function BuildLogConsole({
  log,
  title = "Build Output",
  className,
}: BuildLogConsoleProps) {
  if (!log) {
    return null;
  }

  return (
    <div className={className ?? "w-full max-w-2xl rounded-xl border border-slate-600/30 bg-slate-900/60 overflow-hidden"}>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-slate-700/50 bg-slate-800/50">
        <TerminalSquare className="h-3.5 w-3.5 text-slate-500" />
        <span className="font-mono text-[10px] text-slate-500 tracking-widest uppercase">{title}</span>
      </div>
      <div className="px-4 py-3 max-h-64 overflow-y-auto">
        <pre className="font-mono text-[11px] text-slate-300 whitespace-pre-wrap leading-relaxed">
          {log}
        </pre>
      </div>
    </div>
  );
}
