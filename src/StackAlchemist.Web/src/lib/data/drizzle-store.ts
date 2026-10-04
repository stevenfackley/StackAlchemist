import { DrizzleQueryError, and, count, desc, eq, gte, ne, sql } from "drizzle-orm";
import postgres from "postgres";
import { getDb, type Db } from "@/db";
import { generations, profiles } from "@/db/schema";
import type { Generation } from "@/lib/types";
import { DataStoreError, type DataStore, type NewGeneration, type ProfileSettingsRow, type ProfileUpsert } from "./store";

// A malformed id would make Postgres raise 22P02 (invalid uuid); it is simply not found.
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Drizzle 0.45 wraps every driver failure in a DrizzleQueryError whose message is
 * `Failed query: <sql>\nparams: <values>`. Unwrap to the driver's own error so
 * callers get the Postgres message (the quota trigger's text) and the wrapper's
 * `params:` text (prompts, the encrypted API key) never reaches a DataStoreError.
 * What is NOT stripped: a PostgresError's `detail` for a check or not-null
 * violation reads "Failing row contains (...)" with column values. Fails closed:
 * a wrapper with no cause becomes a bare Error rather than being passed through.
 */
function driverError(e: unknown): unknown {
  return e instanceof DrizzleQueryError ? (e.cause ?? new Error("query failed")) : e;
}

/** Drizzle over postgres-js against the qavren-db `stackalchemist` schema. */
export class DrizzleStore implements DataStore {
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

  async ensureProfile({ id, email }: { id: string; email: string }): Promise<void> {
    try {
      await this.db.insert(profiles).values({ id, email }).onConflictDoNothing({ target: profiles.id });
    } catch (e) { throw new DataStoreError("profiles insert failed", driverError(e)); }
  }

  async countFreeGenerationsThisMonth(userId: string, monthStartUtc: Date): Promise<number> {
    try {
      const [{ n }] = await this.db.select({ n: count() }).from(generations).where(and(
        eq(generations.user_id, userId), eq(generations.tier, 0), ne(generations.status, "failed"),
        gte(generations.created_at, monthStartUtc.toISOString()),
      ));
      return n;
    } catch (e) { throw new DataStoreError("generations count failed", driverError(e)); }
  }

  async insertGeneration(gen: NewGeneration): Promise<Generation> {
    let row: Generation | undefined;
    try {
      // Explicit columns, not `.values(gen)`: a wider object must never be able
      // to set status, attempt_count, transaction_id or preview_files_json here:
      // the column defaults give a pending row with attempt_count 0.
      [row] = await this.db.insert(generations).values({
        mode: gen.mode,
        tier: gen.tier,
        prompt: gen.prompt,
        project_type: gen.project_type,
        schema_json: gen.schema_json,
        personalization_json: gen.personalization_json,
        user_id: gen.user_id,
      }).returning();
    } catch (e) {
      // The quota trigger raises check_violation (23514) with its own message;
      // surface that text so callers (and the test) can see it. Only a Postgres
      // error's message is used, and only when it has one: a refused connection
      // is an AggregateError with an empty message. instanceof, not a name check:
      // Next bundles postgres-js into the server build and a minifier may rename
      // the class, which would silently drop this text from prod logs.
      const cause = driverError(e);
      const fromPostgres = cause instanceof postgres.PostgresError && cause.message !== "";
      throw new DataStoreError(fromPostgres ? cause.message : "generations insert failed", cause);
    }
    if (!row) throw new DataStoreError("generations insert returned no row");
    return row;
  }

  async getGenerationForUser(id: string, userId: string): Promise<Generation | null> {
    if (!UUID.test(id) || !UUID.test(userId)) return null;
    try {
      const [row] = await this.db.select().from(generations)
        .where(and(eq(generations.id, id), eq(generations.user_id, userId))).limit(1);
      return row ?? null;
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
        // id breaks created_at ties so a page boundary never repeats or skips a row.
        this.db.select().from(generations).where(eq(generations.user_id, userId))
          .orderBy(desc(generations.created_at), desc(generations.id)).limit(limit).offset(offset),
        this.db.select({ n: count() }).from(generations).where(eq(generations.user_id, userId)),
      ]);
      return { generations: rows, total: n };
    } catch (e) { throw new DataStoreError("generations list failed", driverError(e)); }
  }

  async generationStats(userId: string) {
    try {
      const [r] = await this.db.select({
        total: count(),
        completed: sql<number>`count(*) filter (where ${generations.status} = 'success')`.mapWith(Number),
        inProgress: sql<number>`count(*) filter (where ${generations.status} not in ('success', 'failed'))`.mapWith(Number),
      }).from(generations).where(eq(generations.user_id, userId));
      return { total: r.total, completed: r.completed, inProgress: r.inProgress };
    } catch (e) { throw new DataStoreError("generations stats failed", driverError(e)); }
  }
}
