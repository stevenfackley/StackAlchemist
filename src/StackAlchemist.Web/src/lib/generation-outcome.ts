import type { Generation, GenerationErrorCategory } from "@/lib/types";

/**
 * How many times a failed build can be retried. Each retry (and each re-enqueue
 * by the Engine's reconciler) raises `attempt_count`; at this number the web
 * refuses a retry and the Engine refunds a paid build that fails. Keep in step
 * with `FailedBuildRefunder.RetryAttemptLimit` and `Generation:MaxAttempts` in
 * the Engine.
 */
export const MAX_RETRIES = 3;

/**
 * Whether a failure of this category is worth retrying. The Engine reads the
 * same table (`ErrorCategorizer.OffersRetry`) to decide when a paid failure
 * has no retry left and is refunded. Keep the two in step.
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
  /** The payment is still in place but nothing is left to retry. The Engine refunds these; it has not recorded the refund yet. */
  | "refund_expected"
  /** The customer disputed the charge with their bank. */
  | "disputed"
  /** A paid tier with no completed payment on record. */
  | "unpaid"
  /** The payment state was not loaded with the row. */
  | "unknown";

export interface FailureOutcome {
  /** A retry will be accepted: by the web's attempt gate and by the Engine's payment gate. */
  canRetry: boolean;
  retriesLeft: number;
  /** The failure is of a kind worth retrying, but every retry has been used. */
  retriesUsedUp: boolean;
  payment: PaymentOutcome;
}

type FailedGeneration = Pick<Generation, "tier" | "error_category" | "attempt_count" | "payment_status">;

/**
 * What the result page may truthfully offer for a failed build: whether Retry
 * will work, how many are left, and what has happened to the payment.
 */
export function failureOutcome(generation: FailedGeneration): FailureOutcome {
  const category = generation.error_category;
  const retriesLeft = Math.max(0, MAX_RETRIES - generation.attempt_count);
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
  return { canRetry, retriesLeft, retriesUsedUp, payment: canRetry ? "kept" : "refund_expected" };
}
