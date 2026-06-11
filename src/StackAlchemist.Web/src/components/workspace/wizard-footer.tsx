"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { Button, buttonVariants } from "@/components/ui";

/**
 * Sticky action bar. Exactly one forward action per step lives here (Next, or
 * Checkout on the last step), keeping the primary action unambiguous. The
 * Previous / Next / Checkout testids are preserved verbatim for the smoke specs.
 */
export function WizardFooter({
  step,
  totalSteps,
  backHref = "/",
  onPrevious,
  onNext,
  onCheckout,
  checkoutDisabled = false,
  checkoutLabel,
  checkoutVariant = "primary",
}: {
  step: number;
  totalSteps: number;
  backHref?: string;
  onPrevious: () => void;
  onNext: () => void;
  onCheckout: () => void;
  checkoutDisabled?: boolean;
  checkoutLabel: ReactNode;
  checkoutVariant?: "primary" | "success";
}) {
  const isLast = step >= totalSteps;
  return (
    <footer className="shrink-0 border-t border-border bg-surface-0/40 px-4 py-3 sm:px-6">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between">
        {step > 1 ? (
          <Button
            variant="ghost"
            size="sm"
            onClick={onPrevious}
            data-testid="advanced-previous-button"
          >
            ← Previous
          </Button>
        ) : (
          <Link href={backHref} className={buttonVariants({ variant: "subtle", size: "sm" })}>
            ← Back
          </Link>
        )}

        {isLast ? (
          <Button
            variant={checkoutVariant}
            size="sm"
            onClick={onCheckout}
            disabled={checkoutDisabled}
            data-testid="advanced-checkout-button"
          >
            {checkoutLabel}
          </Button>
        ) : (
          <Button
            variant="primary"
            size="sm"
            onClick={onNext}
            data-testid="advanced-next-button"
          >
            Next →
          </Button>
        )}
      </div>
    </footer>
  );
}
