import "server-only";
import type { DataStore } from "./store";
import { SupabaseStore } from "./supabase-store";
import { DrizzleStore } from "./drizzle-store";

export type { DataStore, NewGeneration, ProfileRow, ProfileUpsert } from "./store";
export { DataStoreError } from "./store";

/**
 * Postgres (qavren-db) when DATABASE_URL is set, Supabase otherwise. The
 * decision is made per call, not at module load, so tests can flip the env.
 * Phase E sets DATABASE_URL in prod; phase F deletes SupabaseStore.
 */
export function getDataStore(): DataStore {
  return process.env.DATABASE_URL ? new DrizzleStore() : new SupabaseStore();
}
