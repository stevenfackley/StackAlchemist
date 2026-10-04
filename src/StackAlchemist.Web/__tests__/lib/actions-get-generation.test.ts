/**
 * actions.ts — getGeneration is owner-only.
 *
 * The /generate/[id] result page and the status watcher's polling both read
 * through getGeneration. A server action is callable from any page with any
 * argument, so the ownership rule has to live in the read itself: the store
 * query is scoped to the signed-in user's id in SQL, a signed-out caller gets
 * nothing, and someone else's id answers exactly like an unknown one (null),
 * so the response is not an existence oracle.
 */
import { getDataStore } from "@/lib/data";
import { DataStoreError } from "@/lib/data/store";
import { getSessionUser } from "@/lib/session";
import { getGeneration } from "@/lib/actions";
import { makeStore, type FakeStore } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));

const GEN_ID = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";
const OWNER_ID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
const INTRUDER_ID = "9c8d7e6f-1a2b-4c3d-8e4f-5a6b7c8d9e0f";

describe("actions.ts — getGeneration is owner-only", () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
  let store: FakeStore;

  beforeEach(() => {
    vi.mocked(getSessionUser).mockReset();
    store = makeStore();
    vi.mocked(getDataStore).mockReset();
    vi.mocked(getDataStore).mockReturnValue(store);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("returns null for a signed-out caller without touching the database", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(getDataStore).not.toHaveBeenCalled();
    // A clean null, not a swallowed `user.id` TypeError: without the explicit
    // signed-out guard the catch would still return null, but would log.
    expect(consoleErrorSpy).not.toHaveBeenCalled();
  });

  it("scopes the read to the signed-in user's id and returns the owner's row", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: OWNER_ID, email: null });
    const row = { id: GEN_ID, user_id: OWNER_ID, status: "success" };
    store.getGenerationForUser.mockResolvedValue(row as never);

    await expect(getGeneration(GEN_ID)).resolves.toEqual(row);
    expect(store.getGenerationForUser).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
  });

  it("returns null for someone else's generation, the same answer as an unknown id", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: INTRUDER_ID, email: null });
    // The scoped query matches nothing for the intruder.
    store.getGenerationForUser.mockResolvedValue(null);

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(store.getGenerationForUser).toHaveBeenCalledWith(GEN_ID, INTRUDER_ID);
  });

  it("returns null, not a throw, when the read fails", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: OWNER_ID, email: null });
    store.getGenerationForUser.mockRejectedValue(new DataStoreError("generations select failed"));

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(consoleErrorSpy).toHaveBeenCalled();
  });
});
