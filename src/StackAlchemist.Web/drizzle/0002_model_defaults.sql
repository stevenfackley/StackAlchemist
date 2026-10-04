ALTER TABLE "stackalchemist"."profiles" ALTER COLUMN "preferred_model" SET DEFAULT 'claude-sonnet-5-5';
--> statement-breakpoint
-- Remap stored choices that are no longer offered (2026-10-03 model bump). Each old id moves to
-- the current model from the same provider, so a BYOK user keeps routing to the key they stored.
UPDATE "stackalchemist"."profiles"
SET "preferred_model" = CASE "preferred_model"
  WHEN 'claude-sonnet-4-6' THEN 'claude-sonnet-5-5'
  WHEN 'claude-3-5-sonnet-20241022' THEN 'claude-sonnet-5-5'
  WHEN 'claude-3-5-haiku-20241022' THEN 'claude-haiku-4-5'
  WHEN 'openai/gpt-4o-mini' THEN 'openai/gpt-6.1-sol'
  WHEN 'openrouter/anthropic/claude-3.5-sonnet' THEN 'openrouter/anthropic/claude-sonnet-5.5'
END
WHERE "preferred_model" IN (
  'claude-sonnet-4-6',
  'claude-3-5-sonnet-20241022',
  'claude-3-5-haiku-20241022',
  'openai/gpt-4o-mini',
  'openrouter/anthropic/claude-3.5-sonnet'
);
