"use client";

import { useState, useTransition } from "react";
import { Loader2, AlertCircle, ArrowRight } from "lucide-react";
import { createCheckoutSession } from "@/lib/actions";
import { Modal } from "@/components/ui";
import type { Generation, Tier } from "@/lib/types";
import { PAID_TIERS } from "./status";

export function UpgradeModal({ generation, onClose }: { generation: Generation; onClose: () => void }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [pendingTier, setPendingTier] = useState<Tier | null>(null);

  function handlePick(tier: Tier) {
    setError(null);
    setPendingTier(tier);
    startTransition(async () => {
      const result = await createCheckoutSession(
        generation.id,
        tier,
        generation.prompt ?? undefined,
        "DotNetNextJs",
        generation.mode,
        `/generate/${generation.id}`
      );
      if (result.success) {
        window.location.href = result.sessionUrl;
      } else {
        setError(result.error);
        setPendingTier(null);
      }
    });
  }

  return (
    <Modal
      onClose={onClose}
      title="Unlock Source Download"
      zLayer="overlay"
      testId="upgrade-modal"
    >
      <p className="font-mono text-[10px] text-slate-500 -mt-2 mb-4">
        One-time payment &middot; you own it forever
      </p>
      <div className="space-y-3">
        {PAID_TIERS.map((t) => (
          <button
            key={t.id}
            onClick={() => handlePick(t.id)}
            disabled={isPending}
            data-testid={`upgrade-tier-${t.id}`}
            className="w-full flex items-center justify-between gap-4 rounded-xl border border-slate-600/40 bg-slate-700/20 hover:border-blue-500/50 hover:bg-blue-500/5 px-4 py-3 text-left transition-colors disabled:opacity-50"
          >
            <div>
              <div className="font-mono text-xs font-bold text-white uppercase tracking-widest">{t.name}</div>
              <div className="text-xs text-slate-400 mt-0.5">{t.tagline}</div>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <span className="text-lg font-bold text-blue-400">{t.price}</span>
              {isPending && pendingTier === t.id ? (
                <Loader2 className="h-4 w-4 text-blue-400 animate-spin" />
              ) : (
                <ArrowRight className="h-4 w-4 text-slate-500" />
              )}
            </div>
          </button>
        ))}
        {error && (
          <p className="font-mono text-xs text-rose-400 flex items-start gap-1.5">
            <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-0.5" /> {error}
          </p>
        )}
      </div>
    </Modal>
  );
}
