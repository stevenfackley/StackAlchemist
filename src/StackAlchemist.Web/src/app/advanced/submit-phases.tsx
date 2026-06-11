"use client";

import { Loader2 } from "lucide-react";
import type { Generation, ProjectType, Tier } from "@/lib/types";
import { BuildLogConsole } from "@/components/build-log-console";
import { Panel } from "@/components/ui";

interface SubmittingPanelProps {
  generationId: string | null;
  selectedProjectType: ProjectType;
  selectedTier: Tier;
  liveStatus: Generation["status"] | null;
  liveBuildLog: string | null;
}

export function SubmittingPanel({
  generationId,
  selectedProjectType,
  selectedTier,
  liveStatus,
  liveBuildLog,
}: SubmittingPanelProps) {
  return (
    <div className="flex h-full flex-col items-center justify-center px-4">
      <div className="w-full max-w-md space-y-6 text-center">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full border border-accent/30 bg-accent/10">
          <Loader2 className="h-8 w-8 animate-spin text-accent" />
        </div>
        <h2 className="text-xl font-bold text-ink">Synthesizing Your Platform</h2>
        <p className="text-sm text-ink-muted">{liveStatus ? `Status: ${liveStatus}` : "Queued — starting shortly..."}</p>
        {generationId && (
          <Panel className="space-y-2 text-left">
            <p className="font-mono text-xs uppercase text-ink-faint">Generation ID</p>
            <p className="break-all font-mono text-xs text-accent">{generationId}</p>
            <p className="font-mono text-[10px] uppercase text-ink-faint">Platform</p>
            <p className="font-mono text-xs text-ink">{selectedProjectType}</p>
            <p className="font-mono text-xs text-ink-faint">
              {selectedTier === 0
                ? "Keep this page open — we'll launch your live preview when it's ready."
                : "Keep this page open — we'll redirect you when your download is ready."}
            </p>
          </Panel>
        )}
        <BuildLogConsole log={liveBuildLog} title="Live Build Output" className="w-full max-w-2xl overflow-hidden rounded-panel border border-border bg-surface-0/60 text-left" />
        <div className="h-1 w-full rounded-full bg-surface-2">
          <div className="h-full w-3/5 animate-pulse rounded-full bg-accent" />
        </div>
      </div>
    </div>
  );
}
