"use client";

import { useEffect, useState, useTransition } from "react";
import Link from "next/link";
import Image from "next/image";
import {
  Loader2,
  AlertCircle,
  Download,
  CheckCircle2,
  Eye,
  Lock,
  ArrowRight,
  RefreshCw,
  TerminalSquare,
} from "lucide-react";
import { supabase } from "@/lib/supabase";
import { retryGeneration } from "@/lib/actions";
import type { Generation } from "@/lib/types";
import dynamic from "next/dynamic";

// Lazy-load the IDE embed so StackBlitz SDK is only bundled when needed
const MicroIdeEmbed = dynamic(
  () => import("@/components/micro-ide-embed").then((m) => m.MicroIdeEmbed),
  { ssr: false }
);

// ─── Status helpers ───────────────────────────────────────────────────────────

const STATUS_STEPS: Generation["status"][] = [
  "pending",
  "extracting_schema",
  "generating_code",
  "building",
  "success",
];

const STATUS_LABELS: Record<Generation["status"], string> = {
  pending: "Queued — waiting to start",
  extracting_schema: "Extracting schema from prompt",
  generating_code: "Synthesizing code with Claude 3.5 Sonnet",
  building: "Compile Guarantee — running dotnet build",
  success: "Complete",
  failed: "Build failed — triggering auto-correction",
};

const STATUS_DESCRIPTIONS: Record<Generation["status"], string> = {
  pending: "Your generation is in the queue. It'll kick off within seconds.",
  extracting_schema:
    "Claude is reading your prompt and identifying entities, relationships, and API endpoints.",
  generating_code:
    "Generating the full source tree — API controllers, repositories, models, frontend pages, and styles.",
  building:
    "Running dotnet build inside a container. If it fails, we auto-correct with the compiler output and retry (up to 3×).",
  success: "Your codebase is ready.",
  failed:
    "The build failed. We're retrying automatically. If it still fails after 3 attempts, you'll get a full refund.",
};

function stepIndex(status: Generation["status"]): number {
  return STATUS_STEPS.indexOf(status);
}

function isFreeGeneration(gen: Generation): boolean {
  return gen.tier === 0;
}

// ─── Tier name helper ─────────────────────────────────────────────────────────
const TIER_NAMES: Record<number, string> = {
  0: "Spark",
  1: "Blueprint",
  2: "Boilerplate",
  3: "Infrastructure",
};

