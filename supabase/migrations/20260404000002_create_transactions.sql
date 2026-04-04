-- ============================================================================
-- transactions — Stripe payment records
-- ============================================================================

create table if not exists public.transactions (
  id                uuid primary key default gen_random_uuid(),
  user_id           uuid references public.profiles(id) on delete set null,
  stripe_session_id text unique,
  tier              int not null check (tier between 0 and 3),
  amount            int not null default 0,       -- cents
  status            text not null default 'pending'
                    check (status in ('pending', 'completed', 'failed', 'refunded')),
  created_at        timestamptz not null default now()
);

create index idx_transactions_user_id on public.transactions(user_id);
create index idx_transactions_stripe_session on public.transactions(stripe_session_id);

-- RLS
alter table public.transactions enable row level security;

create policy "Users can read own transactions"
  on public.transactions for select
  using (auth.uid() = user_id);

-- Only service_role inserts/updates transactions (via Engine webhooks)
create policy "Service role manages transactions"
  on public.transactions for all
  using (auth.role() = 'service_role');
