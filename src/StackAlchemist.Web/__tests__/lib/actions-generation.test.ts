/**
 * actions.ts — submitSimpleGeneration / submitAdvancedGeneration /
 * createPendingGeneration / getFreeQuotaStatus / getMyGenerations /
 * extractSchema with a "fully configured" (non-demo) runtime: auth gating,
 * the free-tier quota pre-check, DB failure handling, and Engine-fetch
 * resilience.
 */
import { getDataStore } from "@/lib/data";
import { DataStoreError } from "@/lib/data/store";
import { getSessionUser } from "@/lib/session";
import { hasDataStoreConfig } from "@/lib/runtime-config";
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
import { makeStore, fakeResponse, type FakeStore } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => "engine-service-key-test"),
}));

vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));

const USER = { id: "user-123", email: "founder@example.com" };
const PROMPT = "Build a recipe sharing app for home cooks";

const VALID_SCHEMA: GenerationSchema = {
  entities: [{ name: "Widget", fields: [{ name: "id", type: "UUID", pk: true }] }],
  relationships: [],
  endpoints: [],
};

/** UTC midnight on the first of the current month: the quota window's start. */
function monthStartUtc() {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
}

// `console.error` is spied (never restored via `vi.restoreAllMocks`, which
// would also wipe the persistent `() => true` defaults baked into the
// `runtime-config` mock above) — just this one spy is created/torn down
// explicitly in every describe block below.
let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
let store: FakeStore;

/** Fresh fake store per test, returned by every getDataStore() call. */
function resetStore() {
  store = makeStore();
  vi.mocked(getDataStore).mockReset();
  vi.mocked(getDataStore).mockReturnValue(store);
}

describe("actions.ts — submitSimpleGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects when the caller is not authenticated", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const result = await submitSimpleGeneration(PROMPT, 1);

    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
    expect(store.insertGeneration).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("blocks tier-0 submission once the free quota is exhausted, with a friendly message", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.countFreeGenerationsThisMonth.mockResolvedValue(5);

    const result = await submitSimpleGeneration(PROMPT, 0);

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error).toContain("You've used all 5 free builds this month");
    }
    // Counted for the caller, over the current UTC calendar month.
    expect(store.countFreeGenerationsThisMonth).toHaveBeenCalledWith(USER.id, monthStartUtc());
    expect(store.insertGeneration).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("proceeds when the free quota still has room", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.countFreeGenerationsThisMonth.mockResolvedValue(2);
    store.insertGeneration.mockResolvedValue({ id: "gen-abc" } as never);
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitSimpleGeneration(PROMPT, 0);

    expect(result).toEqual({
      success: true,
      generationId: "gen-abc",
      redirectUrl: "/generate/gen-abc",
    });
    // The row is written for the caller, as a simple-mode Spark build.
    expect(store.insertGeneration).toHaveBeenCalledWith({
      mode: "simple",
      tier: 0,
      prompt: PROMPT,
      project_type: "DotNetNextJs",
      schema_json: null,
      personalization_json: null,
      user_id: USER.id,
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/generate");
    expect(init.headers["X-Engine-Key"]).toBe("engine-service-key-test");
  });

  it("does not re-check the quota for paid tiers", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockResolvedValue({ id: "gen-paid" } as never);
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitSimpleGeneration(PROMPT, 2);

    expect(result.success).toBe(true);
    expect(store.countFreeGenerationsThisMonth).not.toHaveBeenCalled();
  });

  it("returns a generic error when the insert fails", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockRejectedValue(new DataStoreError("db down"));

    const result = await submitSimpleGeneration(PROMPT, 2);

    expect(result).toEqual({
      success: false,
      error: "Failed to create generation record. Please try again.",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("still returns success when the Engine fetch rejects outright (row already exists)", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockResolvedValue({ id: "gen-resilient" } as never);
    fetchMock.mockRejectedValue(new Error("ECONNREFUSED"));

    const result = await submitSimpleGeneration(PROMPT, 3);

    expect(result).toEqual({
      success: true,
      generationId: "gen-resilient",
      redirectUrl: "/generate/gen-resilient",
    });
  });

  it("still returns success when the Engine responds non-2xx (logs but doesn't fail the user)", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockResolvedValue({ id: "gen-engine-500" } as never);
    fetchMock.mockResolvedValue(fakeResponse("engine exploded", { ok: false, status: 500 }));

    const result = await submitSimpleGeneration(PROMPT, 1);

    expect(result.success).toBe(true);
  });
});

