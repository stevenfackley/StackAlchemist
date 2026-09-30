import { DrizzleQueryError, and, count, desc, eq, gte, ne, sql } from "drizzle-orm";
import { getDb, type Db } from "@/db";
import { generations, profiles } from "@/db/schema";
import type { Generation } from "@/lib/types";
import { DataStoreError, type DataStore, type NewGeneration, type ProfileSettingsRow, type ProfileUpsert } from "./store";

// Same guard SupabaseStore applies: both stores return null for a malformed id.
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Drizzle 0.45 wraps every driver failure in a DrizzleQueryError whose message
 * is `Failed query: <sql>\nparams: <values>`. Unwrap to the Postgres error: its
 * message is what callers need (the quota trigger's text), and the wrapper's
 * bound params (prompts, the encrypted API key) should not end up in logs.
 */
function driverError(e: unknown): unknown {
  return e instanceof DrizzleQueryError && e.cause ? e.cause : e;
}

/** Drizzle over postgres-js against the qavren-db `stackalchemist` schema. */
export class DrizzleStore implements DataStore {
  readonly kind = "drizzle" as const;
  private readonly db: Db;

  constructor(url?: string) {
    this.db = getDb(url);
  }

  async getProfile(userId: string): Promise<ProfileSettingsRow | null> {
    if (!UUID.test(userId)) return null;
    try {
      const [row] = await this.db
        .select({ email: profiles.email, api_key_override: profiles.api_key_override, preferred_model: profiles.preferred_model })
        .from(profiles).where(eq(profiles.id, userId)).limit(1);
      return row ?? null;
    } catch (e) { throw new DataStoreError("profiles select failed", driverError(e)); }
  }

  async upsertProfile(p: ProfileUpsert): Promise<void> {
    const set: Partial<typeof profiles.$inferInsert> = { email: p.email, preferred_model: p.preferred_model };
    if (p.api_key_override !== undefined) set.api_key_override = p.api_key_override;
    try {
      await this.db.insert(profiles)
        .values({ id: p.id, email: p.email, preferred_model: p.preferred_model, api_key_override: p.api_key_override ?? null })
        .onConflictDoUpdate({ target: profiles.id, set });
    } catch (e) { throw new DataStoreError("profiles upsert failed", driverError(e)); }
  }

  async countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number> {
    try {
      const [{ n }] = await this.db.select({ n: count() }).from(generations).where(and(
        eq(generations.user_id, userId), eq(generations.tier, 0), ne(generations.status, "failed"),
        gte(generations.created_at, monthStartUtc.toISOString()),
      ));
      return Number(n);
    } catch (e) { throw new DataStoreError("generations count failed", driverError(e)); }
  }

  async insertGeneration(gen: NewGeneration): Promise<Generation> {
    try {
      const [row] = await this.db.insert(generations).values(gen).returning();
      return row as Generation;
    } catch (e) {
      // The quota trigger raises check_violation (23514) with its own message;
      // surface that text so callers (and the test) can see it.
      const cause = driverError(e);
      throw new DataStoreError(cause instanceof Error ? cause.message : "generations insert failed", cause);
    }
  }

  async getGenerationById(id: string): Promise<Generation | null> {
    if (!UUID.test(id)) return null;
    try {
      const [row] = await this.db.select().from(generations).where(eq(generations.id, id)).limit(1);
      return (row as Generation | undefined) ?? null;
    } catch (e) { throw new DataStoreError("generations select failed", driverError(e)); }
  }

  async resetForRetry(id: string, userId: string): Promise<boolean> {
    if (!UUID.test(id) || !UUID.test(userId)) return false;
    try {
      const rows = await this.db.update(generations)
        .set({ status: "pending", error_message: null })
        .where(and(eq(generations.id, id), eq(generations.user_id, userId), eq(generations.status, "failed")))
        .returning({ id: generations.id });
      return rows.length === 1;
    } catch (e) { throw new DataStoreError("generations update failed", driverError(e)); }
  }

  async listMyGenerations(userId: string, offset: number, limit: number) {
    try {
      const [rows, [{ n }]] = await Promise.all([
        this.db.select().from(generations).where(eq(generations.user_id, userId))
          .orderBy(desc(generations.created_at)).limit(limit).offset(offset),
        this.db.select({ n: count() }).from(generations).where(eq(generations.user_id, userId)),
      ]);
      return { generations: rows as Generation[], total: Number(n) };
    } catch (e) { throw new DataStoreError("generations list failed", driverError(e)); }
  }

  async generationStats(userId: string) {
    try {
      const [r] = await this.db.select({
        total: count(),
        completed: sql<number>`count(*) filter (where ${generations.status} = 'success')`,
        inProgress: sql<number>`count(*) filter (where ${generations.status} not in ('success', 'failed'))`,
      }).from(generations).where(eq(generations.user_id, userId));
      return { total: Number(r.total), completed: Number(r.completed), inProgress: Number(r.inProgress) };
    } catch (e) { throw new DataStoreError("generations stats failed", driverError(e)); }
  }
}
