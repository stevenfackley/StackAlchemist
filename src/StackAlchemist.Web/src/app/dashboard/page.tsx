import type { Metadata } from "next";
import { redirect } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import {
  LayoutDashboard,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Download,
  Eye,
  Clock,
  Zap,
  Sparkles,
} from "lucide-react";
import { getServerUser } from "@/lib/supabase-server";
import { getMyGenerations, getProfileSettings, getFreeQuotaStatus } from "@/lib/actions";
import { ByokSettingsForm } from "./ByokSettingsForm";
import { GenerationsLiveRefresher } from "./GenerationsLiveRefresher";
import { SectionErrorBoundary } from "@/components/error-boundary";
import type { Generation } from "@/lib/types";

export const metadata: Metadata = {
  title: "Dashboard",
  description:
    "Track your StackAlchemist generations, downloads, and delivery status.",
};

/* ─── Tier helpers ───────────────────────────────────────────────────────── */
const TIER_NAMES: Record<number, string> = {
  0: "Spark",
  1: "Blueprint",
  2: "Boilerplate",
  3: "Infrastructure",
};
const TIER_COLORS: Record<number, string> = {
  0: "text-emerald-400 border-emerald-500/30 bg-emerald-500/10",
  1: "text-accent border-accent/30 bg-accent/10",
  2: "text-purple-400 border-purple-500/30 bg-purple-500/10",
  3: "text-amber-400 border-amber-500/30 bg-amber-500/10",
};

/* ─── Status badge ───────────────────────────────────────────────────────── */
function StatusBadge({ status }: { status: Generation["status"] }) {
  const map: Record<
    Generation["status"],
    { label: string; classes: string; icon: React.ReactNode }
  > = {
    pending: {
      label: "Pending",
      classes: "text-slate-400 border-slate-600/40 bg-slate-700/20",
      icon: <Clock className="h-3 w-3" />,
    },
    extracting_schema: {
      label: "Extracting",
      classes: "text-accent border-accent/30 bg-accent/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    generating_code: {
      label: "Generating",
      classes: "text-accent border-accent/30 bg-accent/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    generating: {
      label: "Generating",
      classes: "text-accent border-accent/30 bg-accent/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    building: {
      label: "Building",
      classes: "text-amber-400 border-amber-500/30 bg-amber-500/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    packing: {
      label: "Packaging",
      classes: "text-amber-400 border-amber-500/30 bg-amber-500/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    uploading: {
      label: "Uploading",
      classes: "text-amber-400 border-amber-500/30 bg-amber-500/10",
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
    },
    success: {
      label: "Complete",
      classes: "text-emerald-400 border-emerald-500/30 bg-emerald-500/10",
      icon: <CheckCircle2 className="h-3 w-3" />,
    },
    failed: {
      label: "Failed",
      classes: "text-rose-400 border-rose-500/30 bg-rose-500/10",
      icon: <AlertCircle className="h-3 w-3" />,
    },
  };

  const { label, classes, icon } = map[status] ?? map.pending;

  return (
    <span
      className={`inline-flex items-center gap-1 font-mono text-[10px] uppercase tracking-widest border rounded-full px-2 py-0.5 ${classes}`}
    >
      {icon}
      {label}
    </span>
  );
}

