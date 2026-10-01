/**
 * actions.ts — submitSimpleGeneration / submitAdvancedGeneration /
 * createPendingGeneration / getFreeQuotaStatus / getMyGenerations /
 * extractSchema with a "fully configured" (non-demo) runtime: auth gating,
 * the free-tier quota pre-check, DB failure handling, and Engine-fetch
 * resilience.
 */
import { getServerUser } from "@/lib/supabase-server";
import { createServerClient } from "@/lib/supabase";
import type { MockInstance } from "vitest";
import { hasDataStoreConfig, hasServerSupabaseConfig, usesPostgresStore } from "@/lib/runtime-config";
import { DataStoreError } from "@/lib/data/store";
import { DrizzleStore } from "@/lib/data/drizzle-store";
import { SupabaseStore } from "@/lib/data/supabase-store";
import {
  createPendingGeneration,
  extractSchema,
  getFreeQuotaStatus,
  getMyGenerations,
  getGenerationStats,
  submitAdvancedGeneration,
  submitSimpleGeneration,
} from "@/lib/actions";
import type { GenerationSchema } from "@/lib/types";
import { makeDb, fakeResponse } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  usesPostgresStore: vi.fn(() => false),
  usesQavrenAuth: vi.fn(() => false),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => "engine-service-key-test"),
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

const USER = { id: "user-123", email: "founder@example.com" };

const VALID_SCHEMA: GenerationSchema = {
  entities: [{ name: "Widget", fields: [{ name: "id", type: "UUID", pk: true }] }],
  relationships: [],
  endpoints: [],
};

// `console.error` is spied (never restored via `vi.restoreAllMocks`, which
// would also wipe the persistent `() => true` defaults baked into the
// `runtime-config` mock above) — just this one spy is created/torn down
// explicitly in every describe block below.
let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

describe("actions.ts — submitSimpleGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects when the caller is not authenticated", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null);
    vi.mocked(createServerClient).mockReturnValue(makeDb([]) as never);

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 1);

    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("blocks tier-0 submission once the free quota is exhausted, with a friendly message", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(makeDb([{ count: 5, error: null }]) as never);

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 0);

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error).toContain("You've used all 5 free builds this month");
    }
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("proceeds when the free quota still has room", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { count: 2, error: null }, // quota check
        { data: { id: "gen-abc" }, error: null }, // insert
      ]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 0);

    expect(result).toEqual({
      success: true,
      generationId: "gen-abc",
      redirectUrl: "/generate/gen-abc",
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/generate");
    expect(init.headers["X-Engine-Key"]).toBe("engine-service-key-test");
  });

  it("does not re-check the quota for paid tiers (only one Supabase query)", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: { id: "gen-paid" }, error: null }]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 2);

    expect(result.success).toBe(true);
  });

  it("returns a generic error when the insert fails", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "db down" } }]) as never
    );

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 2);

    expect(result).toEqual({
      success: false,
      error: "Failed to create generation record. Please try again.",
    });
  });

  it("still returns success when the Engine fetch rejects outright (row already exists)", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: { id: "gen-resilient" }, error: null }]) as never
    );
    fetchMock.mockRejectedValue(new Error("ECONNREFUSED"));

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 3);

    expect(result).toEqual({
      success: true,
      generationId: "gen-resilient",
      redirectUrl: "/generate/gen-resilient",
    });
  });

  it("still returns success when the Engine responds non-2xx (logs but doesn't fail the user)", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: { id: "gen-engine-500" }, error: null }]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse("engine exploded", { ok: false, status: 500 }));

    const result = await submitSimpleGeneration("Build a recipe sharing app for home cooks", 1);

    expect(result.success).toBe(true);
  });
});

describe("actions.ts — submitAdvancedGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects a schema with a blank entity name", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const badSchema: GenerationSchema = {
      entities: [{ name: "  ", fields: [] }],
      relationships: [],
      endpoints: [],
    };

    const result = await submitAdvancedGeneration(badSchema, 1);
    expect(result).toEqual({ success: false, error: "All entities must have a name." });
  });

  it("rejects when unauthenticated", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null);

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 1);
    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
  });

  it("blocks tier-0 submission once quota is exhausted", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(makeDb([{ count: 5, error: null }]) as never);

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 0);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error).toContain("free builds this month");
    }
  });

  it("saves the full schema and fires the engine on success", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: { id: "gen-advanced-1" }, error: null }]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 2);

    expect(result).toEqual({
      success: true,
      generationId: "gen-advanced-1",
      redirectUrl: "/generate/gen-advanced-1",
    });
    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.schema).toEqual(VALID_SCHEMA);
  });
});

describe("actions.ts — createPendingGeneration (configured)", () => {
  beforeEach(() => {
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("rejects when unauthenticated", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null);
    const result = await createPendingGeneration("simple", 1, "a prompt");
    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
  });

  it("creates a pending row without ever calling the engine", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: { id: "gen-pending-1" }, error: null }]) as never
    );

    const result = await createPendingGeneration("simple", 2, "a prompt");
    expect(result).toEqual({ success: true, generationId: "gen-pending-1" });
  });
});

