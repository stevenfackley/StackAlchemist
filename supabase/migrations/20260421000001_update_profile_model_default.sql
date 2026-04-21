-- Align stored profile defaults with the concrete Claude 3.5 Sonnet model id
-- exposed in the dashboard BYOK settings UI.

alter table public.profiles
  alter column preferred_model set default 'claude-3-5-sonnet-20241022';

update public.profiles
set preferred_model = 'claude-3-5-sonnet-20241022'
where preferred_model = 'claude-3-5-sonnet';
