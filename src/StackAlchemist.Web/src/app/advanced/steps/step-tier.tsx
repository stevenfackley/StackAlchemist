"use client";

import { CheckCircle2 } from "lucide-react";
import type { Tier } from "@/lib/types";
import type { FreeQuotaStatus } from "@/lib/actions";
import { cn } from "@/lib/utils";
import { Stack } from "@/components/ui";

export function StepTier({ selectedTier, setSelectedTier, quota }: {
  selectedTier: Tier;
  setSelectedTier: (t: Tier) => void;
  quota: FreeQuotaStatus | null;
}) {
  const tiers: { id: Tier; name: string; price: string; items: string[]; recommended?: boolean; isFree?: boolean }[] = [
    {
      id: 0, name: "SPARK", price: "Free", isFree: true,
      items: ["ER Canvas", "Generated Next.js UI (view-only)", "Live Micro IDE Preview"],
    },
    { id: 1, name: "BLUEPRINT", price: "$299", items: ["Schema JSON", "API Specifications", "SQL Scripts"] },
    { id: 2, name: "BOILERPLATE", price: "$599", items: ["Blueprint features", "Full Source Code", "Compile Guarantee"], recommended: true },
    { id: 3, name: "INFRASTRUCTURE", price: "$999", items: ["Boilerplate features", "AWS CDK Stack", "Helm Charts", "Deployment Runbook"] },
  ];
  return (
    <div data-testid="advanced-step-5-tier-grid" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      {tiers.map((t) => (
        <button key={t.id} type="button" onClick={() => setSelectedTier(t.id)}
          data-testid={`advanced-tier-option-${t.id}`}
          className={cn("relative rounded-panel border p-5 text-left transition-all duration-300",
            selectedTier === t.id
              ? t.isFree
                ? "border-success/60 bg-success/10 shadow-[0_0_20px_rgba(16,185,129,0.15)]"
                : "border-accent/60 bg-accent/10 shadow-[0_0_20px_rgba(77,166,255,0.15)]"
              : "border-border bg-surface-2/30 hover:border-accent/30"
          )}>
          {t.isFree && (
            <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-success px-2 py-0.5 text-[0.625rem] font-medium text-white whitespace-nowrap">
              Free
            </div>
          )}
          {t.recommended && (
            <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-accent px-2 py-0.5 text-[0.625rem] font-medium text-white whitespace-nowrap">
              Recommended
            </div>
          )}
          <Stack gap="sm">
            <div>
              <h3 className="font-mono text-xs font-bold uppercase tracking-[0.15em] text-ink">{t.name}</h3>
              <p className={cn("mt-1 font-mono text-xl font-bold", t.isFree ? "text-success" : "text-accent")}>{t.price}</p>
              {t.isFree && quota && (
                <p className={cn("mt-0.5 font-mono text-[0.625rem]", quota.remaining === 0 ? "text-yellow-400" : "text-ink-faint")}>
                  {quota.remaining === 0
                    ? `Limit reached — resets ${quota.resetsAtLabel}`
                    : `${quota.remaining} of ${quota.limit} free builds remaining`}
                </p>
              )}
            </div>
            <ul className="flex flex-col gap-1">
              {t.items.map((item) => (
                <li key={item} className="flex items-center gap-2 font-mono text-[0.6875rem] text-ink-muted">
                  <span className="text-success">›</span> {item}
                </li>
              ))}
            </ul>
            {selectedTier === t.id && (
              <div className="flex items-center gap-1.5">
                <CheckCircle2 className={cn("h-3.5 w-3.5", t.isFree ? "text-success" : "text-accent")} />
                <span className={cn("font-mono text-[0.625rem]", t.isFree ? "text-success" : "text-accent")}>Selected</span>
              </div>
            )}
          </Stack>
        </button>
      ))}
    </div>
  );
}