describe("actions.ts — the caller's profile row exists before the first insert", () => {
  const fetchMock = vi.fn();
  let ensureProfile: MockInstance<DrizzleStore["ensureProfile"]>;
  let countFree: MockInstance<DrizzleStore["countFreeGenerationsThisMonth"]>;
  let insertGeneration: MockInstance<DrizzleStore["insertGeneration"]>;

  // Each creating action, arranged to reach its insert, with the message its insert failure shows today.
  const creators = [
    ["submitSimpleGeneration", () => submitSimpleGeneration("Build a recipe sharing app for home cooks", 0), "Failed to create generation record. Please try again."],
    ["submitAdvancedGeneration", () => submitAdvancedGeneration(VALID_SCHEMA, 0), "Failed to save your schema. Please try again."],
    ["createPendingGeneration", () => createPendingGeneration("simple", 2, "a prompt"), "Failed to create generation record. Please try again."],
  ] as const;

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));
    vi.mocked(getServerUser).mockReset();
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    // DATABASE_URL set: getDataStore() builds a DrizzleStore. Its postgres-js
    // client is lazy and every query method used here is stubbed, so nothing connects.
    vi.mocked(usesPostgresStore).mockReturnValue(true);
    vi.stubEnv("DATABASE_URL", "postgres://u:p@localhost:5432/db");
    ensureProfile = vi.spyOn(DrizzleStore.prototype, "ensureProfile").mockResolvedValue(undefined);
    countFree = vi.spyOn(DrizzleStore.prototype, "countFreeGenerationsThisMonth").mockResolvedValue(0);
    insertGeneration = vi.spyOn(DrizzleStore.prototype, "insertGeneration").mockResolvedValue({ id: "gen-pg" } as never);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    ensureProfile.mockRestore();
    countFree.mockRestore();
    insertGeneration.mockRestore();
    vi.mocked(usesPostgresStore).mockReturnValue(false);
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it.each(creators)("%s ensures the caller's profile (DATABASE_URL set) before the quota count and the insert", async (name, create) => {
    const result = await create();

    expect(result.success).toBe(true);
    expect(ensureProfile).toHaveBeenCalledTimes(1);
    expect(ensureProfile).toHaveBeenCalledWith({ id: USER.id, email: USER.email });
    const ensuredAt = ensureProfile.mock.invocationCallOrder[0];
    expect(ensuredAt).toBeLessThan(insertGeneration.mock.invocationCallOrder[0]);
    // Free tiers count the quota exactly once; the paid checkout path never does.
    // Pinned so the ordering loop above cannot pass by iterating nothing.
    expect(countFree).toHaveBeenCalledTimes(name === "createPendingGeneration" ? 0 : 1);
    for (const countedAt of countFree.mock.invocationCallOrder) expect(ensuredAt).toBeLessThan(countedAt);
  });

  it("stores an empty email for a caller the session has no email for, as saveProfileSettings does", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: USER.id } as never);

    await createPendingGeneration("simple", 2, "a prompt");

    expect(ensureProfile).toHaveBeenCalledWith({ id: USER.id, email: "" });
  });

  it.each(creators)("%s surfaces a failed ensureProfile exactly like a failed insert, and inserts nothing", async (_name, create, message) => {
    ensureProfile.mockRejectedValue(new DataStoreError("profiles insert failed"));

    await expect(create()).resolves.toEqual({ success: false, error: message });
    expect(insertGeneration).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each(creators)("%s never calls ensureProfile with DATABASE_URL unset (Supabase store)", async (_name, create) => {
    vi.mocked(usesPostgresStore).mockReturnValue(false);
    vi.stubEnv("DATABASE_URL", "");
    const supabaseEnsure = vi.spyOn(SupabaseStore.prototype, "ensureProfile");
    // One result shape serves both the quota count and the insert.
    const row = { data: { id: "gen-sb" }, count: 0, error: null };
    vi.mocked(createServerClient).mockReturnValue(makeDb([row, row]) as never);
    try {
      const result = await create();

      expect(result.success).toBe(true);
      expect(supabaseEnsure).not.toHaveBeenCalled();
      expect(ensureProfile).not.toHaveBeenCalled();
    } finally {
      supabaseEnsure.mockRestore();
    }
  });
});

