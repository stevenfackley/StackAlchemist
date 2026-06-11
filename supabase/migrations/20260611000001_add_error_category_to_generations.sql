-- ============================================================================
-- error_category: machine-readable failure class set by the Engine alongside
-- error_message, so the frontend can render actionable guidance per class
-- instead of dumping raw error text.
-- ============================================================================

alter table public.generations
  add column if not exists error_category text
  check (error_category is null or error_category in
    ('quota', 'schema', 'build', 'rate_limit', 'network', 'internal'));

comment on column public.generations.error_category is
  'Failure class written by the Engine when status=failed. NULL for non-failed rows and for failures predating this column.';
