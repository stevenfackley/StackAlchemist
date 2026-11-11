/**
 * generation-outcome.ts — what the result page may offer for a failed build.
 * The rule under test: never show a Retry that will be refused, and always say
 * what has happened to the payment.
 */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { failureOutcome, MAX_RETRIES } from "@/lib/generation-outcome";
import { GENERATION_ERROR_CATEGORIES, type GenerationErrorCategory, type PaymentStatus, type Tier } from "@/lib/types";

function failed(over: {
  tier?: Tier;
  error_category?: GenerationErrorCategory | null;
  attempt_count?: number;
  payment_status?: PaymentStatus | null;
}) {
  return { tier: 2 as Tier, error_category: "network" as GenerationErrorCategory | null, attempt_count: 0, ...over };
}

describe("failureOutcome — free builds", () => {
  it.each(["schema", "rate_limit", "network", null] as const)("offers Retry for %s", (category) => {
    expect(failureOutcome(failed({ tier: 0, error_category: category }))).toEqual({
      canRetry: true, retriesLeft: 3, retriesUsedUp: false, payment: "none",
    });
  });

  it.each(["quota", "internal"] as const)("offers no Retry for %s", (category) => {
    const outcome = failureOutcome(failed({ tier: 0, error_category: category }));
    expect(outcome.canRetry).toBe(false);
    expect(outcome.retriesUsedUp).toBe(false);
    expect(outcome.payment).toBe("none");
  });

  it("counts retries down and stops offering one when they are used", () => {
    expect(failureOutcome(failed({ tier: 0, attempt_count: 2 }))).toMatchObject({ canRetry: true, retriesLeft: 1 });
    expect(failureOutcome(failed({ tier: 0, attempt_count: 3 }))).toEqual({
      canRetry: false, retriesLeft: 0, retriesUsedUp: true, payment: "none",
    });
    // The reconciler can push the count past the limit.
    expect(failureOutcome(failed({ tier: 0, attempt_count: 5 })).retriesLeft).toBe(0);
  });
});

describe("failureOutcome — paid builds", () => {
  it("payment in place and a retry left: Retry, and the payment is kept", () => {
    expect(failureOutcome(failed({ payment_status: "completed", attempt_count: 1 }))).toEqual({
      canRetry: true, retriesLeft: 2, retriesUsedUp: false, payment: "kept",
    });
  });

  it.each(["refund_pending", "refunded"] as const)("%s: no Retry (the Engine would answer 402), and says refunded", (status) => {
    expect(failureOutcome(failed({ payment_status: status }))).toMatchObject({ canRetry: false, payment: "refunded" });
  });

  it("an internal failure with the payment still in place: no Retry, a refund is on its way", () => {
    expect(failureOutcome(failed({ payment_status: "completed", error_category: "internal" }))).toMatchObject({
      canRetry: false, payment: "refund_expected",
    });
  });

  it("a compile failure is refunded under the Compile Guarantee, so it is never offered a Retry", () => {
    expect(failureOutcome(failed({ payment_status: "completed", error_category: "build" }))).toMatchObject({
      canRetry: false, payment: "refund_expected",
    });
  });

  it("retries used up with the payment still in place: no Retry, a refund is on its way", () => {
    expect(failureOutcome(failed({ payment_status: "completed", attempt_count: MAX_RETRIES }))).toEqual({
      canRetry: false, retriesLeft: 0, retriesUsedUp: true, payment: "refund_expected",
    });
  });

  it("a disputed payment cannot be retried", () => {
    expect(failureOutcome(failed({ payment_status: "disputed" }))).toMatchObject({ canRetry: false, payment: "disputed" });
  });

  it.each([null, "pending", "failed"] as const)("no completed payment (%s): no Retry, unpaid", (status) => {
    expect(failureOutcome(failed({ payment_status: status }))).toMatchObject({ canRetry: false, payment: "unpaid" });
  });

  it("payment state not loaded: falls back to the category and the count, and claims nothing about money", () => {
    expect(failureOutcome(failed({}))).toMatchObject({ canRetry: true, payment: "unknown" });
    expect(failureOutcome(failed({ error_category: "internal" }))).toMatchObject({ canRetry: false, payment: "unknown" });
  });
});

describe("the Engine's copy of these rules", () => {
  // The Engine refunds a paid failure exactly when this page offers no way to retry it. If the
  // two disagree, a customer is either refunded while a Retry button is showing, or left with
  // no button and no refund.
  const engine = (file: string) =>
    readFileSync(resolve(__dirname, "../../../StackAlchemist.Engine/Services", file), "utf8");

  it("ErrorCategorizer.OffersRetry excludes the same categories", () => {
    const match = /OffersRetry\(string category\) => category is not \(([^)]*)\)/.exec(engine("ErrorCategorizer.cs"));
    expect(match).not.toBeNull();
    const constants = match![1].split(/\s+or\s+/).map((name) => name.trim());
    const source = engine("ErrorCategorizer.cs");
    const engineNoRetry = constants
      .map((name) => new RegExp(`const string ${name} = "([a-z_]+)"`).exec(source)?.[1])
      .sort();

    const webNoRetry = GENERATION_ERROR_CATEGORIES
      .filter((category) => !failureOutcome(failed({ tier: 0, error_category: category })).canRetry)
      .sort();

    expect(engineNoRetry).toEqual(webNoRetry);
  });

  it("FailedBuildRefunder.RetryAttemptLimit is MAX_RETRIES", () => {
    const match = /const int RetryAttemptLimit = (\d+);/.exec(engine("FailedBuildRefunder.cs"));
    expect(Number(match?.[1])).toBe(MAX_RETRIES);
  });
});
