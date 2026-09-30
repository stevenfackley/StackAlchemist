// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

// Auth.js's `auth(handler)` wraps the handler with session resolution; unwrap
// so the guard itself is under test. The session is supplied per case as req.auth.
// Registered per test with doMock so the "never loaded" assertion is order-independent
// (vi.mock factories are cached across vi.resetModules()).
const loaded = vi.hoisted(() => vi.fn());
// Stands in for @supabase/ssr's createServerClient: a signed-out client.
const createServerClient = vi.hoisted(() =>
  vi.fn(() => ({ auth: { getUser: async () => ({ data: { user: null } }) } })),
);
type Authed = NextRequest & { auth?: { user?: { id?: string } } | null };
const SUB = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

function mockAuth() {
  vi.doMock("@/auth", () => { loaded(); return { auth: (handler: unknown) => handler }; });
}
function mockSupabase() {
  vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", "https://stub.supabase.co");
  vi.stubEnv("NEXT_PUBLIC_SUPABASE_ANON_KEY", "sb_publishable_stub");
  vi.doMock("@supabase/ssr", () => ({ createServerClient }));
}
const req = (url: string, session: { user?: { id?: string } } | null | undefined = undefined, init?: ConstructorParameters<typeof NextRequest>[1]) => {
  const r = new NextRequest(url, init) as Authed;
  if (session !== undefined) r.auth = session;
  return r;
};
async function run(r: NextRequest) {
  const { proxy } = await import("@/proxy");
  return (proxy as unknown as (r: NextRequest, e: unknown) => Promise<Response>)(r, {});
}
function qavrenMode() {
  vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
}

