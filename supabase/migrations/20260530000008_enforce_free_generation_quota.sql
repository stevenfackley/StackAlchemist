-- ============================================================================
-- Free-tier quota — 5 Spark (tier 0) generations per account, per calendar month
-- ============================================================================
-- The web app inserts generations with the SERVICE_ROLE key, which bypasses RLS.
-- The Engine can also insert directly. So the cap CANNOT live in an RLS policy —
-- it must be a BEFORE INSERT trigger, which fires for every writer (service_role
-- included) and is therefore impossible to bypass from the application layer.
-- Failed runs are excluded so a crash never burns a user's slot.
-- ----------------------------------------------------------------------------

create or replace function public.enforce_free_generation_quota()
returns trigger
language plpgsql
as $$
declare
  used int;
begin
  -- Only the free Spark tier is capped; paid tiers (1-3) are unlimited.
  if new.tier <> 0 then
    return new;
  end if;

  -- A free generation must belong to an account for the per-account cap to mean
  -- anything. Auth is also enforced in middleware + server actions; this makes
  -- the database the final authority.
  if new.user_id is null then
    raise exception 'Free-tier generations require an authenticated account'
      using errcode = 'check_violation';
  end if;

  select count(*)
    into used
    from public.generations
   where user_id = new.user_id
     and tier = 0
     and status <> 'failed'
     and created_at >= date_trunc('month', now());

  if used >= 5 then
    raise exception 'Free generation limit reached: 5 builds per month. Upgrade to download or wait until next month.'
      using errcode = 'check_violation';
  end if;

  return new;
end;
$$;

create trigger generations_enforce_free_quota
  before insert on public.generations
  for each row execute function public.enforce_free_generation_quota();

-- ----------------------------------------------------------------------------
-- Tighten the INSERT policy (defense-in-depth).
-- service_role still bypasses RLS, so the web/Engine insert paths are unchanged.
-- This only governs anon-key/authenticated client inserts: now an authenticated
-- user may insert rows for THEMSELVES only, and anonymous inserts are blocked
-- (every creation is tied to an account, matching the auth gate in middleware).
-- ----------------------------------------------------------------------------
drop policy if exists "Anyone can insert generations" on public.generations;

create policy "Users insert own generations"
  on public.generations for insert
  to authenticated
  with check (user_id = auth.uid());
