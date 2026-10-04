import "server-only";
import type { DataStore } from "./store";
import { DrizzleStore } from "./drizzle-store";

export type { DataStore, NewGeneration, ProfileSettingsRow, ProfileUpsert } from "./store";
export { DataStoreError } from "./store";

/**
 * The qavren-db store (Postgres, schema `stackalchemist`). Callers check
 * hasDataStoreConfig() (DATABASE_URL set) first; without it the actions run in
 * demo mode and never get here.
 */
export function getDataStore(): DataStore {
  return new DrizzleStore();
}
