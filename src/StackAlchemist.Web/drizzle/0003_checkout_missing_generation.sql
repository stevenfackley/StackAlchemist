-- #419: a paid checkout whose generation row does not exist (deleted, or a session created outside
-- the app with no generationId) used to violate transactions.generation_id's FK. That rolled back the
-- whole call, the stripe_events insert included, so Stripe redelivered the event for days and the
-- payment was never recorded. Now the payment is recorded with a NULL generation_id, the event is
-- recorded, and the caller learns the row is missing from a NULL mode (generations.mode is NOT NULL,
-- so a real row never returns one). The Engine logs a MANUAL RECOVERY line and enqueues nothing.
-- Same signature and result shape as 0001, so CREATE OR REPLACE is enough.
CREATE OR REPLACE FUNCTION stackalchemist.process_checkout_completed(
  p_event_id text, p_event_type text, p_session_id text, p_payment_intent text,
  p_generation_id uuid, p_tier integer, p_amount bigint)
RETURNS TABLE(is_new boolean, mode text, prompt text, project_type text, schema_json jsonb, personalization_json jsonb)
LANGUAGE plpgsql AS $$
declare
  v_new boolean;
  v_generation_exists boolean;
begin
  insert into stackalchemist.stripe_events (id, type) values (p_event_id, p_event_type)
  on conflict (id) do nothing;
  v_new := found;
  if not v_new then
    return query select false, null::text, null::text, null::text, null::jsonb, null::jsonb;
    return;
  end if;

  select exists (select 1 from stackalchemist.generations g where g.id = p_generation_id)
    into v_generation_exists;

  if v_generation_exists then
    update stackalchemist.generations g set tier = p_tier where g.id = p_generation_id;
  end if;

  insert into stackalchemist.transactions
    (stripe_session_id, stripe_payment_intent, tier, amount, status, generation_id, last_stripe_event_id, updated_at)
  values
    (p_session_id, p_payment_intent, p_tier, p_amount::int, 'completed',
     case when v_generation_exists then p_generation_id end, p_event_id, now())
  on conflict (stripe_session_id) do update set
    stripe_payment_intent = excluded.stripe_payment_intent,
    tier                  = excluded.tier,
    amount                = excluded.amount,
    status                = excluded.status,
    generation_id         = excluded.generation_id,
    last_stripe_event_id  = excluded.last_stripe_event_id,
    updated_at            = now();

  if v_generation_exists then
    return query
      select true, g.mode, g.prompt, g.project_type, g.schema_json, g.personalization_json
      from stackalchemist.generations g where g.id = p_generation_id;
  else
    return query select true, null::text, null::text, null::text, null::jsonb, null::jsonb;
  end if;
end;
$$;
