// @vitest-environment node
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createServerClient } from "@/lib/supabase";
import { DataStoreError } from "@/lib/data/store";
import { SupabaseStore } from "@/lib/data/supabase-store";

vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

type Call = [method: string, ...args: unknown[]];
type Query = { table: string; calls: Call[] };

const METHODS = [
  "select", "insert", "update", "upsert", "eq", "neq", "not", "gte",
  "order", "range", "single", "maybeSingle",
];

/**
 * Fake service-role client. Each `.from(table)` yields the next result in
 * order (the last one repeats). The builder records every (method, args) call,
 * returns itself, and is thenable, so awaiting it at any point resolves the
 * result, like the real supabase-js query builder.
 */
function stubClient(...results: unknown[]): { queries: Query[] } {
  const queries: Query[] = [];
  const from = vi.fn((table: string) => {
    const result = results[Math.min(queries.length, results.length - 1)];
    const calls: Call[] = [];
    const builder: Record<string, unknown> = {};
    for (const method of METHODS) {
      builder[method] = (...args: unknown[]) => {
        calls.push([method, ...args]);
        return builder;
      };
    }
    builder.then = (resolve: (v: unknown) => unknown, reject?: (r: unknown) => unknown) =>
      Promise.resolve(result).then(resolve, reject);
    queries.push({ table, calls });
    return builder;
  });
  vi.mocked(createServerClient).mockReturnValue({ from } as never);
  return { queries };
}

const UID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
const GEN_ID = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";

async function rejection(promise: Promise<unknown>): Promise<unknown> {
  try {
    await promise;
  } catch (err) {
    return err;
  }
  throw new Error("expected the promise to reject");
}

