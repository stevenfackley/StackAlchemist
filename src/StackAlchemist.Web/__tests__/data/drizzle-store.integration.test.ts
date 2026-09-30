// @vitest-environment node
import { randomUUID } from "node:crypto";
import postgres from "postgres";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { DrizzleStore } from "@/lib/data/drizzle-store";
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
    await sql`delete from stackalchemist.generations where user_id in (${alice}, ${bob})`;
    await sql`delete from stackalchemist.profiles where id in (${alice}, ${bob})`;
    await sql.end();
    await closeDb();
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

  it("getGenerationById returns null for an unknown or malformed id", async () => {
    expect(await store.getGenerationById(randomUUID())).toBeNull();
    expect(await store.getGenerationById("demo-simple-123")).toBeNull();
  });
});

function monthStart() {
  const n = new Date();
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), 1));
}
