import type { Generation, GenerationSchema, PersonalizationData, ProjectType, Tier } from "@/lib/types";

export interface ProfileRow {
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
  constructor(message: string, readonly cause?: unknown) {
    super(message);
    this.name = "DataStoreError";
  }
}

export interface DataStore {
  readonly kind: "drizzle" | "supabase";
  getProfile(userId: string): Promise<ProfileRow | null>;
  upsertProfile(profile: ProfileUpsert): Promise<void>;
  /** Same predicate as the enforce_free_generation_quota trigger: tier 0, not failed, created this calendar month (UTC). */
  countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number>;
  insertGeneration(gen: NewGeneration): Promise<Generation>;
  /** By id, deliberately unscoped: parity with today's service-role read (the /generate/[id] page is reachable by link). */
  getGenerationById(id: string): Promise<Generation | null>;
  /** Scoped in SQL: only the owner's failed row flips back to pending. Returns whether a row changed. */
  resetForRetry(id: string, userId: string): Promise<boolean>;
  listMyGenerations(userId: string, offset: number, limit: number): Promise<{ generations: Generation[]; total: number }>;
  generationStats(userId: string): Promise<{ total: number; completed: number; inProgress: number }>;
}