/* ─── Generation row ─────────────────────────────────────────────────────── */
function GenerationRow({ gen }: { gen: Generation }) {
  const tierName = TIER_NAMES[gen.tier] ?? "Unknown";
  const tierClasses = TIER_COLORS[gen.tier] ?? TIER_COLORS[0];
  const isComplete = gen.status === "success";
  const isFree = gen.tier === 0;

  return (
    <div className="px-5 py-4 flex flex-col sm:flex-row sm:items-center gap-3 hover:bg-slate-700/10 transition-colors group">
      {/* Mode + tier */}
      <div className="shrink-0 flex items-center gap-2">
        <span
          className={`inline-flex items-center font-mono text-[10px] uppercase tracking-widest border rounded-full px-2 py-0.5 ${tierClasses}`}
        >
          {tierName}
        </span>
        <span className="font-mono text-[10px] text-slate-600 uppercase tracking-widest">
          {gen.mode}
        </span>
      </div>

      {/* Prompt */}
      <div className="flex-1 min-w-0">
        <p className="font-mono text-xs text-slate-300 truncate">
          {gen.prompt ?? <span className="text-slate-600 italic">No prompt</span>}
        </p>
        <p className="font-mono text-[10px] text-slate-600 mt-0.5">
          {gen.id.slice(0, 8)}…{" "}
          &middot;{" "}
          {new Date(gen.created_at).toLocaleDateString(undefined, {
            year: "numeric",
            month: "short",
            day: "numeric",
          })}
        </p>
      </div>

      {/* Status */}
      <div className="shrink-0">
        <StatusBadge status={gen.status} />
      </div>

      {/* Actions */}
      <div className="shrink-0 flex items-center gap-2">
        {/* View generation page */}
        {/* Plain <a> (not <Link>): a full-document load so /generate/[id]'s COOP/COEP
            headers apply and the StackBlitz preview is cross-origin isolated. */}
        <a
          href={`/generate/${gen.id}`}
          className="flex items-center gap-1 font-mono text-[10px] uppercase tracking-widest text-slate-500 hover:text-accent transition-colors"
          title="View generation"
        >
          <Eye className="h-3.5 w-3.5" />
          <span className="hidden sm:block">View</span>
        </a>

        {/* Download (paid + complete only) */}
        {isComplete && !isFree && gen.download_url && (
          <a
            href={gen.download_url}
            download
            className="flex items-center gap-1 font-mono text-[10px] uppercase tracking-widest text-emerald-400 hover:text-emerald-300 transition-colors"
            title="Download"
          >
            <Download className="h-3.5 w-3.5" />
            <span className="hidden sm:block">Download</span>
          </a>
        )}
      </div>
    </div>
  );
}

