-- ============================================================================
-- Add 'refund_pending' to transactions.status for the automatic Compile
-- Guarantee refund (StripeRefundService).
--
-- The refund flow is now: CompileWorkerService's final build failure (tier
-- >= 1) calls StripeRefundService, which atomically claims the transaction
-- via a conditional PATCH (status=eq.completed → refund_pending) BEFORE
-- calling the Stripe API. That CAS is what makes the flow idempotent and
-- race-safe: a transaction that isn't still 'completed' (already claimed,
-- already refunded, disputed, or never completed) simply isn't matched, so a
-- concurrent or duplicate call is a no-op instead of a double refund.
--
-- 'refund_pending' is intentionally distinct from 'refunded': the Engine only
-- *initiates* the refund here (Stripe API call accepted). The existing
-- charge.refunded webhook (StripeWebhookHandler) remains the source of truth
-- that finalizes the row to 'refunded' once Stripe confirms the money moved.
-- If the Stripe API call itself fails, StripeRefundService reverts the row
-- back to 'completed' so it isn't stranded in limbo.
-- ============================================================================

alter table public.transactions
  drop constraint if exists transactions_status_check;

alter table public.transactions
  add constraint transactions_status_check
  check (status in ('pending', 'completed', 'failed', 'refund_pending', 'refunded', 'disputed'));
