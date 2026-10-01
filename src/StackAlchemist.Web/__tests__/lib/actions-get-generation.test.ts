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
import { getServerUser } from "@/lib/supabase-server";
import { createServerClient } from "@/lib/supabase";
import { getGeneration } from "@/lib/actions";
import { makeDb } from "./actions-test-helpers";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasPublicSupabaseConfig: vi.fn(() => false),
  hasEngineConfig: vi.fn(() => true),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasDataStoreConfig: vi.fn(() => true),
  usesPostgresStore: vi.fn(() => false),
  usesQavrenAuth: vi.fn(() => false),
  hasStripeConfig: vi.fn(() => true),
  getEngineServiceKey: vi.fn(() => ""),
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

const GEN_ID = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";
const OWNER_ID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
const INTRUDER_ID = "9c8d7e6f-1a2b-4c3d-8e4f-5a6b7c8d9e0f";

/** The `.eq(...)` calls the store made on its single query. */
function eqCalls(db: { from: ReturnType<typeof vi.fn> }) {
  const builder = db.from.mock.results[0]?.value as { eq: ReturnType<typeof vi.fn> } | undefined;
  return builder?.eq.mock.calls ?? [];
}

describe("actions.ts — getGeneration is owner-only", () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it("returns null for a signed-out caller without touching the database", async () => {
    vi.mocked(getServerUser).mockResolvedValue(null as never);

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("scopes the read to the signed-in user's id and returns the owner's row", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: OWNER_ID } as never);
    const row = { id: GEN_ID, user_id: OWNER_ID, status: "success" };
    const db = makeDb([{ data: row, error: null }]);
    vi.mocked(createServerClient).mockReturnValue(db as never);

    await expect(getGeneration(GEN_ID)).resolves.toEqual(row);
    expect(eqCalls(db)).toEqual(expect.arrayContaining([["id", GEN_ID], ["user_id", OWNER_ID]]));
  });

  it("returns null for someone else's generation, the same answer as an unknown id", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: INTRUDER_ID } as never);
    // The scoped query matches nothing for the intruder.
    const db = makeDb([{ data: null, error: null }]);
    vi.mocked(createServerClient).mockReturnValue(db as never);

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(eqCalls(db)).toContainEqual(["user_id", INTRUDER_ID]);
  });

  it("returns null, not a throw, when the read fails", async () => {
    vi.mocked(getServerUser).mockResolvedValue({ id: OWNER_ID } as never);
    vi.mocked(createServerClient).mockReturnValue(
      makeDb([{ data: null, error: { message: "boom", code: "XX000" } }]) as never
    );

    await expect(getGeneration(GEN_ID)).resolves.toBeNull();
    expect(consoleErrorSpy).toHaveBeenCalled();
  });
});
