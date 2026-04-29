-- ============================================================================
-- Expand transactions to support refund / failure / dispute reconciliation
-- and add per-event idempotency for Stripe webhooks.
-- ============================================================================

alter table public.transactions
  add column if not exists generation_id uuid references public.generations(id) on delete set null,
  add column if not exists stripe_payment_intent text,
  add column if not exists stripe_charge_id text,
  add column if not exists last_stripe_event_id text,
  add column if not exists updated_at timestamptz not null default now();

alter table public.transactions
  drop constraint if exists transactions_status_check;

alter table public.transactions
  add constraint transactions_status_check
  check (status in ('pending', 'completed', 'failed', 'refunded', 'disputed'));

create index if not exists idx_transactions_payment_intent
  on public.transactions(stripe_payment_intent)
  where stripe_payment_intent is not null;

create index if not exists idx_transactions_charge
  on public.transactions(stripe_charge_id)
  where stripe_charge_id is not null;

-- ============================================================================
-- stripe_events — idempotency log so a retried webhook never double-processes.
-- ============================================================================
create table if not exists public.stripe_events (
  id           text primary key,
  type         text not null,
  processed_at timestamptz not null default now()
);

alter table public.stripe_events enable row level security;

create policy "Service role manages stripe_events"
  on public.stripe_events for all
  using (auth.role() = 'service_role');
