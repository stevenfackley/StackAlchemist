// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

// Auth.js's `auth(handler)` wraps the handler with session resolution; unwrap
// so the guard itself is under test. The session is supplied per case as req.auth.
// Registered per test with doMock so the "never loaded" assertion is order-independent
// (vi.mock factories are cached across vi.resetModules()).
const loaded = vi.hoisted(() => vi.fn());
type Authed = NextRequest & { auth?: { user?: { id?: string } } | null };
const SUB = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

function mockAuth() {
  vi.doMock("@/auth", () => { loaded(); return { auth: (handler: unknown) => handler }; });
}
const req = (url: string, session: { user?: { id?: string } } | null | undefined = undefined, init?: RequestInit) => {
  const r = new NextRequest(url, init) as Authed;
  if (session !== undefined) r.auth = session;
  return r;
};
async function run(r: NextRequest) {
  const { proxy } = await import("@/proxy");
  return (proxy as unknown as (r: NextRequest, e: unknown) => Promise<Response>)(r, {});
}

describe("proxy", () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.doUnmock("@/auth"); vi.resetModules(); loaded.mockClear(); });

  it("Supabase mode with no Supabase env passes through and never loads @/auth", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", ""); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    const res = await run(req("http://localhost:3000/generate/abc"));
    expect(res.status).toBe(200);
    expect(loaded).not.toHaveBeenCalled();
  });

  it("Qavren mode: an anonymous protected request redirects to /login with returnTo", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    const res = await run(req("http://localhost:3000/generate/abc?tier=0", null));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("http://localhost:3000/login?returnTo=%2Fgenerate%2Fabc%3Ftier%3D0");
    expect(loaded).toHaveBeenCalledTimes(1);
  });

  it("Qavren mode: a signed-in request and every public route pass through", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    expect((await run(req("http://localhost:3000/simple", { user: { id: SUB } }))).status).toBe(200);
    for (const p of ["/", "/pricing", "/login", "/register", "/dashboard", "/simplex", "/generated"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(200);
    }
  });

  it("Qavren mode: a session without a user id is not a session", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    expect((await run(req("http://localhost:3000/advanced", { user: {} }))).status).toBe(307);
  });

  it("Qavren mode: Auth.js routes pass through without the gate; /api/author is not one of them", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false"); vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "");
    mockAuth();
    for (const p of ["/api/auth", "/api/auth/session", "/api/auth/callback/keycloak"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(200);
    }
    expect(loaded).not.toHaveBeenCalled();
    // Not protected, so it passes too, but through the gate: the prefix check is exact.
    expect((await run(req("http://localhost:3000/api/author/x", null))).status).toBe(200);
    expect(loaded).toHaveBeenCalledTimes(1);
  });

  it("test-mirror Basic Auth runs before either mode", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false");
    vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "true"); vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("TEST_SITE_BASIC_AUTH_USER", "u"); vi.stubEnv("TEST_SITE_BASIC_AUTH_PASS", "p");
    mockAuth();
    const denied = await run(req("http://localhost:3000/", null));
    expect(denied.status).toBe(401);
    expect(denied.headers.get("www-authenticate")).toContain("Basic");
    // Auth.js routes are exempt from the gate, not from the mirror's Basic Auth.
    expect((await run(req("http://localhost:3000/api/auth/session", null))).status).toBe(401);
    expect(loaded).not.toHaveBeenCalled();
    const ok = req("http://localhost:3000/", null, { headers: { authorization: `Basic ${Buffer.from("u:p").toString("base64")}` } });
    expect((await run(ok)).status).toBe(200);
    expect((await run(req("http://localhost:3000/api/healthz", null))).status).toBe(200);
    expect((await run(req("http://localhost:3000/api/csp-report", null))).status).toBe(200);
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
