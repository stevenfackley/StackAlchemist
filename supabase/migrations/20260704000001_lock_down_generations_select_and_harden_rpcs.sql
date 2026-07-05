-- ============================================================================
-- Lock down generations SELECT + harden the SECURITY DEFINER build-log/token
-- RPCs. Closes two confirmed anon-key holes.
--
-- ── Hole 1: bulk read of every generation via the public anon key ───────────
-- 20260404000003 shipped `create policy "Anyone can read generations" ...
-- using (true)`, and no later migration tightened it (20260530000008 only
-- touched INSERT). Anyone holding the public anon key could
-- `GET /rest/v1/generations?select=*` and enumerate every user's prompt,
-- schema_json, personalization_json, error_message, and download_url (a
-- presigned R2 URL valid up to 168h in prod) — a bulk paid-content + PII leak.
--
-- Replaced with an owner-only policy scoped to the `authenticated` role. `anon`
-- gets NO select policy, so REST enumeration returns zero rows for the anon key.
--
-- Every read path that leaned on the permissive policy keeps working:
--   * The /generate/[id] status page (initial load + polling fallback) and the
--     /dashboard list fetch through the service_role server actions
--     (getGeneration / getMyGenerations / retryGeneration), which bypass RLS.
--   * Supabase Realtime honours RLS. Both live subscriptions —
--     use-generation-realtime (`id=eq.<id>`) and GenerationsLiveRefresher
--     (`user_id=eq.<uid>`) — run on the browser client carrying the signed-in
--     user's JWT, so `auth.uid() = user_id` is true for their own rows and the
--     UPDATE stream is delivered unchanged. It ALSO now stops a user from
--     subscribing to someone else's `user_id=eq.<other>` feed (previously
--     allowed by `using (true)`; GenerationsLiveRefresher had a client-side
--     guard for exactly this, now enforced server-side).
--   * /generate/* and /simple + /advanced are auth-gated in middleware, and
--     every generation row is account-bound (20260530000008 rejects
--     `user_id is null` at insert), so there is no anonymous reader to keep
--     alive — the "anonymous Spark" path no longer exists.
--
-- Residual (accepted): getGeneration uses the service_role client and does not
-- check ownership, so an authenticated user who already knows another row's
-- UUID v4 can still fetch that single row server-side (a capability model on an
-- unguessable id). This closes ENUMERATION and bulk download_url harvesting via
-- the anon REST key, which was the actual leak; single-row access to a known
-- UUID is left unchanged and out of scope.
--
-- ── Hole 2: unrestricted EXECUTE on the SECURITY DEFINER write RPCs ──────────
-- 20260412000006 created append_build_log / increment_token_usage as
-- SECURITY DEFINER and GRANTed EXECUTE to service_role, but never revoked the
-- implicit PUBLIC EXECUTE grant Postgres adds to every new function. So
-- anon/authenticated could call them over PostgREST RPC
-- (`POST /rest/v1/rpc/append_build_log`) and tamper with ANY generation's
-- build_log or token counters. They also lacked a pinned search_path — a
-- SECURITY DEFINER hijack vector. Revoke PUBLIC and pin search_path, matching
-- 20260611000002's process_checkout_completed. The Engine calls both with the
-- service_role key (SupabaseDeliveryService), so it is unaffected.
-- ============================================================================

-- ── 1. Owner-only SELECT ────────────────────────────────────────────────────
drop policy if exists "Anyone can read generations" on public.generations;

drop policy if exists "Users read own generations" on public.generations;
create policy "Users read own generations"
  on public.generations for select
  to authenticated
  using (auth.uid() = user_id);

-- ── 2. Harden the SECURITY DEFINER RPCs ─────────────────────────────────────
revoke all on function public.append_build_log(uuid, text)
  from public, anon, authenticated;
grant execute on function public.append_build_log(uuid, text)
  to service_role;
alter function public.append_build_log(uuid, text)
  set search_path = public;

revoke all on function public.increment_token_usage(uuid, integer, integer, text)
  from public, anon, authenticated;
grant execute on function public.increment_token_usage(uuid, integer, integer, text)
  to service_role;
alter function public.increment_token_usage(uuid, integer, integer, text)
  set search_path = public;
