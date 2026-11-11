import type { Generation, GenerationErrorCategory } from "@/lib/types";

/**
 * How many times the owner can retry a failed build. Each press of Retry raises
 * `retry_count`; at this number the web refuses a retry and the Engine refunds a
 * paid build that fails. Automatic restarts by the Engine's reconciler are
 * counted apart (`attempt_count`) and never spend one of these. Keep in step
 * with `FailedBuildRefunder.RetryLimit` in the Engine (a test checks it).
 */
export const MAX_RETRIES = 3;

/**
 * How long after a failure the Engine's automatic refund is still "on its way".
 * The Engine starts it within seconds of writing the failed status. Past this,
 * a payment that is still in place means the refund did not go out (the attempt
 * failed, or the build failed before automatic refunds existed), and the page
 * says so instead of repeating a promise nothing stands behind.
 */
export const REFUND_GRACE_MS = 15 * 60 * 1000;

/**
 * Whether a failure of this category is worth retrying. The Engine reads the
 * same table (`ErrorCategorizer.OffersRetry`) to decide when a paid failure
 * has no retry left and is refunded. A test checks the two agree.
 */
const RETRYABLE: Record<GenerationErrorCategory, boolean> = {
  quota: false,
  schema: true,
  build: true,
  rate_limit: true,
  network: true,
  internal: false,
};

/** What happened to the customer's money for a failed build. */
export type PaymentOutcome =
  /** A free build: nothing was paid. */
  | "none"
  /** The payment is in place and a retry is available. Retrying is free. */
  | "kept"
  /** A refund has been started or has completed. */
  | "refunded"
  /** The payment is still in place, nothing is left to retry, and the failure is recent: the Engine's refund has not been recorded yet. */
  | "refund_expected"
  /** As above, but the failure is old: the automatic refund did not go out. */
  | "refund_overdue"
  /** The customer disputed the charge with their bank. */
  | "disputed"
  /** A paid tier with no completed payment on record. */
  | "unpaid"
  /** The payment state was not loaded with the row. */
  | "unknown";

export interface FailureOutcome {
  /** A retry will be accepted: by the web's retry gate and by the Engine's payment gate. */
  canRetry: boolean;
  retriesLeft: number;
  /** The failure is of a kind worth retrying, but the owner has used every retry. */
  retriesUsedUp: boolean;
  payment: PaymentOutcome;
}

type FailedGeneration =
  Pick<Generation, "tier" | "error_category" | "payment_status">
  & Partial<Pick<Generation, "retry_count" | "updated_at">>;

/**
 * What the result page may truthfully offer for a failed build: whether Retry
 * will work, how many are left, and what has happened to the payment.
 */
export function failureOutcome(generation: FailedGeneration, now: number = Date.now()): FailureOutcome {
  const category = generation.error_category;
  const retriesLeft = Math.max(0, MAX_RETRIES - (generation.retry_count ?? 0));
  const worthRetrying = category ? RETRYABLE[category] : true;
  const retriesUsedUp = worthRetrying && retriesLeft === 0;

  if (generation.tier === 0) {
    return { canRetry: worthRetrying && retriesLeft > 0, retriesLeft, retriesUsedUp, payment: "none" };
  }

  const status = generation.payment_status;
  if (status === undefined) {
    return { canRetry: worthRetrying && retriesLeft > 0, retriesLeft, retriesUsedUp, payment: "unknown" };
  }
  if (status === "refund_pending" || status === "refunded") {
    return { canRetry: false, retriesLeft, retriesUsedUp, payment: "refunded" };
  }
  if (status === "disputed") {
    return { canRetry: false, retriesLeft, retriesUsedUp, payment: "disputed" };
  }
  if (status !== "completed") {
    return { canRetry: false, retriesLeft, retriesUsedUp, payment: "unpaid" };
  }

  // Paid and not refunded. A paid build that fails compilation is refunded under
  // the Compile Guarantee, so Retry is not offered for it: the refund is on its
  // way, and a retry would be refused once it lands.
  const canRetry = worthRetrying && retriesLeft > 0 && category !== "build";
  if (canRetry) {
    return { canRetry, retriesLeft, retriesUsedUp, payment: "kept" };
  }

  const failedAt = generation.updated_at ? Date.parse(generation.updated_at) : NaN;
  const overdue = Number.isFinite(failedAt) && now - failedAt > REFUND_GRACE_MS;
  return { canRetry, retriesLeft, retriesUsedUp, payment: overdue ? "refund_overdue" : "refund_expected" };
}
