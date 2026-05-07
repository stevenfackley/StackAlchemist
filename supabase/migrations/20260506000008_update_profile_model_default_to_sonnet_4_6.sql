-- Bump the column-level default for profiles.preferred_model from
-- the retired claude-3-5-sonnet-20241022 to claude-sonnet-4-6.
-- Existing rows are left untouched: a user who explicitly chose 3.5
-- (or had it written before they reviewed BYOK options) keeps their
-- choice. The legacy value is still in ALLOWED_PROFILE_MODELS so
-- normalizePreferredModel does not silently rewrite it.

-- Trade-off: users who never opened BYOK and inherited the 3.5 default will
-- continue to hit 404 from Anthropic until they manually update. We're choosing
-- "respect explicit user choice" over "fix everyone in bulk" because we can't
-- distinguish inherited-default from explicit-choice in the current schema.
-- A follow-up engine-side fallback (if model returns 404, fall back to default
-- and surface a toast) would close this gap without losing user agency.

alter table public.profiles
  alter column preferred_model set default 'claude-sonnet-4-6';
