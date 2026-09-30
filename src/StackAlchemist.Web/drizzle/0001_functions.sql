CREATE OR REPLACE FUNCTION stackalchemist.set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
begin
  new.updated_at = now();
  return new;
end;
$$;
--> statement-breakpoint
CREATE TRIGGER generations_updated_at
BEFORE UPDATE ON stackalchemist.generations
FOR EACH ROW EXECUTE FUNCTION stackalchemist.set_updated_at();
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.enforce_free_generation_quota() RETURNS trigger
LANGUAGE plpgsql AS $$
declare
  used int;
begin
  if new.tier <> 0 then
    return new;
  end if;
  if new.user_id is null then
    raise exception 'Free-tier generations require an authenticated account'
      using errcode = 'check_violation';
  end if;
  select count(*) into used
    from stackalchemist.generations
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
--> statement-breakpoint
CREATE TRIGGER generations_enforce_free_quota
BEFORE INSERT ON stackalchemist.generations
FOR EACH ROW EXECUTE FUNCTION stackalchemist.enforce_free_generation_quota();
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.append_build_log(gen_id uuid, chunk text) RETURNS void
LANGUAGE plpgsql AS $$
begin
  update stackalchemist.generations
  set build_log = case when build_log is null or build_log = '' then chunk
                       else build_log || E'\n' || chunk end,
      updated_at = now()
  where id = gen_id;
end;
$$;
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.increment_token_usage(gen_id uuid, input_delta integer, output_delta integer, model_name text) RETURNS void
LANGUAGE plpgsql AS $$
begin
  update stackalchemist.generations
  set input_tokens  = coalesce(input_tokens, 0)  + greatest(coalesce(input_delta, 0), 0),
      output_tokens = coalesce(output_tokens, 0) + greatest(coalesce(output_delta, 0), 0),
      model_used    = coalesce(nullif(model_name, ''), model_used),
      updated_at    = now()
  where id = gen_id;
end;
$$;
--> statement-breakpoint
CREATE OR REPLACE FUNCTION stackalchemist.process_checkout_completed(
  p_event_id text, p_event_type text, p_session_id text, p_payment_intent text,
  p_generation_id uuid, p_tier integer, p_amount bigint)
RETURNS TABLE(is_new boolean, mode text, prompt text, project_type text, schema_json jsonb, personalization_json jsonb)
LANGUAGE plpgsql AS $$
declare
  v_new boolean;
begin
  insert into stackalchemist.stripe_events (id, type) values (p_event_id, p_event_type)
  on conflict (id) do nothing;
  v_new := found;
  if not v_new then
    return query select false, null::text, null::text, null::text, null::jsonb, null::jsonb;
    return;
  end if;
  update stackalchemist.generations g set tier = p_tier where g.id = p_generation_id;
  insert into stackalchemist.transactions
    (stripe_session_id, stripe_payment_intent, tier, amount, status, generation_id, last_stripe_event_id, updated_at)
  values
    (p_session_id, p_payment_intent, p_tier, p_amount::int, 'completed', p_generation_id, p_event_id, now())
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
    from stackalchemist.generations g where g.id = p_generation_id;
  if not found then
    return query select true, null::text, null::text, null::text, null::jsonb, null::jsonb;
  end if;
end;
$$;
