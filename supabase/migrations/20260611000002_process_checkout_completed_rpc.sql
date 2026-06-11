-- ============================================================================
-- process_checkout_completed — one atomic transaction for the
-- checkout.session.completed webhook: idempotency-event insert, generation
-- tier update, and transaction upsert commit or roll back together.
--
-- Previously the Engine recorded the stripe_event FIRST and ran the side
-- effects afterwards over separate HTTP calls; a downstream failure left the
-- event marked processed, so Stripe's retry short-circuited as a duplicate
-- and the paid generation was never enqueued.
--
-- Returns exactly one row: is_new=false means the event was already
-- processed (caller must not enqueue). When is_new=true the remaining
-- columns carry the generation payload the Engine needs to enqueue the job
-- (NULLs when the generation row is missing).
-- ============================================================================

create or replace function public.process_checkout_completed(
  p_event_id        text,
  p_event_type      text,
  p_session_id      text,
  p_payment_intent  text,
  p_generation_id   uuid,
  p_tier            int,
  p_amount          bigint
) returns table (
  is_new               boolean,
  mode                 text,
  prompt               text,
  project_type         text,
  schema_json          jsonb,
  personalization_json jsonb
)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_new boolean;
begin
  insert into public.stripe_events (id, type)
  values (p_event_id, p_event_type)
  on conflict (id) do nothing;
  v_new := found;  -- exactly one concurrent caller wins the insert

  if not v_new then
    return query select false, null::text, null::text, null::text, null::jsonb, null::jsonb;
    return;
  end if;

  update public.generations g set tier = p_tier where g.id = p_generation_id;

  insert into public.transactions
    (stripe_session_id, stripe_payment_intent, tier, amount, status,
     generation_id, last_stripe_event_id, updated_at)
  values
    (p_session_id, p_payment_intent, p_tier, p_amount::int, 'completed',
     p_generation_id, p_event_id, now())
  on conflict (stripe_session_id) do update set
    stripe_payment_intent = excluded.stripe_payment_intent,
    tier                  = excluded.tier,
    amount                = excluded.amount,
    status                = excluded.status,
    generation_id         = excluded.generation_id,
    last_stripe_event_id  = excluded.last_stripe_event_id,
    updated_at            = now();

  return query
    select true, g.mode, g.prompt, g.project_type, g.schema_json, g.personalization_json
    from public.generations g where g.id = p_generation_id;
  if not found then
    -- Generation row missing: the event is still recorded; the Engine falls
    -- back to session-metadata values for the enqueue.
    return query select true, null::text, null::text, null::text, null::jsonb, null::jsonb;
  end if;
end;
$$;

revoke all on function public.process_checkout_completed(text, text, text, text, uuid, int, bigint)
  from public, anon, authenticated;
grant execute on function public.process_checkout_completed(text, text, text, text, uuid, int, bigint)
  to service_role;