describe("actions.ts — submitAdvancedGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects a schema with a blank entity name", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    const badSchema: GenerationSchema = {
      entities: [{ name: "  ", fields: [] }],
      relationships: [],
      endpoints: [],
    };

    const result = await submitAdvancedGeneration(badSchema, 1);
    expect(result).toEqual({ success: false, error: "All entities must have a name." });
  });

  it("rejects when unauthenticated", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 1);
    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
    expect(store.insertGeneration).not.toHaveBeenCalled();
  });

  it("blocks tier-0 submission once quota is exhausted", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.countFreeGenerationsThisMonth.mockResolvedValue(5);

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 0);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error).toContain("free builds this month");
    }
    expect(store.insertGeneration).not.toHaveBeenCalled();
  });

  it("saves the full schema and fires the engine on success", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockResolvedValue({ id: "gen-advanced-1" } as never);
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await submitAdvancedGeneration(VALID_SCHEMA, 2);

    expect(result).toEqual({
      success: true,
      generationId: "gen-advanced-1",
      redirectUrl: "/generate/gen-advanced-1",
    });
    expect(store.insertGeneration).toHaveBeenCalledWith({
      mode: "advanced",
      tier: 2,
      prompt: "Widget — 1 entities, 0 relationships, 0 endpoints",
      project_type: "DotNetNextJs",
      schema_json: VALID_SCHEMA,
      personalization_json: null,
      user_id: USER.id,
    });
    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.schema).toEqual(VALID_SCHEMA);
  });
});

