alter table public.generations
  add column if not exists input_tokens integer not null default 0,
  add column if not exists output_tokens integer not null default 0,
  add column if not exists model_used text;

create or replace function public.append_build_log(gen_id uuid, chunk text)
returns void
language plpgsql
security definer
as $$
begin
  update public.generations
  set
    build_log = case
      when build_log is null or build_log = '' then chunk
      else build_log || E'\n' || chunk
    end,
    updated_at = now()
  where id = gen_id;
end;
$$;

create or replace function public.increment_token_usage(
  gen_id uuid,
  input_delta integer,
  output_delta integer,
  model_name text)
returns void
language plpgsql
security definer
as $$
begin
  update public.generations
  set
    input_tokens = coalesce(input_tokens, 0) + greatest(coalesce(input_delta, 0), 0),
    output_tokens = coalesce(output_tokens, 0) + greatest(coalesce(output_delta, 0), 0),
    model_used = coalesce(nullif(model_name, ''), model_used),
    updated_at = now()
  where id = gen_id;
end;
$$;

grant execute on function public.append_build_log(uuid, text) to service_role;
grant execute on function public.increment_token_usage(uuid, integer, integer, text) to service_role;
