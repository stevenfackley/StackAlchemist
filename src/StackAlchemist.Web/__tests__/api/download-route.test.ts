/**
 * GET /api/download/{id}: the owner of a delivered paid build is sent to a
 * link signed for this click; nobody else is sent anywhere.
 */
import { GET } from "@/app/api/download/[id]/route";
import { getDataStore } from "@/lib/data";
import { getSessionUser } from "@/lib/session";
import type { Generation } from "@/lib/types";
import { fakeResponse, makeStore, type FakeStore } from "../lib/actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => "engine-service-key-test"),
}));
vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));

const USER = { id: "0b0e7a52-6c1c-4d55-9d8e-2b1f6f1f0a11", email: "founder@example.com" };
const ID = "3f2c9a10-5b7e-4c1d-8a2f-9e6d4b3c2a10";
const FRESH = "https://account.r2.cloudflarestorage.com/builds/fresh?X-Amz-Expires=900";
const STORED = "https://account.r2.cloudflarestorage.com/builds/stored?X-Amz-Expires=604800";

const fetchMock = vi.fn();
let store: FakeStore;
let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

function delivered(overrides: Partial<Generation> = {}): Generation {
  return {
    id: ID,
    user_id: USER.id,
    tier: 2,
    status: "success",
    download_url: STORED,
    completed_at: new Date().toISOString(),
    ...overrides,
  } as Generation;
}

const get = (id = ID) => GET(new Request(`http://localhost/api/download/${id}`), { params: Promise.resolve({ id }) });

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  fetchMock.mockReset();
  store = makeStore();
  vi.mocked(getDataStore).mockReturnValue(store);
  vi.mocked(getSessionUser).mockReset();
  vi.mocked(getSessionUser).mockResolvedValue(USER);
  consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  consoleErrorSpy.mockRestore();
});

describe("GET /api/download/[id]", () => {
  it("redirects the owner to a link the Engine signed for this click", async () => {
    store.getGenerationForUser.mockResolvedValue(delivered());
    fetchMock.mockResolvedValue(fakeResponse({ url: FRESH }));

    const res = await get();

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe(FRESH);
    expect(res.headers.get("cache-control")).toBe("no-store");
    // Looked up for this user only, and the Engine is told who is asking.
    expect(store.getGenerationForUser).toHaveBeenCalledWith(ID, USER.id);
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/download-url$/);
    expect(JSON.parse(init.body)).toEqual({ generationId: ID });
    expect(init.headers["X-Engine-User"]).toBe(USER.id);
  });

  it("sends a signed-out visitor to sign in, and back to the build's page", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const res = await get();

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe(`/login?returnTo=${encodeURIComponent(`/generate/${ID}`)}`);
    expect(store.getGenerationForUser).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([
    ["someone else's or an unknown build", null],
    ["a build that failed", delivered({ status: "failed" })],
    ["a build still running", delivered({ status: "building" })],
    ["a free preview", delivered({ tier: 0 })],
  ])("answers 404 for %s, without asking the Engine", async (_case, row) => {
    store.getGenerationForUser.mockResolvedValue(row);

    const res = await get();

    expect(res.status).toBe(404);
    expect(res.headers.get("location")).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("answers 404 for an id that is not a uuid, before any lookup", async () => {
    const res = await get("../../login");

    expect(res.status).toBe(404);
    expect(getSessionUser).not.toHaveBeenCalled();
    expect(store.getGenerationForUser).not.toHaveBeenCalled();
  });

  it.each([
    ["is unreachable", () => fetchMock.mockRejectedValue(new Error("ECONNREFUSED"))],
    ["answers 500", () => fetchMock.mockResolvedValue(fakeResponse({ error: "boom" }, { ok: false, status: 500 }))],
    ["returns a link that is not https", () => fetchMock.mockResolvedValue(fakeResponse({ url: "javascript:alert(1)" }))],
  ])("falls back to the stored link while it is under seven days old, when the Engine %s", async (_case, arrange) => {
    store.getGenerationForUser.mockResolvedValue(delivered());
    arrange();

    const res = await get();

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe(STORED);
  });

  it("answers 503, not a dead link, when the Engine fails and the stored link has expired", async () => {
    const eightDaysAgo = new Date(Date.now() - 8 * 24 * 60 * 60 * 1000).toISOString();
    store.getGenerationForUser.mockResolvedValue(delivered({ completed_at: eightDaysAgo }));
    fetchMock.mockRejectedValue(new Error("ECONNREFUSED"));

    const res = await get();

    expect(res.status).toBe(503);
    expect(res.headers.get("location")).toBeNull();
    expect(await res.text()).toBe("The download could not be prepared. Please try again in a moment.");
  });

  it("answers 503 when the build cannot be looked up", async () => {
    store.getGenerationForUser.mockRejectedValue(new Error("db down"));

    const res = await get();

    expect(res.status).toBe(503);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