describe("proxy", () => {
  afterEach(() => {
    vi.unstubAllEnvs(); vi.doUnmock("@/auth"); vi.doUnmock("@supabase/ssr"); vi.resetModules();
    loaded.mockClear(); createServerClient.mockClear();
  });

  it("Supabase mode with no Supabase env passes through and never loads @/auth", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", ""); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    const res = await run(req("http://localhost:3000/generate/abc"));
    expect(res.status).toBe(200);
    expect(loaded).not.toHaveBeenCalled();
  });

  it("Supabase mode: Auth.js routes pass through without the Supabase refresh", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth(); mockSupabase();
    expect((await run(req("http://localhost:3000/api/auth/session"))).status).toBe(200);
    expect(createServerClient).not.toHaveBeenCalled();
    expect(loaded).not.toHaveBeenCalled();
    // Control: any other route does run the refresh.
    expect((await run(req("http://localhost:3000/pricing"))).status).toBe(200);
    expect(createServerClient).toHaveBeenCalledTimes(1);
  });

  it("Qavren mode: an anonymous protected request redirects to /login with returnTo", async () => {
    qavrenMode();
    mockAuth();
    const res = await run(req("http://localhost:3000/generate/abc?tier=0", null));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("http://localhost:3000/login?returnTo=%2Fgenerate%2Fabc%3Ftier%3D0");
    expect(loaded).toHaveBeenCalledTimes(1);
  });

  it("Qavren mode: percent-encoded protected paths are gated on their decoded form", async () => {
    qavrenMode();
    mockAuth();
    for (const p of ["/%73imple", "/generate/%61bc", "/%61dvanced"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(307);
    }
    // A malformed escape cannot be decoded; the raw path is not protected.
    expect((await run(req("http://localhost:3000/%zz", null))).status).toBe(200);
  });

  it("Qavren mode: a signed-in request and every public route pass through", async () => {
    qavrenMode();
    mockAuth();
    expect((await run(req("http://localhost:3000/simple", { user: { id: SUB } }))).status).toBe(200);
    for (const p of ["/", "/pricing", "/login", "/register", "/dashboard", "/simplex", "/generated"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(200);
    }
  });

  it("Qavren mode: a session without a user id is not a session", async () => {
    qavrenMode();
    mockAuth();
    expect((await run(req("http://localhost:3000/advanced", { user: {} }))).status).toBe(307);
  });

  it("Qavren mode: demo mode passes an anonymous protected request without loading Auth.js", async () => {
    qavrenMode(); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "true");
    mockAuth();
    expect((await run(req("http://localhost:3000/simple", null))).status).toBe(200);
    expect(loaded).not.toHaveBeenCalled();
  });

  it("Qavren mode: Auth.js routes pass through without the gate; /api/author is not one of them", async () => {
    qavrenMode();
    mockAuth();
    for (const p of ["/api/auth", "/api/auth/session", "/api/auth/callback/keycloak"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(200);
    }
    expect(loaded).not.toHaveBeenCalled();
    // Not protected, so it passes too, but through the gate: the prefix check is exact.
    expect((await run(req("http://localhost:3000/api/author/x", null))).status).toBe(200);
    expect(loaded).toHaveBeenCalledTimes(1);
  });

  it("Qavren mode: POST /auth/signout passes through without the gate, so no refreshed session cookie races the deletion", async () => {
    qavrenMode();
    mockAuth();
    const res = await run(req("http://localhost:3000/auth/signout", null, { method: "POST" }));
    expect(res.status).toBe(200);
    expect(res.headers.getSetCookie()).toEqual([]);
    expect(loaded).not.toHaveBeenCalled();
  });

  it("Supabase mode: POST /auth/signout passes through without the Supabase refresh", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth(); mockSupabase();
    expect((await run(req("http://localhost:3000/auth/signout", null, { method: "POST" }))).status).toBe(200);
    expect(createServerClient).not.toHaveBeenCalled();
    expect(loaded).not.toHaveBeenCalled();
  });

  it.each([
    ["Supabase", ""],
    ["Qavren", "http://localhost:8090"],
  ])("test-mirror Basic Auth runs before the auth mode (%s mode)", async (_mode, qavrenAuthUrl) => {
    vi.stubEnv("QAVREN_AUTH_URL", qavrenAuthUrl); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false");
    vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "true");
    vi.stubEnv("TEST_SITE_BASIC_AUTH_USER", "u"); vi.stubEnv("TEST_SITE_BASIC_AUTH_PASS", "p");
    mockAuth(); mockSupabase();
    const denied = await run(req("http://localhost:3000/", null));
    expect(denied.status).toBe(401);
    expect(denied.headers.get("www-authenticate")).toContain("Basic");
    // Auth.js routes are exempt from the gate, not from the mirror's Basic Auth.
    expect((await run(req("http://localhost:3000/api/auth/session", null))).status).toBe(401);
    // Unauthenticated traffic touches neither Supabase nor Auth.js.
    expect(createServerClient).not.toHaveBeenCalled();
    expect(loaded).not.toHaveBeenCalled();
    const ok = req("http://localhost:3000/", null, { headers: { authorization: `Basic ${Buffer.from("u:p").toString("base64")}` } });
    expect((await run(ok)).status).toBe(200);
    expect((await run(req("http://localhost:3000/api/healthz", null))).status).toBe(200);
    expect((await run(req("http://localhost:3000/api/csp-report", null))).status).toBe(200);
  });

  it("test-mirror Basic Auth fails open, with a warning, when its credentials are missing in production", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", "");
    vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "true"); vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("TEST_SITE_BASIC_AUTH_USER", ""); vi.stubEnv("TEST_SITE_BASIC_AUTH_PASS", "");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    try {
      expect((await run(req("http://localhost:3000/"))).status).toBe(200);
      expect(warn).toHaveBeenCalledWith(expect.stringContaining("running open"));
    } finally {
      warn.mockRestore();
    }
  });

  it("the matcher is today's: it skips static assets and still catches guarded and Auth.js paths", async () => {
    const { config } = await import("@/proxy");
    const m = new RegExp(`^${config.matcher[0]}$`);
    for (const skipped of ["/_next/static/c.js", "/_next/image", "/favicon.ico", "/logo.svg", "/x.png"]) {
      expect(m.test(skipped), skipped).toBe(false);
    }
    for (const kept of ["/generate/abc", "/simple", "/dashboard", "/api/healthz", "/api/csp-report", "/generate/a.b", "/api/auth/session", "/api/auth/callback/keycloak", "/api/author"]) {
      expect(m.test(kept), kept).toBe(true);
    }
  });
});
