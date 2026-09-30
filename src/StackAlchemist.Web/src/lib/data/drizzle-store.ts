import type { Generation } from "@/lib/types";
import type { DataStore, NewGeneration, ProfileSettingsRow, ProfileUpsert } from "./store";

const NOT_YET = "DrizzleStore lands in Task 3";

/** Placeholder so the seam type-checks; Task 3 replaces every method with real Drizzle queries. */
export class DrizzleStore implements DataStore {
  readonly kind = "drizzle" as const;

  async getProfile(_userId: string): Promise<ProfileSettingsRow | null> {
    throw new Error(NOT_YET);
  }

  async upsertProfile(_profile: ProfileUpsert): Promise<void> {
    throw new Error(NOT_YET);
  }

  async countFreeGenerationsThisMonth(_userId: string, _monthStartUtc: Date): Promise<number> {
    throw new Error(NOT_YET);
  }

  async insertGeneration(_gen: NewGeneration): Promise<Generation> {
    throw new Error(NOT_YET);
  }

  async getGenerationById(_id: string): Promise<Generation | null> {
    throw new Error(NOT_YET);
  }

  async resetForRetry(_id: string, _userId: string): Promise<boolean> {
    throw new Error(NOT_YET);
  }

  async listMyGenerations(
    _userId: string,
    _offset: number,
    _limit: number
  ): Promise<{ generations: Generation[]; total: number }> {
    throw new Error(NOT_YET);
  }

  async generationStats(_userId: string): Promise<{ total: number; completed: number; inProgress: number }> {
    throw new Error(NOT_YET);
  }
}
