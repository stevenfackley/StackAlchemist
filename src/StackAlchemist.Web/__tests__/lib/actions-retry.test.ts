/**
 * actions.ts — retryGeneration guards (configured / non-demo runtime).
 *
 * retryGeneration re-fires the Engine (real LLM spend) via the service-role
 * client, which bypasses RLS — so it must authenticate the caller and verify
 * ownership itself. The ownership rejection reuses the "Generation not found."
 * message so responses are not an existence oracle for other users' ids.
 */
import { getServerUser } from "@/lib/supabase-server";
import { createServerClient } from "@/lib/supabase";
import { retryGeneration } from "@/lib/actions";
import { makeDb, chainable, fakeResponse } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  usesPostgresStore: vi.fn(() => false),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

// The store rejects malformed ids before querying, so fixtures need real UUIDs.
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
    status: "failed",
    ...overrides,
  };
}

describe("actions.ts — retryGeneration (configured)", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getServerUser).mockReset();
    // Default: the signed-in caller owns baseGeneration().
    vi.mocked(getServerUser).mockResolvedValue({ id: OWNER_ID } as never);
    vi.mocked(createServerClient).mockReset();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects unauthenticated callers before touching the database", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null as never);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Please sign in to retry a build." });
    expect(createServerClient).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects a caller who does not own the generation, without leaking its existence", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: INTRUDER_ID } as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: baseGeneration(), error: null }]) as never
    );

    const result = await retryGeneration(GEN_ID);
    // Identical message to the missing-row case — no existence oracle.
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns an error when the generation does not exist", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "not found" } }]) as never
    );

    const result = await retryGeneration(MISSING_ID);
    expect(result).toEqual({ success: false, error: "Generation not found." });
  });

  it("refuses to retry once the max attempt count (3) is reached", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: baseGeneration({ attempt_count: 3 }), error: null }]) as never
    );

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({
      success: false,
      error: "Maximum retry attempts (3) reached. Please start a new generation.",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refuses to retry a generation that isn't in a failed state", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: baseGeneration({ status: "success" }), error: null }]) as never
    );

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({
      success: false,
      error: "Only failed generations can be retried.",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("resets status and re-fires the engine for the owner of a failed generation", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { data: baseGeneration(), error: null }, // select
        { data: [{ id: GEN_ID }], error: null }, // update
      ]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
    expect(getServerUser).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/generate");
  });

  it("does not re-fire the engine when another retry already claimed the failed row", async () => {
    // The scoped UPDATE matched no row: a concurrent retry flipped it first.
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { data: baseGeneration(), error: null }, // select
        { data: [], error: null }, // update
      ]) as never
    );

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Only failed generations can be retried." });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not re-fire the engine when the reset query itself rejects", async () => {
    const db = makeDb([{ data: baseGeneration(), error: null }]); // select
    // The update's awaited result throws (a dropped connection), rather than resolving with an error.
    const rejecting = chainable({});
    rejecting.then = (resolve: (v: unknown) => unknown, reject?: (r: unknown) => unknown) =>
      Promise.reject(new Error("connection reset")).then(resolve, reject);
    db.from.mockReturnValueOnce(rejecting);
    vi.mocked(createServerClient).mockReturnValue(db as never);

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: false, error: "Only failed generations can be retried." });
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      "[retryGeneration] Status reset failed:",
      expect.objectContaining({ message: "connection reset" })
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("still reports success even when the re-fire fetch throws", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { data: baseGeneration(), error: null },
        { data: [{ id: GEN_ID }], error: null },
      ]) as never
    );
    fetchMock.mockRejectedValue(new Error("engine unreachable"));

    const result = await retryGeneration(GEN_ID);
    expect(result).toEqual({ success: true });
  });
});
