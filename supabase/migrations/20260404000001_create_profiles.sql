-- ============================================================================
-- profiles — extends auth.users with app-specific fields
-- ============================================================================

create table if not exists public.profiles (
  id              uuid primary key references auth.users(id) on delete cascade,
  email           text not null,
  api_key_override text,                          -- encrypted BYOK key
  preferred_model text not null default 'claude-3-5-sonnet',
  created_at      timestamptz not null default now()
);

comment on column public.profiles.api_key_override is 'AES-encrypted Bring-Your-Own-Key value';

-- Auto-create profile on signup via trigger
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer set search_path = ''
as $$
begin
  insert into public.profiles (id, email)
  values (new.id, new.email);
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- RLS
alter table public.profiles enable row level security;

create policy "Users can read own profile"
  on public.profiles for select
  using (auth.uid() = id);

create policy "Users can update own profile"
  on public.profiles for update
  using (auth.uid() = id)
  with check (auth.uid() = id);