describe("actions.ts — createPendingGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects when unauthenticated", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);
    const result = await createPendingGeneration("simple", 1, "a prompt");
    expect(result).toEqual({ success: false, error: "Please sign in to start a build." });
    expect(store.insertGeneration).not.toHaveBeenCalled();
  });

  it("creates a pending row without ever calling the engine", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.insertGeneration.mockResolvedValue({ id: "gen-pending-1" } as never);

    const result = await createPendingGeneration("simple", 2, "a prompt");
    expect(result).toEqual({ success: true, generationId: "gen-pending-1" });
    expect(store.insertGeneration).toHaveBeenCalledWith(
      expect.objectContaining({ mode: "simple", tier: 2, prompt: "a prompt", user_id: USER.id })
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("actions.ts — the caller's profile row exists before the first insert", () => {
  const fetchMock = vi.fn();

  // Each creating action, arranged to reach its insert, with the message its insert failure shows today.
  const creators = [
    ["submitSimpleGeneration", () => submitSimpleGeneration(PROMPT, 0), "Failed to create generation record. Please try again."],
    ["submitAdvancedGeneration", () => submitAdvancedGeneration(VALID_SCHEMA, 0), "Failed to save your schema. Please try again."],
    ["createPendingGeneration", () => createPendingGeneration("simple", 2, "a prompt"), "Failed to create generation record. Please try again."],
  ] as const;

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));
    vi.mocked(getSessionUser).mockReset();
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    resetStore();
    store.insertGeneration.mockResolvedValue({ id: "gen-pg" } as never);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it.each(creators)("%s ensures the caller's profile before the quota count and the insert", async (name, create) => {
    const result = await create();

    expect(result.success).toBe(true);
    expect(store.ensureProfile).toHaveBeenCalledTimes(1);
    expect(store.ensureProfile).toHaveBeenCalledWith({ id: USER.id, email: USER.email });
    const ensuredAt = store.ensureProfile.mock.invocationCallOrder[0];
    expect(ensuredAt).toBeLessThan(store.insertGeneration.mock.invocationCallOrder[0]);
    // Free tiers count the quota exactly once; the paid checkout path never does.
    // Pinned so the ordering loop above cannot pass by iterating nothing.
    expect(store.countFreeGenerationsThisMonth).toHaveBeenCalledTimes(name === "createPendingGeneration" ? 0 : 1);
    for (const countedAt of store.countFreeGenerationsThisMonth.mock.invocationCallOrder) expect(ensuredAt).toBeLessThan(countedAt);
  });

  it("stores an empty email for a caller the session has no email for, as saveProfileSettings does", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: USER.id, email: null });

    await createPendingGeneration("simple", 2, "a prompt");

    expect(store.ensureProfile).toHaveBeenCalledWith({ id: USER.id, email: "" });
  });

  it.each(creators)("%s surfaces a failed ensureProfile exactly like a failed insert, and inserts nothing", async (_name, create, message) => {
    store.ensureProfile.mockRejectedValue(new DataStoreError("profiles insert failed"));

    await expect(create()).resolves.toEqual({ success: false, error: message });
    expect(store.insertGeneration).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("actions.ts — getFreeQuotaStatus / getMyGenerations (config gating, not demo mode)", () => {
  beforeEach(() => {
    vi.mocked(getSessionUser).mockReset();
    resetStore();
    vi.mocked(hasDataStoreConfig).mockReturnValue(true);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("getFreeQuotaStatus falls back to a full quota when no data store is configured, even though isDemoMode is false", async () => {
    vi.mocked(hasDataStoreConfig).mockReturnValueOnce(false);

    const status = await getFreeQuotaStatus();
    expect(status.remaining).toBe(status.limit);
    expect(getSessionUser).not.toHaveBeenCalled();
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("getFreeQuotaStatus reports usage for an authenticated user", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.countFreeGenerationsThisMonth.mockResolvedValue(3);

    const status = await getFreeQuotaStatus();
    expect(status.used).toBe(3);
    expect(status.remaining).toBe(status.limit - 3);
    expect(store.countFreeGenerationsThisMonth).toHaveBeenCalledWith(USER.id, monthStartUtc());
  });

  it("getMyGenerations returns an empty page when no data store is configured", async () => {
    vi.mocked(hasDataStoreConfig).mockReturnValueOnce(false);
    vi.mocked(getSessionUser).mockResolvedValue(USER);

    const result = await getMyGenerations();
    expect(result).toEqual({ generations: [], total: 0 });
    expect(getDataStore).not.toHaveBeenCalled();
  });

  it("getMyGenerations returns the user's rows newest-first with the total count", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    const rows = [{ id: "gen-2" }, { id: "gen-1" }];
    store.listMyGenerations.mockResolvedValue({ generations: rows as never, total: 57 });

    const result = await getMyGenerations();
    expect(result.generations).toEqual(rows);
    expect(result.total).toBe(57);
    // Scoped to the caller: first page, default size 20.
    expect(store.listMyGenerations).toHaveBeenCalledWith(USER.id, 0, 20);
  });

  it("getMyGenerations requests the correct window for a later page", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.listMyGenerations.mockResolvedValue({ generations: [], total: 100 });

    await getMyGenerations(3, 20);

    // Page 3 @ pageSize 20 → rows 40..59: offset 40, limit 20.
    expect(store.listMyGenerations).toHaveBeenCalledWith(USER.id, 40, 20);
  });

  it.each([
    ["10000", 10000, 100],
    ["NaN", Number.NaN, 20],
    ["0", 0, 1],
    ["a fraction", 2.7, 2],
  ])("getMyGenerations clamps pageSize %s to an integer in [1, 100] (default 20 for NaN)", async (_name, pageSize, limit) => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);

    await getMyGenerations(1, pageSize);

    expect(store.listMyGenerations).toHaveBeenCalledWith(USER.id, 0, limit);
  });

  it("getMyGenerations returns an empty page when the query errors", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.listMyGenerations.mockRejectedValue(new DataStoreError("generations list failed"));

    const result = await getMyGenerations();
    expect(result).toEqual({ generations: [], total: 0 });
  });

  it("getGenerationStats aggregates total / completed / in-progress counts", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.generationStats.mockResolvedValue({ total: 42, completed: 30, inProgress: 5 });

    const stats = await getGenerationStats();
    expect(stats).toEqual({ total: 42, completed: 30, inProgress: 5 });
    expect(store.generationStats).toHaveBeenCalledWith(USER.id);
  });

  it("getGenerationStats returns zeros when a count query errors", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(USER);
    store.generationStats.mockRejectedValue(new DataStoreError("generations stats failed"));

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
