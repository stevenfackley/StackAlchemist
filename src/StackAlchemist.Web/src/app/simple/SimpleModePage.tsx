"use client";

import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { AlertCircle, Loader2 } from "lucide-react";
import { submitSimpleGeneration } from "@/lib/actions";
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
  const [phase, setPhase] = useState<"building" | "error">(prompt ? "building" : "error");
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
  useEffect(() => {
    if (!generationId || !supabase) return;
    const client = supabase;
    let done = false;

    const apply = (row: Pick<Generation, "status" | "error_message">) => {
      if (done) return;
      setLiveStatus(row.status);
      if (row.status === "success") {
        done = true;
        // Hard navigation (NOT router.push): the result page must arrive as a
        // fresh document so its COOP/COEP headers apply and
        // window.crossOriginIsolated is true — required by the StackBlitz
        // WebContainers preview. A soft nav reuses the current (non-isolated)
        // document and StackBlitz errors "without isolation headers".
        window.location.href = `/generate/${generationId}`;
      } else if (row.status === "failed") {
        done = true;
        setErrorMsg(row.error_message ?? "Generation failed. Please try again.");
        setPhase("error");
      }
    };

    const channel = client
      .channel(`generation:${generationId}`)
      .on(
        "postgres_changes",
        { event: "UPDATE", schema: "public", table: "generations", filter: `id=eq.${generationId}` },
        (payload) => apply(payload.new as Generation)
      )
      .subscribe((status) => {
        // The deterministic Tier-0 render can flip status=success BEFORE this WS
        // handshake completes, and postgres_changes never replays events missed
        // before SUBSCRIBED — so fetch the current row once to catch an
        // already-finished build. Later completions still arrive via UPDATE.
        if (status !== "SUBSCRIBED") return;
        void (async () => {
          const { data } = await client
            .from("generations")
            .select("status, error_message")
            .eq("id", generationId)
            .single();
          if (data) apply(data as Pick<Generation, "status" | "error_message">);
        })();
      });

    return () => {
      done = true;
      client.removeChannel(channel);
    };
  }, [generationId]);

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

        {/* ── Phase: Error ──────────────────────────────────────────────────── */}
        {phase === "error" && (
          <div data-testid="simple-phase-error" className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-6">
            <div className="h-16 w-16 rounded-full bg-rose-500/10 border-2 border-rose-500/30 flex items-center justify-center">
              <AlertCircle className="h-8 w-8 text-rose-400" />
            </div>
            <div className="text-center space-y-2 max-w-md">
              <h2 className="text-xl font-bold text-white">Something went wrong</h2>
              <p className="text-slate-400 text-sm leading-relaxed">{errorMsg ?? "An unexpected error occurred."}</p>
            </div>
            <Link
              href="/"
              className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-5 py-2.5 rounded-full uppercase tracking-widest transition-colors text-center"
            >
              Start Over
            </Link>
          </div>
        )}
      </main>
    </div>
  );
}
