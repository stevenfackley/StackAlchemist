"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { Loader2 } from "lucide-react";
import { submitSimpleGeneration, getFreeQuotaStatus } from "@/lib/actions";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import { useGenerationRealtime } from "@/lib/hooks/use-generation-realtime";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";
import type { Generation } from "@/lib/types";

// Friendly, jargon-free narration of the backend pipeline — no "schema",
// "migration", ".NET", or "compile". Spark renders a deterministic preview, so
// these are reassurance, not literal step gating.
const BUILD_STEPS = [
  "Understanding your idea...",
  "Designing your data model...",
  "Assembling your live preview...",
  "Polishing the details...",
];

export default function SimpleModePage() {
  const searchParams = useSearchParams();
  const prompt = searchParams?.get("q") ?? "";

  // No-prompt is knowable at first render (q is a static query param), so seed
  // the error state here rather than via setState-in-effect.
  const [phase, setPhase] = useState<"building" | "error" | "quota">(prompt ? "building" : "error");
  const [generationId, setGenerationId] = useState<string | null>(null);
  const [liveStatus, setLiveStatus] = useState<Generation["status"] | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(
    prompt ? null : "No prompt provided. Head back and describe the app you want."
  );
  const [logLines, setLogLines] = useState<string[]>([]);
  const [progress, setProgress] = useState(0);

  // Exactly-once submit guard. Without it, React StrictMode's double-invoked
  // mount effect would insert TWO tier-0 rows and burn two free-quota slots.
  const submittedRef = useRef(false);

  // ── Kick off a single Spark (tier 0) generation on mount ──────────────────
  useEffect(() => {
    if (submittedRef.current) return;
    if (!prompt) return; // error phase already seeded from initial state
    submittedRef.current = true;

    // Decorative step animation while the engine works.
    let step = 0;
    const interval = setInterval(() => {
      if (step < BUILD_STEPS.length) {
        setLogLines((l) => [...l, BUILD_STEPS[step]]);
        setProgress(Math.round(((step + 1) / BUILD_STEPS.length) * 90));
        step++;
      }
    }, 700);

    void (async () => {
      // Pre-check quota. On network error, proceed — DB trigger is the hard gate.
      try {
        const q = await getFreeQuotaStatus();
        if (q.remaining === 0) {
          clearInterval(interval);
          setPhase("quota");
          return;
        }
      } catch {
        // quota fetch failed — proceed, trigger will enforce on the backend
      }

      const result = await submitSimpleGeneration(prompt, 0);
      clearInterval(interval);
      if (result.success) {
        setProgress(100);
        setGenerationId(result.generationId);
        // Demo / no-Realtime: nothing will push us forward, so navigate now.
        if (isDemoMode || !supabase) {
          window.location.href = `${result.redirectUrl}${result.redirectUrl.includes("?") ? "&" : "?"}tier=0`;
        }
      } else {
        setErrorMsg(result.error);
        setPhase("error");
      }
    })();

    return () => clearInterval(interval);
  }, [prompt]);

  // ── Redirect to the result page once the preview is ready ─────────────────
  // Resilient watcher (catch-up fetch, re-subscribe, polling fallback). The
  // `done` ref guards against double-acting: the live event and the catch-up
  // fetch can both report success.
  const doneRef = useRef(false);
  const applyGenerationUpdate = useCallback(
    (row: Generation) => {
      if (doneRef.current) return;
      setLiveStatus(row.status);
      if (row.status === "success") {
        doneRef.current = true;
        // Hard navigation (NOT router.push): the result page must arrive as a
        // fresh document so its COOP/COEP headers apply and
        // window.crossOriginIsolated is true — required by the StackBlitz
        // WebContainers preview. A soft nav reuses the current (non-isolated)
        // document and StackBlitz errors "without isolation headers".
        window.location.href = `/generate/${generationId}`;
      } else if (row.status === "failed") {
        doneRef.current = true;
        setErrorMsg(row.error_message ?? "Generation failed. Please try again.");
        setPhase("error");
      }
    },
    [generationId]
  );

  useGenerationRealtime({
    generationId,
    enabled: !!generationId,
    onUpdate: applyGenerationUpdate,
  });

  function statusLabel(s: Generation["status"] | null) {
    switch (s) {
      case "pending": return "Queued — starting in a moment...";
      case "extracting_schema": return "Understanding your idea...";
      case "generating_code":
      case "generating": return "Assembling your live preview...";
      case "building": return "Putting it together...";
      case "packing": return "Almost ready...";
      case "uploading": return "Finalizing...";
      case "success": return "Ready! Opening your preview...";
      case "failed": return "Something went wrong...";
      default: return "Building your app...";
    }
  }

  return (
    <div data-testid="simple-mode-page" className="min-h-screen flex flex-col bg-slate-800">
      {/* Header */}
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80">
            <Image src="/logo.svg" alt="Stack Alchemist" width={28} height={28} className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]" />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-blue-400">AL</span>CHEMIST
            </span>
          </Link>
          <span className="text-slate-600 font-mono text-xs">|</span>
          <Link href="/" className="font-mono text-xs text-slate-400 hover:text-blue-400 transition-colors tracking-widest uppercase">
            &larr; Back
          </Link>
        </div>
      </header>

      {/* Prompt banner */}
      <div data-testid="simple-prompt-banner" className="border-b border-slate-700/50 bg-slate-700/20 px-4 py-3">
        <div className="max-w-6xl mx-auto">
          <p className="font-mono text-xs text-slate-500 tracking-widest uppercase mb-1">Prompt</p>
          <p data-testid="simple-prompt-value" className="font-mono text-sm text-white">{prompt || "No prompt provided."}</p>
        </div>
      </div>

      <main className="flex-1 flex flex-col">
        {/* ── Phase: Building ───────────────────────────────────────────────── */}
        {phase === "building" && (
          <div data-testid="simple-phase-building" className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
            <div className="w-full max-w-xl space-y-6 text-center">
              <div className="flex items-center justify-center">
                <div className="h-16 w-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center">
                  <Loader2 className="h-8 w-8 text-emerald-400 animate-spin" />
                </div>
              </div>
              <div>
                <h2 className="text-xl font-bold text-white mb-2">Building Your App</h2>
                <p className="text-slate-400 text-sm leading-relaxed">{statusLabel(liveStatus)}</p>
              </div>
            </div>

            {/* Friendly build log */}
            <div className="w-full max-w-xl rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 space-y-1.5">
              {logLines.map((line, i) => (
                <p key={i} className="font-mono text-xs text-emerald-400">
                  <span className="text-slate-500 mr-2">&rsaquo;</span>
                  {line}
                </p>
              ))}
              {progress < 100 && (
                <p className="font-mono text-xs text-blue-400 animate-pulse">
                  <span className="mr-2">&rsaquo;</span>_
                </p>
              )}
            </div>

            <div className="w-full max-w-xl bg-slate-700 rounded-full h-1 overflow-hidden">
              <div className="h-full bg-emerald-500 transition-all duration-300 rounded-full" style={{ width: `${progress}%` }} />
            </div>

            {generationId && (
              <div className="w-full max-w-sm rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 space-y-1 text-left">
                <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">Generation ID</p>
                <p className="font-mono text-xs text-blue-400 break-all">{generationId}</p>
                <p className="font-mono text-[10px] text-slate-600">Keep this page open — your live preview opens automatically.</p>
              </div>
            )}
          </div>
        )}

        {/* ── Phase: Quota Exhausted ───────────────────────────────────────── */}
        {phase === "quota" && (
          <GenerationErrorPanel category="quota" testId="simple-phase-quota" />
        )}

        {/* ── Phase: Error ──────────────────────────────────────────────────── */}
        {phase === "error" && (
          <GenerationErrorPanel testId="simple-phase-error" errorMessage={errorMsg} />
        )}
      </main>
    </div>
  );
}