// ─── Progress Bar ─────────────────────────────────────────────────────────────
function ProgressStepper({ status }: { status: Generation["status"] }) {
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

// ─── Free Tier IDE Panel ──────────────────────────────────────────────────────
function FreeTierPanel({
  generation,
}: {
  generation: Generation;
}) {
  const hasFiles =
    generation.preview_files_json &&
    Object.keys(generation.preview_files_json).length > 0;

  const prompt = generation.prompt ?? "Your SaaS";
  const title = `StackAlchemist — ${prompt.slice(0, 50)}`;

  return (
    <div className="flex flex-col h-full min-h-0">
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
          <Link
            href="/pricing"
            className="flex items-center gap-1.5 rounded-full bg-blue-500 hover:bg-blue-400 text-white px-3 py-1.5 font-mono text-[10px] uppercase tracking-widest transition-colors whitespace-nowrap"
          >
            Upgrade to Download
            <ArrowRight className="h-3 w-3" />
          </Link>
        </div>
      </div>

      {/* IDE or fallback */}
      <div className="flex-1 min-h-0 p-4">
        {hasFiles ? (
          <MicroIdeEmbed
            files={generation.preview_files_json!}
            title={title}
            openFile="src/app/page.tsx"
          />
        ) : (
          <SchemaFallbackView generation={generation} />
        )}
      </div>
    </div>
  );
}

// ─── Schema Fallback (when preview_files_json is not yet available) ───────────
function SchemaFallbackView({ generation }: { generation: Generation }) {
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
        <Link
          href="/pricing"
          className="inline-flex items-center gap-2 rounded-full bg-blue-500 hover:bg-blue-400 text-white px-5 py-2.5 text-sm font-medium transition-all duration-300"
        >
          View Paid Tiers <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
}

// ─── Paid Tier Download Panel ─────────────────────────────────────────────────
function PaidTierPanel({ generation }: { generation: Generation }) {
  const tierName = TIER_NAMES[generation.tier] ?? "Unknown";

  return (
    <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
      {/* Success icon */}
      <div className="relative">
        <div className="h-20 w-20 rounded-full bg-emerald-500/10 border-2 border-emerald-500/40 flex items-center justify-center">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
        </div>
        <div className="absolute -top-1 -right-1 h-6 w-6 rounded-full bg-blue-500 border-2 border-slate-800 flex items-center justify-center">
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
            ? "Your full-stack codebase has been compiled and verified. Download, run, and ship."
            : "Your production-ready stack — code + infrastructure + runbook — is packaged and verified."}
        </p>
      </div>

      {/* Info card */}
      <div className="w-full max-w-md rounded-xl border border-slate-600/30 bg-slate-700/20 divide-y divide-slate-700/40">
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Tier</span>
          <span className="font-mono text-xs text-blue-400 font-bold">{tierName}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Mode</span>
          <span className="font-mono text-xs text-white capitalize">{generation.mode}</span>
        </div>
        <div className="px-5 py-3 flex items-center justify-between">
          <span className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">Status</span>
          <span className="font-mono text-xs text-emerald-400 flex items-center gap-1.5">
            <CheckCircle2 className="h-3 w-3" /> Compile Verified
          </span>
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
          className="font-mono text-xs text-slate-500 hover:text-blue-400 transition-colors tracking-widest uppercase"
        >
          &larr; Generate Another
        </Link>
      </div>
    </div>
  );
}

