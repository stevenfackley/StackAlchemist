/**
 * tiers.ts — the one place the site's tier names and prices come from, and the
 * check that it agrees with what the Engine tells Stripe to charge.
 */
import { readFileSync } from "node:fs";
import path from "node:path";
import { TIERS, tierName, tierPriceLabel } from "@/lib/tiers";

describe("tiers.ts", () => {
  it("names every tier, and falls back for one it does not know", () => {
    expect([0, 1, 2, 3].map(tierName)).toEqual(["Spark", "Blueprint", "Boilerplate", "Infrastructure"]);
    expect(tierName(7)).toBe("Unknown");
  });

  it("labels Spark as free and the paid tiers by price", () => {
    expect(tierPriceLabel(0)).toBe("Free");
    expect(tierPriceLabel(1)).toBe("$299");
    expect(tierPriceLabel(2)).toBe("$599");
    expect(tierPriceLabel(3)).toBe("$999");
  });

  it("shows the prices the Engine charges (TierCatalog.cs)", () => {
    // The Engine sets the Stripe amount in cents, per tier, as `1 => new(29_900, ...`.
    const catalog = readFileSync(
      path.resolve(__dirname, "../../../StackAlchemist.Engine/Services/TierCatalog.cs"),
      "utf8"
    );
    const charged = Object.fromEntries(
      [...catalog.matchAll(/^\s*(\d) => new\(([\d_]+),/gm)].map(([, tier, cents]) => [
        Number(tier),
        Number(cents.replaceAll("_", "")),
      ])
    );

    expect(charged).toEqual({
      1: TIERS[1].priceUsd * 100,
      2: TIERS[2].priceUsd * 100,
      3: TIERS[3].priceUsd * 100,
    });
  });
});
