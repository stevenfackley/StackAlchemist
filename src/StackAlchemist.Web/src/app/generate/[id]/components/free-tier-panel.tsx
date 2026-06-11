"use client";

import { useState } from "react";
import { Eye, Lock, ArrowRight, TerminalSquare } from "lucide-react";
import dynamic from "next/dynamic";
import type { Generation } from "@/lib/types";
import { SectionErrorBoundary } from "@/components/error-boundary";
import { UpgradeModal } from "./upgrade-modal";

const MicroIdeEmbed = dynamic(
  () => import("@/components/micro-ide-embed").then((m) => m.MicroIdeEmbed),
  { ssr: false }
);

function SchemaFallbackView({ generation, onUpgrade }: { generation: Generation; onUpgrade: () => void }) {
  const schema = generation.schema_json;

  return (
    <div className="h-full flex flex-col items-center justify-center px-4 py-8 space-y-8">
      {/* Icon */}
      <div className="h-16 w-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center">
        <TerminalSquare className="h-8 w-8 text-emerald-400" />
      </div>

      <div className="text-center space-y-2 max-w-md">
        <h3 className="text-lg font-bold text-white">Your Architecture is Ready</h3>
        <p className="text-slate-400 text-sm leading-relaxed">
          The live IDE preview will be available once the engine populates the
          preview files. In the meantime, here&apos;s your generated schema:
        </p>
      </div>

      {schema && (
        <div className="w-full max-w-2xl space-y-4">
          {/* Entities */}
          {schema.entities.length > 0 && (
            <div className="rounded-xl border border-slate-600/30 bg-slate-700/20 overflow-hidden">
              <div className="px-4 py-2.5 border-b border-slate-600/30 bg-slate-700/30">
                <p className="font-mono text-[10px] tracking-[0.3em] text-blue-400 uppercase">
                  Entities ({schema.entities.length})
                </p>
              </div>
              <div className="p-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
                {schema.entities.map((entity) => (
                  <div
                    key={entity.name}
                    className="rounded-lg border border-blue-500/20 bg-slate-800/40 overflow-hidden"
                  >
                    <div className="px-3 py-1.5 bg-blue-500/10 border-b border-blue-500/20">
                      <span className="font-mono text-xs font-bold text-blue-400 uppercase tracking-widest">
                        {entity.name}
                      </span>
                    </div>
                    <div className="px-3 py-2 space-y-0.5">
                      {entity.fields.map((f) => (
                        <div key={f.name} className="flex items-center gap-2">
                          {f.pk && (
                            <span className="font-mono text-[9px] text-yellow-400 border border-yellow-400/30 px-0.5 rounded">
                              PK
                            </span>
                          )}
                          <span className="font-mono text-[11px] text-white">{f.name}</span>
                          <span className="font-mono text-[10px] text-slate-500">{f.type}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Relationships */}
          {schema.relationships.length > 0 && (
            <div className="rounded-xl border border-slate-600/30 bg-slate-700/20 overflow-hidden">
              <div className="px-4 py-2.5 border-b border-slate-600/30 bg-slate-700/30">
                <p className="font-mono text-[10px] tracking-[0.3em] text-purple-400 uppercase">
                  Relationships ({schema.relationships.length})
                </p>
              </div>
              <div className="p-4 space-y-1.5">
                {schema.relationships.map((r, i) => (
                  <div key={i} className="flex items-center gap-2 font-mono text-xs">
                    <span className="text-blue-400">{r.from}</span>
                    <span className="text-slate-600">→</span>
                    <span className="text-[10px] text-purple-400 border border-purple-500/30 px-1.5 py-0.5 rounded">
                      {r.type}
                    </span>
                    <span className="text-slate-600">→</span>
                    <span className="text-blue-400">{r.to}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Upgrade CTA */}
      <div className="rounded-xl border border-blue-500/20 bg-blue-500/5 p-5 max-w-md text-center space-y-3">
        <p className="text-slate-300 text-sm font-medium">Want the full downloadable codebase?</p>
        <p className="text-slate-500 text-xs leading-relaxed">
          Upgrade to Blueprint ($299), Boilerplate ($599), or Infrastructure ($999) to get the full
          source code — compiled, tested, and yours forever.
        </p>
        <button
          onClick={onUpgrade}
          className="inline-flex items-center gap-2 rounded-full bg-blue-500 hover:bg-blue-400 text-white px-5 py-2.5 text-sm font-medium transition-all duration-300"
        >
          View Paid Tiers <ArrowRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}

export function FreeTierPanel({ generation }: { generation: Generation }) {
  const [showUpgrade, setShowUpgrade] = useState(false);
  const hasFiles =
    generation.preview_files_json &&
    Object.keys(generation.preview_files_json).length > 0;

  const prompt = generation.prompt ?? "Your SaaS";
  const title = `StackAlchemist — ${prompt.slice(0, 50)}`;

  return (
    <div
      data-testid="generate-free-tier-panel"
      className="flex flex-col h-full min-h-0"
    >
      {/* Banner */}
      <div className="shrink-0 border-b border-emerald-500/20 bg-emerald-500/5 px-4 py-3 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <Eye className="h-4 w-4 text-emerald-400 shrink-0" />
          <div>
            <p className="font-mono text-xs font-bold text-emerald-400 tracking-widest uppercase">
              Spark — Live Preview
            </p>
            <p className="font-mono text-[10px] text-slate-500 mt-0.5">
              Code is view-only. Download requires a paid tier.
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3 shrink-0">
          <div className="flex items-center gap-1.5">
            <Lock className="h-3 w-3 text-slate-600" />
            <span className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">
              No Download
            </span>
          </div>
          <button
            onClick={() => setShowUpgrade(true)}
            data-testid="free-tier-upgrade-button"
            className="flex items-center gap-1.5 rounded-full bg-blue-500 hover:bg-blue-400 text-white px-3 py-1.5 font-mono text-[10px] uppercase tracking-widest transition-colors whitespace-nowrap"
          >
            Upgrade to Download
            <ArrowRight className="h-3 w-3" />
          </button>
        </div>
      </div>

      {/* IDE or fallback */}
      <div className="flex-1 min-h-0 p-4">
        <SectionErrorBoundary section="Code Preview">
          {hasFiles ? (
            <MicroIdeEmbed
              files={generation.preview_files_json!}
              title={title}
              openFile="app/page.tsx"
            />
          ) : (
            <SchemaFallbackView generation={generation} onUpgrade={() => setShowUpgrade(true)} />
          )}
        </SectionErrorBoundary>
      </div>

      {showUpgrade && (
        <UpgradeModal generation={generation} onClose={() => setShowUpgrade(false)} />
      )}
    </div>
  );
}
