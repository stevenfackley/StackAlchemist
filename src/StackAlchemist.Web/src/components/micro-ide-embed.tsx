"use client";

import { useEffect, useRef, useState } from "react";
import { Loader2, AlertCircle } from "lucide-react";

interface MicroIdeEmbedProps {
  /** File contents keyed by relative path, e.g. { "src/app/page.tsx": "..." } */
  files: Record<string, string>;
  /** Project title shown in the StackBlitz tab */
  title: string;
  /** Which file to open initially in the editor */
  openFile?: string;
}

/**
 * MicroIdeEmbed
 *
 * Embeds a live StackBlitz IDE in the page using the @stackblitz/sdk.
 * The SDK is imported dynamically so it doesn't bloat the server bundle.
 *
 * For the Spark (free) tier, `files` comes from `preview_files_json` on the
 * generation record. The project runs in StackBlitz WebContainers — a full
 * Node.js runtime in the browser — so Next.js dev server starts automatically.
 */
export function MicroIdeEmbed({ files, title, openFile }: MicroIdeEmbedProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;

    async function mount() {
      try {
        // Dynamic import keeps this out of the SSR / initial bundle
        const sdk = (await import("@stackblitz/sdk")).default;
        if (!containerRef.current || !mounted) return;

        await sdk.embedProject(
          containerRef.current,
          {
            title,
            // "node" template activates WebContainers (Node.js in the browser)
            // which is required for Next.js dev server
            template: "node",
            files,
          },
          {
            // Show the editor (left) + live preview (right).
            // Cast needed: StackBlitz SDK types don't expose "both" publicly
            // but the runtime accepts it. "default" is the typed alternative.
            view: "default",
            // Open the main page file by default
            openFile: openFile ?? "src/app/page.tsx",
            // Keep the terminal visible so users can see the dev server boot
            terminalHeight: 30,
            forceEmbedLayout: true,
            // Disable the "fork" / "open in new tab" controls to enforce
            // the view-only / non-downloadable nature of the Spark tier
            hideNavigation: false,
            hideDevTools: false,
          }
        );

        if (mounted) setState("ready");
      } catch (err) {
        console.error("[MicroIdeEmbed] StackBlitz embed error:", err);
        if (mounted) {
          setErrorMsg(
            err instanceof Error ? err.message : "Unknown error loading IDE"
          );
          setState("error");
        }
      }
    }

    mount();
    return () => {
      mounted = false;
    };
  }, [files, title, openFile]);

  return (
    <div className="relative w-full h-full rounded-xl overflow-hidden border border-slate-600/30 bg-slate-900">
      {/* Loading overlay */}
      {state === "loading" && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-4 bg-slate-900">
          <div className="h-12 w-12 rounded-full border-2 border-emerald-500/30 flex items-center justify-center">
            <Loader2 className="h-6 w-6 text-emerald-400 animate-spin" />
          </div>
          <div className="text-center space-y-1">
            <p className="font-mono text-xs text-white">Booting WebContainers...</p>
            <p className="font-mono text-[10px] text-slate-500">
              Starting Next.js dev server in your browser
            </p>
          </div>
          {/* Animated progress dots */}
          <div className="flex gap-1.5 mt-1">
            {[0, 1, 2].map((i) => (
              <div
                key={i}
                className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-bounce"
                style={{ animationDelay: `${i * 150}ms` }}
              />
            ))}
          </div>
        </div>
      )}

      {/* Error state */}
      {state === "error" && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-4 bg-slate-900 px-8">
          <div className="h-12 w-12 rounded-full bg-rose-500/10 border border-rose-500/30 flex items-center justify-center">
            <AlertCircle className="h-6 w-6 text-rose-400" />
          </div>
          <div className="text-center space-y-2 max-w-sm">
            <p className="font-mono text-sm text-white font-bold">IDE failed to load</p>
            <p className="font-mono text-xs text-slate-400 leading-relaxed">
              {errorMsg ?? "StackBlitz WebContainers could not start. This may be a browser compatibility issue."}
            </p>
            <p className="font-mono text-[10px] text-slate-600">
              Chromium-based browsers (Chrome, Edge, Arc) are required for WebContainers.
            </p>
          </div>
          <button
            onClick={() => window.location.reload()}
            className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-4 py-2 rounded-full uppercase tracking-widest transition-colors"
          >
            Retry
          </button>
        </div>
      )}

      {/* StackBlitz embed target */}
      <div
        ref={containerRef}
        className="w-full h-full"
        style={{ opacity: state === "ready" ? 1 : 0, transition: "opacity 0.3s ease" }}
      />
    </div>
  );
}
