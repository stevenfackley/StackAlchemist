import type { Generation, GenerationSchema, PersonalizationData, ProjectType, Tier } from "@/lib/types";

/** The profile columns the settings page reads (not the full stored row). */
export interface ProfileSettingsRow {
  email: string;
  api_key_override: string | null;
  preferred_model: string | null;
}

export interface ProfileUpsert {
  id: string;
  email: string;
  preferred_model: string;
  /** undefined = leave the stored key alone; null = clear it. */
  api_key_override?: string | null;
}

export interface NewGeneration {
  mode: "simple" | "advanced";
  tier: Tier;
  prompt: string | null;
  project_type: ProjectType;
  schema_json: GenerationSchema | null;
  personalization_json: PersonalizationData | null;
  user_id: string;
}

/** Thrown by every store method on a failed query; actions.ts maps it to the same user-facing messages it shows today. */
export class DataStoreError extends Error {
  constructor(message: string, cause?: unknown) {
    super(message, { cause });
    this.name = "DataStoreError";
  }
}

export interface DataStore {
  readonly kind: "drizzle" | "supabase";
  getProfile(userId: string): Promise<ProfileSettingsRow | null>;
  upsertProfile(profile: ProfileUpsert): Promise<void>;
  /**
   * Insert the profile row for a first-seen user; a no-op when it exists (settings
   * live there and must not be overwritten). qavren-db has no auth.users trigger,
   * and generations.user_id references profiles.id.
   */
  ensureProfile(profile: { id: string; email: string }): Promise<void>;
  /** Same predicate as the enforce_free_generation_quota trigger: tier 0, not failed, created this calendar month (UTC). */
  countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number>;
  /** Returns the row as stored: status `pending`, `attempt_count` 0, column defaults applied. */
  insertGeneration(gen: NewGeneration): Promise<Generation>;
  /**
   * The row with this id, only if it belongs to `userId`: the owner scope is in
   * the query itself, so no caller can forget it (result pages are owner-only,
   * decided 2026-09-30, phase B decision 3). Someone else's id, an unknown id
   * and a malformed id or owner id all return null, the same answer, so the
   * read is not an existence oracle. There is deliberately no unscoped by-id
   * read in this interface.
   */
  getGenerationForUser(id: string, userId: string): Promise<Generation | null>;
  /**
   * Atomic compare-and-set, scoped in SQL: only the owner's failed row flips
   * back to pending. Returns whether a row changed, so of two concurrent
   * retries exactly one gets `true`.
   */
  resetForRetry(id: string, userId: string): Promise<boolean>;
  /**
   * The caller's rows ordered by `created_at` descending. `total` is the
   * caller's full row count, independent of `offset` and `limit`.
   */
  listMyGenerations(userId: string, offset: number, limit: number): Promise<{ generations: Generation[]; total: number }>;
  /**
   * `total` is all of the caller's rows, `completed` those with status
   * `success`, `inProgress` those with a status that is neither `success`
   * nor `failed`.
   */
  generationStats(userId: string): Promise<{ total: number; completed: number; inProgress: number }>;
}
