import type { Tier } from "./types";

/**
 * The name and price of each tier, for every page that shows one. The paid
 * prices must match the Engine's TierCatalog.cs: that file sets what Stripe
 * charges, this one sets what the site says it will charge.
 */
export const TIERS: Record<Tier, { name: string; priceUsd: number }> = {
  0: { name: "Spark", priceUsd: 0 },
  1: { name: "Blueprint", priceUsd: 299 },
  2: { name: "Boilerplate", priceUsd: 599 },
  3: { name: "Infrastructure", priceUsd: 999 },
};

/** The tier's display name. A row can carry a tier this build does not know, hence the fallback. */
export function tierName(tier: number): string {
  return TIERS[tier as Tier]?.name ?? "Unknown";
}

/** "Free" for Spark, otherwise the price as "$299". */
export function tierPriceLabel(tier: Tier): string {
  const { priceUsd } = TIERS[tier];
  return priceUsd === 0 ? "Free" : `$${priceUsd}`;
}
