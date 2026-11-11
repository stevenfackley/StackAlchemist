/**
 * actions.ts — retryGeneration guards (configured / non-demo runtime).
 *
 * retryGeneration re-fires the Engine (real LLM spend), and the store has no
 * row-level security — so it must authenticate the caller and verify
 * ownership itself. The ownership rejection reuses the "Generation not found."
 * message so responses are not an existence oracle for other users' ids.
 */
import { getDataStore } from "@/lib/data";
import { DataStoreError } from "@/lib/data/store";
import { getSessionUser } from "@/lib/session";
import { retryGeneration } from "@/lib/actions";
import { makeStore, fakeResponse, type FakeStore } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
let store: FakeStore;

// Session ids are Keycloak subs, and generation ids are UUIDs too.
const GEN_ID = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";
const MISSING_ID = "7a1c9d2e-3b4f-4a5c-8d6e-9f0a1b2c3d4e";
const OWNER_ID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
const INTRUDER_ID = "9c8d7e6f-1a2b-4c3d-8e4f-5a6b7c8d9e0f";

function baseGeneration(overrides: Record<string, unknown> = {}) {
  return {
    id: GEN_ID,
    user_id: OWNER_ID,
    mode: "simple",
    tier: 1,
    project_type: "DotNetNextJs",
    prompt: "a prompt",
    schema_json: null,
    personalization_json: null,
    attempt_count: 0,
    retry_count: 0,
    status: "failed",
    ...overrides,
  };
}

describe("actions.ts — retryGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getSessionUser).mockReset();
    // Default: the signed-in caller owns baseGeneration().
    vi.mocked(getSessionUser).mockResolvedValue({ id: OWNER_ID, email: null });
    store = makeStore();
    vi.mocked(getDataStore).mockReset();
    vi.mocked(getDataStore).mockReturnValue(store);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects unauthenticated callers before touching the database", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Please sign in to retry a build." });
    expect(getDataStore).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects a caller who does not own the generation, without leaking its existence", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: INTRUDER_ID, email: null });
    // Even if the read handed back someone else's row, the defence-in-depth owner check refuses it.
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);

    const result = await retryGeneration(GEN_ID);
    // Identical message to the missing-row case — no existence oracle.
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(store.getGenerationForUser).toHaveBeenCalledWith(GEN_ID, INTRUDER_ID);
    expect(store.resetForRetry).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns an error when the generation does not exist", async () => {
    store.getGenerationForUser.mockResolvedValue(null);

    const result = await retryGeneration(MISSING_ID);
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(store.getGenerationForUser).toHaveBeenCalledWith(MISSING_ID, OWNER_ID);
  });

  it("returns the same not-found error when the lookup itself fails", async () => {
    store.getGenerationForUser.mockRejectedValue(new DataStoreError("generations select failed"));

    const result = await retryGeneration(MISSING_ID);
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(consoleErrorSpy).toHaveBeenCalledWith("[retryGeneration] Lookup error:", expect.any(DataStoreError));
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refuses to retry once all 3 retries are used", async () => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration({ retry_count: 3 }) as never);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({
      success: false,
      error: "You have used all 3 retries for this build. Please start a new one.",
    });
    expect(store.resetForRetry).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refuses to retry a generation that isn't in a failed state", async () => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration({ status: "success" }) as never);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({
      success: false,
      error: "Only failed generations can be retried.",
    });
    expect(store.resetForRetry).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("resets status and re-fires the engine for the owner of a failed generation", async () => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);
    store.resetForRetry.mockResolvedValue(true);
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
    expect(getSessionUser).toHaveBeenCalledTimes(1);
    // The compare-and-set is scoped to the caller, and it runs before the spend.
    expect(store.resetForRetry).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(store.resetForRetry.mock.invocationCallOrder[0]).toBeLessThan(fetchMock.mock.invocationCallOrder[0]);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/generate");
    // A retry spends the caller's own rate-limit window, like their first submit.
    expect(init.headers["X-Engine-User"]).toBe(OWNER_ID);
    // The Engine accepted the build, so the retry counted by the reset stands.
    expect(store.uncountRetry).not.toHaveBeenCalled();
  });

  it("does not re-fire the engine when another retry already claimed the failed row", async () => {
    // The scoped UPDATE matched no row: a concurrent retry flipped it first.
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);
    store.resetForRetry.mockResolvedValue(false);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Only failed generations can be retried." });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not re-fire the engine when the reset query itself rejects", async () => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);
    // The update throws (a dropped connection).
    store.resetForRetry.mockRejectedValue(new Error("connection reset"));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Only failed generations can be retried." });
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      "[retryGeneration] Status reset failed:",
      expect.objectContaining({ message: "connection reset" })
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("reports the Engine's refusal when the build has no completed payment (402)", async () => {
    // A paid build whose payment was refunded: the Engine will not rebuild it.
    store.getGenerationForUser.mockResolvedValue(baseGeneration({ tier: 2 }) as never);
    store.resetForRetry.mockResolvedValue(true);
    fetchMock.mockResolvedValue(
      fakeResponse({ error: "This build has no completed payment, so it was not started." }, { ok: false, status: 402 })
    );

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({
      success: false,
      error: "This build has no completed payment, so it was not started.",
    });
    // The refused retry started nothing: it is not one of the owner's three.
    expect(store.uncountRetry).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
  });

  it("still reports success when the re-fire fetch throws, and gives the retry back", async () => {
    // The row is pending and the Engine's reconciler will start it; the page shows it in progress.
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);
    store.resetForRetry.mockResolvedValue(true);
    fetchMock.mockRejectedValue(new Error("engine unreachable"));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
    expect(store.uncountRetry).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
  });

  it.each([429, 500, 503])("gives the retry back when the Engine answers %i without starting a build", async (status) => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration({ tier: 2, retry_count: 2 }) as never);
    store.resetForRetry.mockResolvedValue(true);
    fetchMock.mockResolvedValue(fakeResponse({ error: "busy" }, { ok: false, status }));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
    expect(store.uncountRetry).toHaveBeenCalledTimes(1);
    expect(store.uncountRetry).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
  });

  it("a failure to give the retry back is logged and does not change the answer", async () => {
    store.getGenerationForUser.mockResolvedValue(baseGeneration() as never);
    store.resetForRetry.mockResolvedValue(true);
    store.uncountRetry.mockRejectedValue(new Error("connection reset"));
    fetchMock.mockRejectedValue(new Error("engine unreachable"));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      "[retryGeneration] Could not give the retry back:",
      expect.objectContaining({ message: "connection reset" })
    );
  });

  it("automatic restarts by the Engine do not use up the owner's retries", async () => {
    // attempt_count is the reconciler's restart budget. Three restarts, no retries pressed.
    store.getGenerationForUser.mockResolvedValue(baseGeneration({ attempt_count: 3, retry_count: 0 }) as never);
    store.resetForRetry.mockResolvedValue(true);
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    expect(await retryGeneration(GEN_ID)).toEqual({ success: true });
  });
});

