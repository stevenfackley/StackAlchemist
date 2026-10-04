/**
 * actions.ts — createCheckoutSession.
 *
 * `actions.ts` never talks to the Stripe SDK directly: it POSTs to the .NET
 * Engine's `/api/stripe/create-session` endpoint, which is the layer that
 * actually holds a Stripe price/secret key. So "missing price ID" isn't a
 * Next.js-side concern here — these tests instead cover the pieces that DO
 * live in this file: the tier-0 guard, the demo/no-Stripe fallback, origin
 * resolution from request headers, Engine error propagation, and the owner
 * check: a checkout acts on an existing generation row, so (like the result
 * page) only the signed-in owner of that row may start one.
 */
import { getDataStore } from "@/lib/data";
import { DataStoreError } from "@/lib/data/store";
import { getSessionUser } from "@/lib/session";
import { createCheckoutSession } from "@/lib/actions";
import { fakeResponse, makeStore, type FakeStore } from "./actions-test-helpers";

const hasStripeConfigMock = vi.fn(() => true);
const hasEngineConfigMock = vi.fn(() => true);
const hasDataStoreConfigMock = vi.fn(() => true);

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  hasEngineConfig: () => hasEngineConfigMock(),
  hasDataStoreConfig: () => hasDataStoreConfigMock(),
  hasStripeConfig: () => hasStripeConfigMock(),
  getEngineServiceKey: vi.fn(() => ""),
}));

const headersMock = vi.fn();
vi.mock("next/headers", () => ({ headers: () => headersMock() }));
vi.mock("@/lib/session", () => ({ getSessionUser: vi.fn() }));
vi.mock("@/lib/data", () => ({ getDataStore: vi.fn() }));

// Session ids are Keycloak subs, and generation ids are UUIDs too.
const GEN_ID = "0b5d3c1e-6f7a-4c2d-9e8f-1a2b3c4d5e6f";
const OWNER_ID = "3f2b8c1a-5d4e-4f6a-9b7c-8d9e0f1a2b3c";
const INTRUDER_ID = "9c8d7e6f-1a2b-4c3d-8e4f-5a6b7c8d9e0f";

/** The signed-in owner's row, as the scoped read returns it. */
const OWNED_ROW = { id: GEN_ID, user_id: OWNER_ID, tier: 0, status: "success" };

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
let store: FakeStore;

