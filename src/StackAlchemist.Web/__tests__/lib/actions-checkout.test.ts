/**
 * actions.ts — createCheckoutSession.
 *
 * `actions.ts` never talks to the Stripe SDK directly: it POSTs to the .NET
 * Engine's `/api/stripe/create-session` endpoint, which is the layer that
 * actually holds a Stripe price/secret key. So "missing price ID" isn't a
 * Next.js-side concern here — these tests instead cover the pieces that DO
 * live in this file: the tier-0 guard, the demo/no-Stripe fallback, origin
 * resolution from request headers, and Engine error propagation.
 */
import { createCheckoutSession } from "@/lib/actions";
import { fakeResponse } from "./actions-test-helpers";

const hasStripeConfigMock = vi.fn(() => true);
const hasEngineConfigMock = vi.fn(() => true);

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: false,
  // `actions.ts` also imports `createServerClient` from "./supabase", and
  // that module calls `hasPublicSupabaseConfig()` at import time to decide
  // whether to build a *browser* Supabase client — it must be present on
  // this mock even though createCheckoutSession itself never calls it.
  // Keep it false so the real `createBrowserClient` (which needs real env
  // vars) never actually runs in this test file.
  hasPublicSupabaseConfig: vi.fn(() => false),
  hasEngineConfig: () => hasEngineConfigMock(),
  hasServerSupabaseConfig: vi.fn(() => true),
  hasStripeConfig: () => hasStripeConfigMock(),
  getEngineServiceKey: vi.fn(() => ""),
}));

const headersMock = vi.fn();
vi.mock("next/headers", () => ({ headers: () => headersMock() }));

let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

describe("actions.ts — createCheckoutSession", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.stubEnv("NODE_ENV", "test");
    hasStripeConfigMock.mockReturnValue(true);
    hasEngineConfigMock.mockReturnValue(true);
    headersMock.mockReset();
    headersMock.mockResolvedValue(new Headers({ host: "app.stackalchemist.app" }));
    consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    consoleErrorSpy.mockRestore();
  });

  it("rejects tier 0 — Spark is free, no checkout needed", async () => {
    const result = await createCheckoutSession("gen-1", 0);
    expect(result).toEqual({
      success: false,
      error: "Tier 0 (Spark) is free — no checkout required.",
    });
    expect(headersMock).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("falls back to a demo redirect when Stripe isn't configured", async () => {
    hasStripeConfigMock.mockReturnValue(false);

    const result = await createCheckoutSession("gen-2", 1);
    expect(result).toEqual({ success: true, sessionUrl: "/generate/gen-2?demo=1&tier=1" });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("falls back to a demo redirect when the Engine isn't configured", async () => {
    hasEngineConfigMock.mockReturnValue(false);

    const result = await createCheckoutSession("gen-3", 2);
    expect(result).toEqual({ success: true, sessionUrl: "/generate/gen-3?demo=1&tier=2" });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("creates a real session, deriving origin/cancel path from mode when no cancelPath is given", async () => {
    fetchMock.mockResolvedValue(
      fakeResponse({ url: "https://checkout.stripe.com/session/abc" })
    );

    const result = await createCheckoutSession("gen-4", 2, "make me an app", "DotNetNextJs", "simple");

    expect(result).toEqual({ success: true, sessionUrl: "https://checkout.stripe.com/session/abc" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/stripe/create-session");
    const body = JSON.parse(init.body);
    expect(body.successUrl).toBe(
      "http://app.stackalchemist.app/generate/gen-4?session_id={CHECKOUT_SESSION_ID}"
    );
    expect(body.cancelUrl).toContain("/simple?q=make%20me%20an%20app&tier=2");
  });

  it("uses the advanced-mode default cancel path when mode is advanced", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/xyz" }));

    await createCheckoutSession("gen-5", 3, undefined, "PythonReact", "advanced");

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.cancelUrl).toContain("/advanced?step=4&tier=3&projectType=PythonReact");
  });

  it("prefers an explicit cancelPath over the mode default", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/qqq" }));

    await createCheckoutSession("gen-6", 1, "p", "DotNetNextJs", "simple", "/generate/gen-6?upgrade=1");

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.cancelUrl).toBe("http://app.stackalchemist.app/generate/gen-6?upgrade=1");
  });

  it("resolves https origin from x-forwarded-proto even outside production", async () => {
    headersMock.mockResolvedValue(
      new Headers({ host: "app.stackalchemist.app", "x-forwarded-proto": "https" })
    );
    fetchMock.mockResolvedValue(fakeResponse({ url: "https://checkout.stripe.com/session/ssl" }));

    await createCheckoutSession("gen-7", 1);

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

    await createCheckoutSession("gen-7b", 1);

    const [, init] = fetchMock.mock.calls[0];
    const body = JSON.parse(init.body);
    expect(body.successUrl.startsWith("https://")).toBe(true);
  });

  it("propagates the Engine's error message on a non-2xx response", async () => {
    fetchMock.mockResolvedValue(fakeResponse({ error: "Card declined" }, { ok: false, status: 402 }));

    const result = await createCheckoutSession("gen-8", 1);
    expect(result).toEqual({ success: false, error: "Card declined" });
  });

  it("uses the json()-parse-failure fallback message when the non-2xx body isn't valid JSON", async () => {
    fetchMock.mockResolvedValue(fakeResponse("", { ok: false, status: 500, jsonThrows: true }));

    const result = await createCheckoutSession("gen-9", 1);
    // `.json().catch(() => ({ error: "Checkout session creation failed." }))`
    // supplies its own `error` field, so the `body.error ?? "Failed to create
    // checkout session."` fallback further down never actually triggers here.
    expect(result).toEqual({ success: false, error: "Checkout session creation failed." });
  });

  it("uses the 'Failed to create checkout session' fallback when the JSON body has no error field", async () => {
    fetchMock.mockResolvedValue(fakeResponse({}, { ok: false, status: 500 }));

    const result = await createCheckoutSession("gen-9b", 1);
    expect(result).toEqual({ success: false, error: "Failed to create checkout session." });
  });

  it("returns a sane error when the fetch itself throws", async () => {
    fetchMock.mockRejectedValue(new Error("network down"));

    const result = await createCheckoutSession("gen-10", 1);
    expect(result).toEqual({
      success: false,
      error: "Failed to reach the payment service. Please try again.",
    });
  });
});