describe("SupabaseStore", () => {
  beforeEach(() => {
    vi.mocked(createServerClient).mockReset();
  });

  it("listMyGenerations scopes to the user, newest first, with the right range window", async () => {
    const rows = [{ id: "b" }, { id: "a" }];
    const { queries } = stubClient({ data: rows, error: null, count: 57 });

    const result = await new SupabaseStore().listMyGenerations(UID, 0, 20);

    expect(result).toEqual({ generations: rows, total: 57 });
    expect(queries).toHaveLength(1);
    expect(queries[0].table).toBe("generations");
    expect(queries[0].calls).toContainEqual(["eq", "user_id", UID]);
    expect(queries[0].calls).toContainEqual(["order", "created_at", { ascending: false }]);
    // id breaks created_at ties so a page boundary never repeats or skips a row.
    expect(queries[0].calls).toContainEqual(["order", "id", { ascending: false }]);
    expect(queries[0].calls).toContainEqual(["range", 0, 19]);
  });

  it("generationStats scopes all three queries to the user and maps the counts", async () => {
    const { queries } = stubClient({ count: 42, error: null }, { count: 30, error: null }, { count: 5, error: null });

    const stats = await new SupabaseStore().generationStats(UID);

    expect(stats).toEqual({ total: 42, completed: 30, inProgress: 5 });
    expect(queries).toHaveLength(3);
    for (const query of queries) {
      expect(query.table).toBe("generations");
      expect(query.calls).toContainEqual(["eq", "user_id", UID]);
    }
    expect(queries[1].calls).toContainEqual(["eq", "status", "success"]);
    expect(queries[2].calls).toContainEqual(["not", "status", "in", "(success,failed)"]);
  });

  it("countFreeGenerationsThisMonth counts the user's non-failed tier 0 rows since the month start", async () => {
    const { queries } = stubClient({ count: 3, error: null });
    const monthStart = new Date(Date.UTC(2026, 8, 1));

    const count = await new SupabaseStore().countFreeGenerationsThisMonth(UID, monthStart);

    expect(count).toBe(3);
    const { calls } = queries[0];
    expect(calls).toContainEqual(["eq", "user_id", UID]);
    expect(calls).toContainEqual(["eq", "tier", 0]);
    expect(calls).toContainEqual(["neq", "status", "failed"]);
    expect(calls).toContainEqual(["gte", "created_at", monthStart.toISOString()]);
  });

  it("getProfile returns null for a malformed id without calling the client", async () => {
    await expect(new SupabaseStore().getProfile("not-a-uuid")).resolves.toBeNull();
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("ensureProfile inserts only a new id, never overwriting an existing row's settings", async () => {
    const { queries } = stubClient({ data: null, error: null });

    await new SupabaseStore().ensureProfile({ id: UID, email: "a@example.test" });

    expect(queries).toHaveLength(1);
    expect(queries[0].table).toBe("profiles");
    expect(queries[0].calls).toEqual([
      ["upsert", { id: UID, email: "a@example.test" }, { onConflict: "id", ignoreDuplicates: true }],
    ]);
  });

  describe("resetForRetry", () => {
    it("reports false for a malformed generation or owner id without calling the client", async () => {
      await expect(new SupabaseStore().resetForRetry("not-a-uuid", UID)).resolves.toBe(false);
      await expect(new SupabaseStore().resetForRetry(GEN_ID, "not-a-uuid")).resolves.toBe(false);
      expect(createServerClient).not.toHaveBeenCalled();
    });

    it("is scoped to the owner's failed row and reports true when exactly one row changed", async () => {
      const { queries } = stubClient({ data: [{ id: GEN_ID }], error: null });

      await expect(new SupabaseStore().resetForRetry(GEN_ID, UID)).resolves.toBe(true);

      const { calls } = queries[0];
      expect(calls).toContainEqual(["update", { status: "pending", error_message: null }]);
      expect(calls).toContainEqual(["eq", "id", GEN_ID]);
      expect(calls).toContainEqual(["eq", "user_id", UID]);
      expect(calls).toContainEqual(["eq", "status", "failed"]);
    });

    it("reports false when no row matched", async () => {
      stubClient({ data: [], error: null });
      await expect(new SupabaseStore().resetForRetry(GEN_ID, UID)).resolves.toBe(false);
    });

    it("reports false unless exactly one row changed", async () => {
      stubClient({ data: [{ id: GEN_ID }, { id: GEN_ID }], error: null });
      await expect(new SupabaseStore().resetForRetry(GEN_ID, UID)).resolves.toBe(false);
    });
  });

  describe("getGenerationForUser", () => {
    it("returns null for a malformed generation or owner id without calling the client", async () => {
      await expect(new SupabaseStore().getGenerationForUser("not-a-uuid", UID)).resolves.toBeNull();
      await expect(new SupabaseStore().getGenerationForUser(GEN_ID, "not-a-uuid")).resolves.toBeNull();
      expect(createServerClient).not.toHaveBeenCalled();
    });

    it("is scoped to the owner in the query itself and returns null when no row matches", async () => {
      const { queries } = stubClient({ data: null, error: null });

      await expect(new SupabaseStore().getGenerationForUser(GEN_ID, UID)).resolves.toBeNull();

      expect(queries[0].calls).toContainEqual(["eq", "id", GEN_ID]);
      expect(queries[0].calls).toContainEqual(["eq", "user_id", UID]);
      expect(queries[0].calls.map(([method]) => method)).toContain("maybeSingle");
    });

    it("returns the row when the owner's row exists", async () => {
      const row = { id: GEN_ID, user_id: UID };
      stubClient({ data: row, error: null });
      await expect(new SupabaseStore().getGenerationForUser(GEN_ID, UID)).resolves.toEqual(row);
    });
  });

  describe("failed queries reject with a DataStoreError carrying the supabase error", () => {
    const boom = { message: "boom", code: "XX000" };

    it.each([
      ["getProfile", (s: SupabaseStore) => s.getProfile(UID)],
      ["upsertProfile", (s: SupabaseStore) => s.upsertProfile({ id: UID, email: "a@b.c", preferred_model: "m" })],
      ["ensureProfile", (s: SupabaseStore) => s.ensureProfile({ id: UID, email: "a@b.c" })],
      ["countFreeGenerationsThisMonth", (s: SupabaseStore) => s.countFreeGenerationsThisMonth(UID, new Date())],
      ["getGenerationForUser", (s: SupabaseStore) => s.getGenerationForUser(GEN_ID, UID)],
      ["resetForRetry", (s: SupabaseStore) => s.resetForRetry(GEN_ID, UID)],
      ["listMyGenerations", (s: SupabaseStore) => s.listMyGenerations(UID, 0, 20)],
      ["generationStats", (s: SupabaseStore) => s.generationStats(UID)],
    ])("%s", async (_name, call) => {
      stubClient({ data: null, count: null, error: boom });

      const err = await rejection(call(new SupabaseStore()));

      expect(err).toBeInstanceOf(DataStoreError);
      expect((err as DataStoreError).cause).toBe(boom);
    });

    const newGeneration = {
      mode: "simple",
      tier: 0,
      prompt: "p",
      project_type: "DotNetNextJs",
      schema_json: null,
      personalization_json: null,
      user_id: UID,
    } as const;

    it("insertGeneration", async () => {
      stubClient({ data: null, error: boom });
      const err = await rejection(new SupabaseStore().insertGeneration(newGeneration));
      expect(err).toBeInstanceOf(DataStoreError);
      expect((err as DataStoreError).cause).toBe(boom);
    });

    it("insertGeneration when no row comes back and there is no error", async () => {
      stubClient({ data: null, error: null });
      const err = await rejection(new SupabaseStore().insertGeneration(newGeneration));
      expect(err).toBeInstanceOf(DataStoreError);
      expect((err as DataStoreError).cause).toBeNull();
    });
  });
});