// ─── In-Progress Panel ────────────────────────────────────────────────────────
function InProgressPanel({ generation }: { generation: Generation }) {
  const isFree = isFreeGeneration(generation);
  const description = STATUS_DESCRIPTIONS[generation.status];

  return (
    <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-10">
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

// ─── Failed Panel ─────────────────────────────────────────────────────────────
function FailedPanel({
  generation,
  onRetry,
  isRetrying,
}: {
  generation: Generation;
  onRetry: () => void;
  isRetrying: boolean;
}) {
  const canRetry = generation.attempt_count < 3;

  return (
    <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-6">
      <div className="h-20 w-20 rounded-full bg-rose-500/10 border-2 border-rose-500/30 flex items-center justify-center">
        <AlertCircle className="h-10 w-10 text-rose-400" />
      </div>

      <div className="text-center space-y-2 max-w-md">
        <h2 className="text-xl font-bold text-white">Generation Failed</h2>
        <p className="text-slate-400 text-sm leading-relaxed">
          {generation.error_message ?? "An unexpected error occurred during generation."}
        </p>
        {generation.attempt_count > 0 && (
          <p className="font-mono text-xs text-slate-600">
            Attempt {generation.attempt_count} of 3
          </p>
        )}
      </div>

      {/* Actions */}
      <div className="flex flex-col sm:flex-row gap-3">
        {canRetry && (
          <button
            onClick={onRetry}
            disabled={isRetrying}
            className="flex items-center gap-2 rounded-full border border-blue-500/30 text-blue-400 hover:bg-blue-500/10 px-5 py-2.5 text-sm font-medium transition-all duration-300 disabled:opacity-60"
          >
            {isRetrying ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
            {isRetrying ? "Retrying..." : "Retry Generation"}
          </button>
        )}
        <Link
          href="/"
          className="flex items-center gap-2 rounded-full bg-slate-700/50 border border-slate-600/50 text-slate-300 hover:border-blue-500/40 hover:text-blue-400 px-5 py-2.5 text-sm font-medium transition-all duration-300"
        >
          Start Over
        </Link>
      </div>

      {!canRetry && (
        <div className="rounded-xl border border-rose-500/20 bg-rose-500/5 p-5 max-w-sm text-center space-y-2">
          <p className="text-rose-400 text-sm font-medium">Max retries reached</p>
          <p className="text-slate-500 text-xs leading-relaxed">
            You&apos;re entitled to a full refund. Please contact support with your generation ID:{" "}
            <code className="text-blue-400 font-mono text-[10px] bg-slate-800/50 px-1 rounded">
              {generation.id}
            </code>
          </p>
        </div>
      )}
    </div>
  );
}

// ─── Main Client Page ─────────────────────────────────────────────────────────
interface Props {
  initialGeneration: Generation;
  generationId: string;
}

export function GenerateClientPage({ initialGeneration, generationId }: Props) {
  const [generation, setGeneration] = useState<Generation>(initialGeneration);
  const [isPending, startTransition] = useTransition();

  // ── Realtime subscription ──────────────────────────────────────────────────
  useEffect(() => {
    if (!supabase) return;
    if (generation.status === "success" || generation.status === "failed") return;

    const client = supabase;
    const channel = client
      .channel(`gen-result:${generationId}`)
      .on(
        "postgres_changes",
        {
          event: "UPDATE",
          schema: "public",
          table: "generations",
          filter: `id=eq.${generationId}`,
        },
        (payload) => {
          const updated = payload.new as Generation;
          setGeneration(updated);
          // For paid tiers: redirect is handled by the success panel rendering
          // For free tier: we stay on this page and show the IDE embed
        }
      )
      .subscribe();

    return () => {
      client.removeChannel(channel);
    };
  }, [generationId, generation.status]);

  // ── Retry handler ──────────────────────────────────────────────────────────
  function handleRetry() {
    startTransition(async () => {
      const result = await retryGeneration(generationId);
      if (result.success) {
        // Reset local state to pending so progress bar reappears
        setGeneration((g) => ({
          ...g,
          status: "pending",
          error_message: null,
        }));
      }
    });
  }

  const isFree = isFreeGeneration(generation);
  const isComplete = generation.status === "success";
  const isFailed = generation.status === "failed";
  const isInProgress = !isComplete && !isFailed;

  return (
    <div
      className={`min-h-screen flex flex-col bg-slate-800 ${
        isComplete && isFree ? "h-screen overflow-hidden" : ""
      }`}
    >
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <header className="shrink-0 border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50">
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
              STACK <span className="text-blue-400">AL</span>CHEMIST
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
              <span className="rounded-full bg-blue-500/10 border border-blue-500/30 px-2 py-0.5 font-mono text-[10px] text-blue-400 uppercase tracking-widest shrink-0">
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
              <span className="flex items-center gap-1.5 font-mono text-[10px] text-blue-400 uppercase tracking-widest">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Processing
              </span>
            )}
          </div>
        </div>
      </header>

      {/* ── Prompt banner ────────────────────────────────────────────────────── */}
      {generation.prompt && (
        <div className="shrink-0 border-b border-slate-700/50 bg-slate-700/10 px-4 py-2">
          <div className="max-w-7xl mx-auto flex items-center gap-3">
            <p className="font-mono text-[10px] text-slate-600 uppercase tracking-widest shrink-0">Prompt</p>
            <p className="font-mono text-xs text-slate-400 truncate">{generation.prompt}</p>
          </div>
        </div>
      )}

      {/* ── Main Content ─────────────────────────────────────────────────────── */}
      <main
        className={`flex-1 flex flex-col min-h-0 ${
          isComplete && isFree ? "overflow-hidden" : ""
        }`}
      >
        {isInProgress && <InProgressPanel generation={generation} />}

        {isFailed && (
          <FailedPanel
            generation={generation}
            onRetry={handleRetry}
            isRetrying={isPending}
          />
        )}

        {isComplete && isFree && <FreeTierPanel generation={generation} />}

        {isComplete && !isFree && <PaidTierPanel generation={generation} />}
      </main>
    </div>
  );
}
