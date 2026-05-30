-- ============================================================================
-- Widen generations.status CHECK to include the worker's in-flight states
-- ============================================================================
-- The compile worker reports progress through the engine's GenerationState enum
-- (SupabaseDeliveryService writes `state.ToString().ToLowerInvariant()`): it
-- emits "generating" (on a build-retry), "packing", and "uploading". The original
-- constraint (migration 20260404000003) rejected all three, so PostgREST returned
-- 400 and the delivery service silently swallowed it — those transitions never
-- reached the row and the UI never showed packing/uploading progress.
--
-- Adding them lets the full lifecycle persist:
--   pending → extracting_schema → generating_code → building
--           → packing → uploading → success    (or → failed at any point)
-- "generating" is the build-retry alias of "generating_code".

alter table public.generations
  drop constraint if exists generations_status_check;

alter table public.generations
  add constraint generations_status_check
  check (status in (
    'pending',
    'extracting_schema',
    'generating_code',
    'generating',
    'building',
    'packing',
    'uploading',
    'success',
    'failed'
  ));
