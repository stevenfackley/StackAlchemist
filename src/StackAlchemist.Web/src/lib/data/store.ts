import type { Generation, GenerationSchema, GenerationSummary, PersonalizationData, ProjectType, Tier } from "@/lib/types";

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
  /** The caller's paid-tier rows created since `since` and still `pending`: made for a checkout that has not been paid (or not yet picked up). */
  countUnpaidPendingGenerations(userId: string, since: Date): Promise<number>;
  /** Returns the row as stored: status `pending`, `attempt_count` 0, column defaults applied. */
  insertGeneration(gen: NewGeneration): Promise<Generation>;
  /**
   * The row with this id, only if it belongs to `userId`: the owner scope is in
   * the query itself, so no caller can forget it (result pages are owner-only,
   * decided 2026-09-30, phase B decision 3). Someone else's id, an unknown id
   * and a malformed id or owner id all return null, the same answer, so the
   * read is not an existence oracle. There is deliberately no unscoped by-id
   * read in this interface. The row carries `payment_status`: the status of
   * the payment behind the build (null when it has no transaction).
   */
  getGenerationForUser(id: string, userId: string): Promise<Generation | null>;
  /**
   * Atomic compare-and-set, scoped in SQL: only the owner's failed row with a
   * retry left (`retry_count` under MAX_RETRIES) flips back to pending, and
   * the retry is counted in the same write. Returns whether a row changed, so
   * of two concurrent retries exactly one gets `true` and one retry is spent.
   * Counted before the Engine is called, because the Engine reads the count as
   * soon as it accepts the build.
   */
  resetForRetry(id: string, userId: string): Promise<boolean>;
  /**
   * Gives back the retry that `resetForRetry` counted, when the Engine did not
   * accept the build: a retry that started nothing is not one of the owner's
   * three. Never goes below zero.
   */
  uncountRetry(id: string, userId: string): Promise<void>;
  /**
   * The caller's rows ordered by `created_at` descending, as list summaries:
   * the large columns (schema, build log, preview files) are not read. `total`
   * is the caller's full row count, independent of `offset` and `limit`.
   */
  listMyGenerations(userId: string, offset: number, limit: number): Promise<{ generations: GenerationSummary[]; total: number }>;
  /**
   * `total` is all of the caller's rows, `completed` those with status
   * `success`, `inProgress` those with a status that is neither `success`
   * nor `failed`.
   */
  generationStats(userId: string): Promise<{ total: number; completed: number; inProgress: number }>;
}
