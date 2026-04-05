-- Add personalization_json column to store wizard answers collected before generation.
-- Stored as jsonb alongside schema_json so the engine can read both in one query.
ALTER TABLE public.generations
    ADD COLUMN IF NOT EXISTS personalization_json jsonb DEFAULT NULL;

COMMENT ON COLUMN public.generations.personalization_json IS
    'Personalization wizard answers: business identity, color scheme, domain context, feature flags.';