describe("actions.ts — createCheckoutSession", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.stubEnv("NODE_ENV", "test");
    hasStripeConfigMock.mockReturnValue(true);
    hasEngineConfigMock.mockReturnValue(true);
    hasDataStoreConfigMock.mockReturnValue(true);
    headersMock.mockReset();
    headersMock.mockResolvedValue(new Headers({ host: "app.stackalchemist.app" }));
    // Default: the signed-in caller owns GEN_ID.
    vi.mocked(getSessionUser).mockReset();
    vi.mocked(getSessionUser).mockResolvedValue({ id: OWNER_ID, email: null });
    store = makeStore();
    store.getGenerationForUser.mockImplementation(async (id, userId) =>
      id === GEN_ID && userId === OWNER_ID ? (OWNED_ROW as never) : null
    );
    vi.mocked(getDataStore).mockReset();
    vi.mocked(getDataStore).mockReturnValue(store);
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    consoleErrorSpy.mockRestore();
  });

  it("rejects tier 0 — Spark is free, no checkout needed", async () => {
    const result = await createCheckoutSession(GEN_ID, 0);
    expect(result).toEqual({
      success: false,
      error: "Tier 0 (Spark) is free — no checkout required.",
    });
    expect(headersMock).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("falls back to a demo redirect when Stripe isn't configured", async () => {
    hasStripeConfigMock.mockReturnValue(false);

    const result = await createCheckoutSession(GEN_ID, 1);
    expect(result).toEqual({ success: true, sessionUrl: `/generate/${GEN_ID}?demo=1&tier=1` });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("falls back to a demo redirect when the Engine isn't configured", async () => {
    hasEngineConfigMock.mockReturnValue(false);

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: true, sessionUrl: `/generate/${GEN_ID}?demo=1&tier=2` });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects a signed-out caller before calling the Engine", async () => {
    vi.mocked(getSessionUser).mockResolvedValue(null);

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: false, error: "Please sign in to continue to checkout." });
    expect(getDataStore).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects someone else's generation with the not-found message, before calling the Engine", async () => {
    vi.mocked(getSessionUser).mockResolvedValue({ id: INTRUDER_ID, email: null });
    // The read is scoped to the caller in the query, so the intruder's lookup matches nothing.

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(store.getGenerationForUser).toHaveBeenCalledWith(GEN_ID, INTRUDER_ID);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("reads the generation scoped to the signed-in owner before calling the Engine", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/own" }));

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: true, sessionUrl: "https://checkout.stripe.com/session/own" });
    expect(store.getGenerationForUser).toHaveBeenCalledWith(GEN_ID, OWNER_ID);
    expect(store.getGenerationForUser.mock.invocationCallOrder[0]).toBeLessThan(fetchMock.mock.invocationCallOrder[0]);
  });

  it("refuses, without calling the Engine, when the ownership lookup itself fails", async () => {
    store.getGenerationForUser.mockRejectedValue(new DataStoreError("generations select failed"));

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(consoleErrorSpy).toHaveBeenCalledWith("[createCheckoutSession] Lookup error:", expect.anything());
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refuses, without calling the Engine, when no data store is configured", async () => {
    hasDataStoreConfigMock.mockReturnValue(false);

    const result = await createCheckoutSession(GEN_ID, 2);
    expect(result).toEqual({ success: false, error: "Generation not found." });
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      expect.stringContaining("[createCheckoutSession] No data store configured")
    );
    expect(getDataStore).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("creates a real session, deriving origin/cancel path from mode when no cancelPath is given", async () => {
    fetchMock.mockResolvedValue(
      fakeResponse({ url: "https://checkout.stripe.com/session/abc" })
    );

    const result = await createCheckoutSession(GEN_ID, 2, "make me an app", "DotNetNextJs", "simple");

    expect(result).toEqual({ success: true, sessionUrl: "https://checkout.stripe.com/session/abc" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/stripe/create-session");
    const body = JSON.parse(init.body);
    expect(body.successUrl).toBe(
      `http://app.stackalchemist.app/generate/${GEN_ID}?session_id={CHECKOUT_SESSION_ID}`
    );
    expect(body.cancelUrl).toContain("/simple?q=make%20me%20an%20app&tier=2");
  });

  it("uses the advanced-mode default cancel path when mode is advanced", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/xyz" }));

    await createCheckoutSession(GEN_ID, 3, undefined, "PythonReact", "advanced");

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.cancelUrl).toContain("/advanced?step=4&tier=3&projectType=PythonReact");
  });

  it("prefers an explicit cancelPath over the mode default", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/qqq" }));

    await createCheckoutSession(GEN_ID, 1, "p", "DotNetNextJs", "simple", `/generate/${GEN_ID}?upgrade=1`);

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.cancelUrl).toBe(`http://app.stackalchemist.app/generate/${GEN_ID}?upgrade=1`);
  });

  it("resolves https origin from x-forwarded-proto even outside production", async () => {
    headersMock.mockResolvedValue(
      new Headers({ host: "app.stackalchemist.app", "x-forwarded-proto": "https" })
    );
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/ssl" }));

    await createCheckoutSession(GEN_ID, 1);

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.successUrl.startsWith("https://")).toBe(true);
  });

  it("resolves https origin in production even without an x-forwarded-proto header", async () => {
    vi.stubEnv("NODE_ENV", "production");
    // resolveEngineUrl() throws in production unless ENGINE_API_URL is set —
    // stub it so the request still goes out.
    vi.stubEnv("ENGINE_API_URL", "https://engine.internal");
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/prod" }));

    await createCheckoutSession(GEN_ID, 1);

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.successUrl.startsWith("https://")).toBe(true);
  });

  it("propagates the Engine's error message on a non-2xx response", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ error: "Card declined" }, { ok: false, status: 402 }));

    const result = await createCheckoutSession(GEN_ID, 1);
    expect(result).toEqual({ success: false, error: "Card declined" });
  });

  it("uses the json()-parse-failure fallback message when the non-2xx body isn't valid JSON", async () => {
    fetchMock.mockResolvedValue(fakeResponse("", { ok: false, status: 500, jsonThrows: true }));

    const result = await createCheckoutSession(GEN_ID, 1);
    // `.json().catch(() => ({ error: "Checkout session creation failed." }))`
    // supplies its own `error` field, so the `body.error ?? "Failed to create
    // checkout session."` fallback further down never actually triggers here.
    expect(result).toEqual({ success: false, error: "Checkout session creation failed." });
  });

  it("uses the 'Failed to create checkout session' fallback when the JSON body has no error field", async () => {
    fetchMock.mockResolvedValue(fakeResponse({}, { ok: false, status: 500 }));

    const result = await createCheckoutSession(GEN_ID, 1);
    expect(result).toEqual({ success: false, error: "Failed to create checkout session." });
  });

  it("returns a sane error when the fetch itself throws", async () => {
    fetchMock.mockRejectedValue(new Error("network down"));

    const result = await createCheckoutSession(GEN_ID, 1);
    expect(result).toEqual({
      success: false,
      error: "Failed to reach the payment service. Please try again.",
    });
  });
});
