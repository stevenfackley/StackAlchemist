# Stripe Webhooks Runbook

How Stripe reaches the Engine, what the live endpoint must be configured with, and how to check it without making a payment.

## The path

Stripe → `https://stackalchemist.app/api/webhooks/stripe` → Cloudflare → tunnel → nginx → `sa-engine`.

- nginx sends every `/api/` path except `/api/auth/`, `/api/csp-report` and `/api/healthz` to the Engine.
- The Engine's `X-Engine-Key` middleware exempts `/api/webhooks/`, so Stripe needs no app key.
- The Engine verifies the `Stripe-Signature` header with `STRIPE_WEBHOOK_SECRET` (GitHub Prod environment → `Stripe:WebhookSecret`). A bad or missing signature returns **401 with an empty body**.

Probing prod with a bare POST shows that the route is wired. Checked on 2026-10-03:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST -H 'Content-Type: application/json' --data '{}' https://stackalchemist.app/api/webhooks/stripe
# 401  (from the signature check)
```

A JSON `{"error":"Unauthorized"}` body would instead mean the key middleware caught the request, which would be a regression.

## Required endpoint configuration (Stripe dashboard, live mode)

Go to **Developers → Webhooks**. Exactly one endpoint should exist with:

- **URL:** `https://stackalchemist.app/api/webhooks/stripe`
- **Status:** enabled
- **Events:**

| Event | What the Engine does |
|---|---|
| `checkout.session.completed` | The paid moment for cards. Runs `process_checkout_completed` (one Postgres transaction: event, tier, transaction) and enqueues the build. A session that completes with `payment_status = unpaid` (a delayed method) is deferred. |
| `checkout.session.async_payment_succeeded` | The paid moment for delayed payment methods. Handled like `completed`. Only needed if a delayed method (ACH or another bank debit) is enabled in Checkout. Subscribe to it anyway. |
| `checkout.session.async_payment_failed` | Marks the transaction `failed`. |
| `payment_intent.payment_failed` | Marks the transaction `failed`. |
| `charge.refunded` | Marks the transaction `refunded` and fails a generation that hasn't been delivered yet. This finalizes Compile Guarantee refunds. |
| `charge.dispute.created` | Marks the transaction `disputed`, matched by payment intent. |

The **signing secret** shown on that endpoint must equal the `STRIPE_WEBHOOK_SECRET` secret in GitHub's Prod environment. If it doesn't, every delivery is answered 401 and Stripe keeps retrying.

## Checking delivery without paying

1. **Dashboard:** open the endpoint and look at **Recent deliveries**. Every entry should be `200`. A `401` means the signing secret doesn't match. A `5xx` means the Engine asked Stripe to retry (`rpc_failed`, `enqueue_failed` or `idempotency_unavailable` in the Engine logs).
2. **Database (read-only):** the Engine records every event it accepts in `stackalchemist.stripe_events`, except `checkout.session.completed`, whose record is written by `process_checkout_completed`. Use `select type, count(*), max(processed_at) from stackalchemist.stripe_events group by type;` with the session URL from the credentials vault.
3. **Zero-charge probe:** `.github/workflows/stripe-wiring-check.yml` (`workflow_dispatch`, Prod environment; approved by the owner 2026-10-03). Run it with `gh workflow run stripe-wiring-check.yml -f probe=true`.
   - **Read-only by default.** It lists the live endpoints and fails if none is enabled at the URL above with every event in the table.
   - **`probe: true`** creates a $1 Checkout Session and expires it at once, so it is never payable. It temporarily subscribes the endpoint to `checkout.session.expired`, then confirms Stripe delivered the event with a 2xx and that the event id landed in `stripe_events`. That proves the full chain, signing secret included.
   - **`fix_missing_events: true`** subscribes the endpoint to any handled event it is missing.

As of 2026-10-03, `stripe_events` was empty: no live event had reached the Engine since the 2026-10-01 cutover. No payment had happened, and the one test session's `checkout.session.expired` isn't a subscribed event, so this does not prove a fault. It does mean delivery has not yet been proven end to end on qavren-db.

## Failure handling, by design

- **Idempotency:**
  - Checkout events are deduplicated inside `process_checkout_completed`.
  - Every other event goes through `TryRecordEventAsync` before its side effects.
  - A redelivery is answered 200 with `skipped: duplicate`.
- **Retries:** the Engine returns 5xx only when a retry can help: the idempotency log was unreachable, the checkout call failed, or the enqueue failed (the event record is then deleted so the redelivery runs fresh). Everything else is answered 200.
- **A paid checkout for a generation that doesn't exist** records the payment with a NULL `generation_id`, records the event, and logs `MANUAL RECOVERY` (Engine EventId 305). It does not retry. Refund it or recreate the generation by hand.
- **Unpaid paid-tier rows are never built:** the reconciler requeues a stale `pending` row with tier ≥ 1 only when a completed transaction backs it. It fails the row ("Checkout was not completed.") once the session can no longer be paid, 25 h after the row's last update.
- **A refund claim that fails mid-flight** is reverted (guarded on `refund_pending`) and logs `MANUAL RECOVERY` (EventId 807).
