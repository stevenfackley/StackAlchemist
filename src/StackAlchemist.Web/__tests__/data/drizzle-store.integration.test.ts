// @vitest-environment node
import { randomUUID } from "node:crypto";
import postgres from "postgres";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { DrizzleStore } from "@/lib/data/drizzle-store";
import { DataStoreError } from "@/lib/data/store";
import { closeDb } from "@/db";

const url = process.env.TEST_DATABASE_URL;

describe.skipIf(!url)("DrizzleStore against real Postgres", () => {
  // Built in beforeAll, not in the describe body: vitest evaluates a skipped
  // describe's body, and getDb() throws when the URL is missing.
  let store: DrizzleStore;
  let sql: ReturnType<typeof postgres>;
  const alice = randomUUID();
  const bob = randomUUID();

  beforeAll(async () => {
    store = new DrizzleStore(url);
    sql = postgres(url!, { max: 1 });
    await sql`insert into stackalchemist.profiles (id, email) values (${alice}, 'alice@example.test'), (${bob}, 'bob@example.test')`;
  });
  beforeEach(async () => {
    await sql`delete from stackalchemist.generations where user_id in (${alice}, ${bob})`;
  });
  afterAll(async () => {
    try {
      // sql is unassigned when beforeAll threw before connecting.
      if (sql) {
        await sql`delete from stackalchemist.generations where user_id in (${alice}, ${bob})`;
        await sql`delete from stackalchemist.profiles where id in (${alice}, ${bob})`;
      }
    } finally {
      await sql?.end();
      await closeDb();
    }
  });

  const gen = (user_id: string, tier: 0 | 1 = 1) => store.insertGeneration({
    mode: "simple", tier, prompt: "p", project_type: "DotNetNextJs", schema_json: null, personalization_json: null, user_id,
  });

  it("insert returns the row with defaults applied", async () => {
    const g = await gen(alice);
    expect(g.status).toBe("pending");
    expect(g.attempt_count).toBe(0);
    // ISO with a Z, the shape PostgREST rows had; Postgres text form breaks Safari's Date parser.
    expect(g.created_at).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$/);
  });

  it("list and stats are scoped to the caller", async () => {
    await gen(alice); await gen(alice); await gen(bob);
    const a = await store.listMyGenerations(alice, 0, 20);
    const b = await store.listMyGenerations(bob, 0, 20);
    expect(a.total).toBe(2);
    expect(b.total).toBe(1);
    expect(a.generations.every((g) => g.user_id === alice)).toBe(true);
    expect((await store.generationStats(bob)).total).toBe(1);
  });

  it("list pages by offset and limit, newest first, with total independent of the page", async () => {
    const oldest = await gen(alice);
    const middle = await gen(alice);
    const newest = await gen(alice);
    // Pin distinct timestamps so the order does not depend on insert timing.
    await sql`update stackalchemist.generations set created_at = now() - interval '3 days' where id = ${oldest.id}`;
    await sql`update stackalchemist.generations set created_at = now() - interval '2 days' where id = ${middle.id}`;
    await sql`update stackalchemist.generations set created_at = now() - interval '1 day' where id = ${newest.id}`;

    const page = await store.listMyGenerations(alice, 1, 1);
    expect(page.generations.map((g) => g.id)).toEqual([middle.id]);
    expect(page.total).toBe(3);

    const all = await store.listMyGenerations(alice, 0, 20);
    expect(all.generations.map((g) => g.id)).toEqual([newest.id, middle.id, oldest.id]);
  });

  it("stats count completed and in-progress rows separately", async () => {
    const done = await gen(alice);
    const failed = await gen(alice);
    await gen(alice); // stays pending
    await sql`update stackalchemist.generations set status = 'success' where id = ${done.id}`;
    await sql`update stackalchemist.generations set status = 'failed' where id = ${failed.id}`;
    expect(await store.generationStats(alice)).toEqual({ total: 3, completed: 1, inProgress: 1 });
  });

  it("retry cannot flip another user's failed row", async () => {
    const g = await gen(alice);
    await sql`update stackalchemist.generations set status = 'failed' where id = ${g.id}`;
    expect(await store.resetForRetry(g.id, bob)).toBe(false);
    expect(await store.resetForRetry(g.id, alice)).toBe(true);
    expect((await store.getGenerationById(g.id))?.status).toBe("pending");
  });

  it("retry only flips failed rows", async () => {
    const g = await gen(alice);
    expect(await store.resetForRetry(g.id, alice)).toBe(false);
  });

  it("free-tier quota: the trigger rejects the sixth non-failed tier-0 build this month", async () => {
    for (let i = 0; i < 5; i++) await gen(alice, 0);
    expect(await store.countFreeGenerationsThisMonth(alice, monthStart())).toBe(5);
    await expect(gen(alice, 0)).rejects.toThrow(/Free generation limit reached/);
    expect(await store.countFreeGenerationsThisMonth(bob, monthStart())).toBe(0);
  });

  it("free-tier quota: the count and the trigger agree on which rows use the allowance", async () => {
    const rows = [];
    for (let i = 0; i < 5; i++) rows.push(await gen(alice, 0));
    await gen(alice, 1); // paid tier: never counted
    await sql`update stackalchemist.generations set status = 'failed' where id = ${rows[0].id}`;
    // One second before the UTC month start: last month's build, not counted.
    await sql`update stackalchemist.generations set created_at = date_trunc('month', now(), 'UTC') - interval '1 second' where id = ${rows[1].id}`;

    expect(await store.countFreeGenerationsThisMonth(alice, monthStart())).toBe(3);
    // The trigger counts the same three, so two more free builds fit under the limit of 5.
    // A trigger that ignored any one exclusion would see 4 and reject the second insert.
    await expect(gen(alice, 0)).resolves.toMatchObject({ status: "pending", tier: 0 });
    await expect(gen(alice, 0)).resolves.toMatchObject({ status: "pending", tier: 0 });
    expect(await store.countFreeGenerationsThisMonth(alice, monthStart())).toBe(5);
    // Now the allowance is exactly used up, so the next counted build is refused.
    await expect(gen(alice, 0)).rejects.toThrow(/Free generation limit reached/);
  });

  it("profile upsert: undefined leaves the key, null clears it", async () => {
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-sonnet-4-6", api_key_override: "v1:cipher" });
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-3-5-haiku-20241022" });
    expect((await store.getProfile(alice))?.api_key_override).toBe("v1:cipher");
    await store.upsertProfile({ id: alice, email: "alice@example.test", preferred_model: "claude-sonnet-4-6", api_key_override: null });
    expect((await store.getProfile(alice))?.api_key_override).toBeNull();
  });

  it("getProfile returns only the settings columns, and null for an unknown or malformed id", async () => {
    expect(await store.getProfile(bob)).toEqual({ email: "bob@example.test", api_key_override: null, preferred_model: "claude-sonnet-4-6" });
    expect(await store.getProfile(randomUUID())).toBeNull();
    expect(await store.getProfile("not-a-uuid")).toBeNull();
  });

  it("profile upsert inserts an unseen id, storing an omitted key as null", async () => {
    const carol = randomUUID();
    try {
      await store.upsertProfile({ id: carol, email: "carol@example.test", preferred_model: "claude-3-5-haiku-20241022", api_key_override: undefined });
      expect(await store.getProfile(carol)).toEqual({ email: "carol@example.test", api_key_override: null, preferred_model: "claude-3-5-haiku-20241022" });
    } finally {
      await sql`delete from stackalchemist.profiles where id = ${carol}`;
    }
  });

  it("getGenerationById returns null for an unknown or malformed id", async () => {
    expect(await store.getGenerationById(randomUUID())).toBeNull();
    expect(await store.getGenerationById("demo-simple-123")).toBeNull();
  });

  it("getGenerationById is deliberately unscoped: anyone with the id gets the row", async () => {
    const g = await gen(alice);
    // Phase B decision 3: the by-id read stays unscoped (the result page is reachable by link);
    // owner-only result pages are an open product question. Callers that need ownership check user_id.
    expect((await store.getGenerationById(g.id))?.user_id).toBe(alice);
  });

  it("a failed query rejects with the Postgres error as cause, without the SQL params", async () => {
    // A malformed uuid makes Postgres raise 22P02; Drizzle wraps it in a DrizzleQueryError whose text carries the bound params.
    const err = await store.listMyGenerations("not-a-uuid", 0, 20).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(DataStoreError);
    const { message, cause } = err as DataStoreError;
    expect((cause as { code?: string }).code).toBe("22P02");
    expect(message).not.toMatch(/params:/);
    expect(String((cause as Error).message)).not.toMatch(/params:/);
  });
});

function monthStart() {
  const n = new Date();
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), 1));
}