/* ─── Dashboard Page (Server Component) ─────────────────────────────────── */
export default async function DashboardPage() {
  const user = await getServerUser();

  // Auth gate — redirect to login with returnTo so they come back after auth.
  if (!user) {
    redirect("/login?returnTo=/dashboard");
  }

  const [generations, profileSettings, quota] = await Promise.all([
    getMyGenerations(),
    getProfileSettings(),
    getFreeQuotaStatus(),
  ]);

  const total = generations.length;
  const completed = generations.filter((g) => g.status === "success").length;
  const inProgress = generations.filter(
    (g) => !["success", "failed"].includes(g.status)
  ).length;

  return (
    <div className="min-h-screen bg-slate-800 flex flex-col">
      {/* ── Header ────────────────────────────────────────────────────────── */}
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-header">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80">
            <Image
              src="/logo.svg"
              alt="Stack Alchemist"
              width={28}
              height={28}
              className="drop-shadow-[0_0_6px_rgba(77,166,255,0.4)]"
            />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-accent">AL</span>CHEMIST
            </span>
          </Link>
          <span className="text-slate-600 font-mono text-xs">|</span>
          <div className="flex items-center gap-2">
            <LayoutDashboard className="h-3.5 w-3.5 text-accent" />
            <span className="font-mono text-xs text-slate-400 uppercase tracking-widest">Dashboard</span>
          </div>

          {/* Spacer */}
          <div className="flex-1" />

          {/* User email */}
          <span className="hidden sm:block font-mono text-[10px] text-slate-500 truncate max-w-[200px]">
            {user.email}
          </span>

          {/* Sign out */}
          <form action="/auth/signout" method="POST">
            <button
              type="submit"
              className="font-mono text-[10px] text-slate-500 hover:text-rose-400 transition-colors uppercase tracking-widest"
            >
              Sign Out
            </button>
          </form>
        </div>
      </header>

      {/* Null-rendering island: subscribes to Supabase Realtime and calls
          router.refresh() (throttled 5s) so status badges update without a
          manual reload. Server component stays the source of truth for rows. */}
      <GenerationsLiveRefresher userId={user.id} />

      <main className="flex-1 max-w-6xl mx-auto w-full px-4 py-10 space-y-8">
        {/* ── Page title ──────────────────────────────────────────────────── */}
        <div className="space-y-1">
          <h1 className="text-2xl font-bold text-white tracking-tight">
            Your Generations
          </h1>
          <p className="text-slate-400 text-sm">
            All projects generated while signed in to your account.
          </p>
        </div>

        {/* ── Stats row ───────────────────────────────────────────────────── */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          {[
            { label: "Total", value: total, icon: <Zap className="h-4 w-4 text-accent" /> },
            { label: "Complete", value: completed, icon: <CheckCircle2 className="h-4 w-4 text-emerald-400" /> },
            { label: "In Progress", value: inProgress, icon: <Loader2 className="h-4 w-4 text-amber-400 animate-spin" /> },
            { label: "Free Builds Left", value: `${quota.remaining} / ${quota.limit}`, icon: <Sparkles className="h-4 w-4 text-emerald-400" /> },
          ].map(({ label, value, icon }) => (
            <div
              key={label}
              className="rounded-xl border border-slate-600/30 bg-slate-700/20 px-5 py-4 flex items-center gap-3"
            >
              {icon}
              <div>
                <p className="text-xl font-bold text-white">{value}</p>
                <p className="font-mono text-[10px] text-slate-500 uppercase tracking-widest">
                  {label}
                </p>
              </div>
            </div>
          ))}
        </div>

        {/* ── Generation list ──────────────────────────────────────────────── */}
        <SectionErrorBoundary section="Generations">
        <div className="rounded-2xl border border-slate-600/40 bg-slate-700/10 overflow-hidden">
          {/* Table header */}
          <div className="px-5 py-3 border-b border-slate-600/30 bg-slate-700/20 hidden sm:grid grid-cols-[auto_1fr_auto_auto] gap-4">
            {["Tier", "Prompt", "Status", "Actions"].map((h) => (
              <span
                key={h}
                className="font-mono text-[9px] text-slate-600 uppercase tracking-[0.25em]"
              >
                {h}
              </span>
            ))}
          </div>

          {generations.length === 0 ? (
            /* Empty state */
            <div className="px-5 py-16 text-center space-y-4">
              <div className="h-14 w-14 rounded-full bg-slate-700/30 border border-slate-600/30 flex items-center justify-center mx-auto">
                <Zap className="h-6 w-6 text-slate-600" />
              </div>
              <p className="font-mono text-sm text-slate-500">
                No generations yet. Start one from the{" "}
                <Link href="/" className="text-accent hover:text-accent underline underline-offset-2">
                  home page
                </Link>
                .
              </p>
            </div>
          ) : (
            <div className="divide-y divide-slate-700/40">
              {generations.map((gen) => (
                <GenerationRow key={gen.id} gen={gen} />
              ))}
            </div>
          )}
        </div>
        </SectionErrorBoundary>

        {/* ── BYOK / API Settings ─────────────────────────────────────────── */}
        <SectionErrorBoundary section="API Settings">
          <ByokSettingsForm
            settings={{
              ...profileSettings,
              email: user.email ?? profileSettings.email,
            }}
          />
        </SectionErrorBoundary>

        {/* ── New generation CTA ──────────────────────────────────────────── */}
        <div className="pt-2 text-center">
          <Link
            href="/"
            className="inline-flex items-center gap-2 rounded-full bg-accent hover:bg-accent/90 text-white px-6 py-3 font-mono text-xs uppercase tracking-widest transition-colors"
          >
            <Zap className="h-3.5 w-3.5" />
            Generate New Project
          </Link>
        </div>
      </main>
    </div>
  );
}
