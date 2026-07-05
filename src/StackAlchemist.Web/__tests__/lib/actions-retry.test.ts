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
import { makeDb, fakeResponse } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

function baseGeneration(overrides: Record<string, unknown> = {}) {
  return {
    id: "gen-1",
    user_id: "owner-user",
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
    vi.mocked(getServerUser).mockResolvedValue({ id: "owner-user" } as never);
    vi.mocked(createServerClient).mockReset();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    consoleErrorSpy.mockRestore();
  });

  it("rejects unauthenticated callers before touching the database", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null as never);

    const result = await retryGeneration("gen-1");
    expect(result).toEqual({ success: false, error: "Please sign in to retry a build." });
    expect(createServerClient).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects a caller who does not own the generation, without leaking its existence", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: "intruder" } as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: baseGeneration(), error: null }]) as never
    );

    const result = await retryGeneration("gen-1");
    // Identical message to the missing-row case — no existence oracle.
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns an error when the generation does not exist", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "not found" } }]) as never
    );

    const result = await retryGeneration("missing-id");
    expect(result).toEqual({ success: false, error: "Generation not found." });
  });

  it("refuses to retry once the max attempt count (3) is reached", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: baseGeneration({ attempt_count: 3 }), error: null }]) as never
    );

    const result = await retryGeneration("gen-1");
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

    const result = await retryGeneration("gen-1");
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
        { error: null }, // update
      ]) as never
    );
    fetchMock.mockResolvedValue(fakeResponse({ ok: true }));

    const result = await retryGeneration("gen-1");
    expect(result).toEqual({ success: true });
    expect(getServerUser).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/generate");
  });

  it("still reports success even when the re-fire fetch throws", async () => {
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([
        { data: baseGeneration(), error: null },
        { error: null },
      ]) as never
    );
    fetchMock.mockRejectedValue(new Error("engine unreachable"));

    const result = await retryGeneration("gen-1");
    expect(result).toEqual({ success: true });
  });
});
