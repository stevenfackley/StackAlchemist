-- ============================================================================
-- generations — code generation jobs
-- ============================================================================

create table if not exists public.generations (
  id                 uuid primary key default gen_random_uuid(),
  user_id            uuid references public.profiles(id) on delete set null,
  transaction_id     uuid references public.transactions(id) on delete set null,
  mode               text not null check (mode in ('simple', 'advanced')),
  tier               int not null default 0 check (tier between 0 and 3),
  prompt             text,
  schema_json        jsonb,
  status             text not null default 'pending'
                     check (status in (
                       'pending',
                       'extracting_schema',
                       'generating_code',
                       'building',
                       'success',
                       'failed'
                     )),
  download_url       text,
  preview_files_json jsonb,                        -- Tier 0 (Spark) only
  build_log          text,                         -- streaming build output
  error_message      text,
  attempt_count      int not null default 0,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  completed_at       timestamptz
);

create index idx_generations_user_id on public.generations(user_id);
create index idx_generations_status on public.generations(status);

-- Auto-update updated_at
create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create trigger generations_updated_at
  before update on public.generations
  for each row execute function public.set_updated_at();

-- RLS
alter table public.generations enable row level security;

-- Anon/authenticated users can insert (unauthenticated free-tier generations)
create policy "Anyone can insert generations"
  on public.generations for insert
  with check (true);

-- Users can read own generations (authenticated) or by ID (anon via Realtime)
create policy "Anyone can read generations"
  on public.generations for select
  using (true);

-- Only service_role updates generations (Engine writes status/build_log/download_url)
create policy "Service role updates generations"
  on public.generations for update
  using (auth.role() = 'service_role');

-- ============================================================================
-- Enable Realtime on generations for live status updates
-- ============================================================================
alter publication supabase_realtime add table public.generations;