describe("actions.ts — getFreeQuotaStatus / getMyGenerations (config gating, not demo mode)", () => {
  beforeEach(() => {
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    vi.mocked(hasServerSupabaseConfig).mockReturnValue(true);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("getFreeQuotaStatus falls back to a full quota when Supabase config is missing, even though isDemoMode is false", async () => {
    vi.mocked(hasServerSupabaseConfig).mockReturnValueOnce(false);
    vi.mocked(hasDataStoreConfig).mockReturnValueOnce(false);

    const status = await getFreeQuotaStatus();
    expect(status.remaining).toBe(status.limit);
    expect(getServerUser).not.toHaveBeenCalled();
  });

  it("getFreeQuotaStatus reports usage for an authenticated user", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(makeDb([{ count: 3, error: null }]) as never);

    const status = await getFreeQuotaStatus();
    expect(status.used).toBe(3);
    expect(status.remaining).toBe(status.limit - 3);
  });

  it("getMyGenerations returns an empty page when Supabase config is missing", async () => {
    vi.mocked(hasServerSupabaseConfig).mockReturnValueOnce(false);
    vi.mocked(hasDataStoreConfig).mockReturnValueOnce(false);
    vi.mocked(getServerUser).mockResolvedValue(USER as never);

    const result = await getMyGenerations();
    expect(result).toEqual({ generations: [], total: 0 });
  });

  it("getMyGenerations returns the user's rows newest-first with the total count", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const rows = [{ id: "gen-2" }, { id: "gen-1" }];
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: rows, error: null, count: 57 }]) as never
    );

    const result = await getMyGenerations();
    expect(result.generations).toEqual(rows);
    expect(result.total).toBe(57);
  });

  it("getMyGenerations requests the correct range window for a later page", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const chain = makeDb([{ data: [], error: null, count: 100 }]) as never as {
      from: ReturnType<typeof vi.fn>;
    };
    vi.mocked(createServerClient).mockReturnValue(chain as never);

    await getMyGenerations(3, 20);

    // Page 3 @ pageSize 20 → rows 40..59 (0-indexed range).
    const builder = chain.from.mock.results[0].value as Record<string, ReturnType<typeof vi.fn>>;
    expect(builder.range).toHaveBeenCalledWith(40, 59);
  });

  it.each([
    ["10000", 10000, [0, 99]],
    ["NaN", Number.NaN, [0, 19]],
    ["0", 0, [0, 0]],
    ["a fraction", 2.7, [0, 1]],
  ])("getMyGenerations clamps pageSize %s to an integer in [1, 100] (default 20 for NaN)", async (_name, pageSize, range) => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    const chain = makeDb([{ data: [], error: null, count: 0 }]) as never as {
      from: ReturnType<typeof vi.fn>;
    };
    vi.mocked(createServerClient).mockReturnValue(chain as never);

    await getMyGenerations(1, pageSize);

    const builder = chain.from.mock.results[0].value as Record<string, ReturnType<typeof vi.fn>>;
    expect(builder.range).toHaveBeenCalledWith(...range);
  });

  it("getMyGenerations returns an empty page when the query errors", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "boom" }, count: null }]) as never
    );

    const result = await getMyGenerations();
    expect(result).toEqual({ generations: [], total: 0 });
  });

  it("getGenerationStats aggregates total / completed / in-progress counts", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    // Three .from() calls: total, completed, in-progress.
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { count: 42, error: null },
        { count: 30, error: null },
        { count: 5, error: null },
      ]) as never
    );

    const stats = await getGenerationStats();
    expect(stats).toEqual({ total: 42, completed: 30, inProgress: 5 });
  });

  it("getGenerationStats returns zeros when a count query errors", async () => {
    vi.mocked(getServerUser).mockResolvedValue(USER as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { count: 42, error: null },
        { count: null, error: { message: "boom" } },
        { count: 5, error: null },
      ]) as never
    );

    const stats = await getGenerationStats();
    expect(stats).toEqual({ total: 0, completed: 0, inProgress: 0 });
  });
});

describe("actions.ts — extractSchema (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("returns the schema from a successful Engine response", async () => {
    const schema: GenerationSchema = { entities: [], relationships: [], endpoints: [] };
    fetchMock.mockResolvedValue(fakeResponse({ schema }));

    const result = await extractSchema("gen-1", "a prompt");
    expect(result).toEqual({ success: true, schema });
  });

  it("propagates the Engine's error message on a non-2xx response", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ error: "LLM extraction failed" }, { ok: false, status: 502 }));

    const result = await extractSchema("gen-1", "a prompt");
    expect(result).toEqual({ success: false, error: "LLM extraction failed" });
  });

  it("falls back to a generic error when a non-2xx body isn't valid JSON", async () => {
    fetchMock.mockResolvedValue(fakeResponse("", { ok: false, status: 500, jsonThrows: true }));

    const result = await extractSchema("gen-1", "a prompt");
    expect(result).toEqual({ success: false, error: "Schema extraction failed" });
  });

  it("returns a timeout-specific message when the request aborts", async () => {
    const abortErr = new Error("The operation was aborted.");
    abortErr.name = "AbortError";
    fetchMock.mockRejectedValue(abortErr);

    const result = await extractSchema("gen-1", "a prompt");
    expect(result).toEqual({
      success: false,
      error: "Schema extraction timed out. The engine may be under load — please try again.",
    });
  });

  it("returns a generic reachability error on any other fetch failure", async () => {
    fetchMock.mockRejectedValue(new Error("ECONNRESET"));

    const result = await extractSchema("gen-1", "a prompt");
    expect(result).toEqual({ success: false, error: "Failed to reach the generation engine." });
  });
});
