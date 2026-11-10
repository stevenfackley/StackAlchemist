/**
 * engine-client.ts — the one place the web app calls the Engine from: where the
 * URL comes from, which headers every call carries, and the time limit.
 */
import { postToEngine, resolveEngineUrl } from "@/lib/engine-client";

const serviceKeyMock = vi.fn(() => "");
vi.mock("@/lib/runtime-config", () => ({ getEngineServiceKey: () => serviceKeyMock() }));

const USER_ID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";

describe("engine-client.ts", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({ ok: true, status: 200 });
    serviceKeyMock.mockReturnValue("");
    vi.stubEnv("NODE_ENV", "test");
    vi.stubEnv("ENGINE_API_URL", "http://engine.test");
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it("POSTs the body as JSON to the configured Engine", async () => {
    await postToEngine("/api/generate", { generationId: "g1" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://engine.test/api/generate");
    expect(init.method).toBe("POST");
    expect(init.headers).toEqual({ "Content-Type": "application/json" });
    expect(JSON.parse(init.body)).toEqual({ generationId: "g1" });
  });

  it("sends the service key and the user the call is made for", async () => {
    serviceKeyMock.mockReturnValue("service-key");

    await postToEngine("/api/generate", {}, { userId: USER_ID });

    expect(fetchMock.mock.calls[0][1].headers).toEqual({
      "Content-Type": "application/json",
      "X-Engine-Key": "service-key",
      "X-Engine-User": USER_ID,
    });
  });

  it("returns a non-2xx response instead of throwing", async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 402 });

    const res = await postToEngine("/api/generate", {});
    expect(res.status).toBe(402);
  });

  it("gives up on an Engine that does not answer within the limit", async () => {
    vi.useFakeTimers();
    // A connection that hangs until the caller aborts it, as fetch does.
    fetchMock.mockImplementation(
      (_url: string, init: RequestInit) =>
        new Promise((_resolve, reject) => {
          init.signal?.addEventListener("abort", () => reject(new DOMException("aborted", "AbortError")));
        })
    );

    const call = postToEngine("/api/generate", {}, { timeoutMs: 1_000 });
    const outcome = expect(call).rejects.toMatchObject({ name: "AbortError" });
    await vi.advanceTimersByTimeAsync(1_000);
    await outcome;
  });

  it("does not abort a call that answered in time", async () => {
    vi.useFakeTimers();
    let signal: AbortSignal | undefined;
    fetchMock.mockImplementation(async (_url: string, init: RequestInit) => {
      signal = init.signal ?? undefined;
      return { ok: true, status: 200 };
    });

    await postToEngine("/api/generate", {}, { timeoutMs: 1_000 });
    await vi.advanceTimersByTimeAsync(5_000);
    expect(signal?.aborted).toBe(false);
  });

  it("falls back to localhost outside production", () => {
    vi.stubEnv("ENGINE_API_URL", "");
    expect(resolveEngineUrl()).toBe("http://localhost:5000");
  });

  it("refuses to guess the Engine URL in production", () => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("ENGINE_API_URL", "");
    expect(() => resolveEngineUrl()).toThrow("ENGINE_API_URL must be configured in production.");
  });
});
