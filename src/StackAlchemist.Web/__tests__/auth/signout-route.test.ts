// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const { getToken, authjsSignOut, supabaseSignOut, createServerClient, cookieStore } = vi.hoisted(() => {
  const supabaseSignOut = vi.fn(async () => ({ error: null }));
  return {
    getToken: vi.fn(),
    authjsSignOut: vi.fn(),
    supabaseSignOut,
    createServerClient: vi.fn(() => ({ auth: { signOut: supabaseSignOut } })),
    cookieStore: { getAll: () => [], set: vi.fn() },
  };
});
vi.mock("next-auth/jwt", () => ({ getToken }));
vi.mock("@/auth", () => ({ signOut: authjsSignOut }));
vi.mock("@supabase/ssr", () => ({ createServerClient }));
vi.mock("next/headers", () => ({ cookies: async () => cookieStore }));

const URL_ = "http://localhost:3000/auth/signout";
const post = (headers: Record<string, string> = {}) => new NextRequest(URL_, { method: "POST", headers });
const LOGOUT = "http://localhost:8090/realms/stackalchemist-dev/protocol/openid-connect/logout";
const SECRET = "test-auth-secret";
// The attribute, not the `__Secure-` prefix in the name.
const SECURE_ATTR = /;\s*Secure(;|$)/i;

/** The Set-Cookie line the response carries for `name`, if any. */
const setCookie = (res: Response, name: string) =>
  res.headers.getSetCookie().find((line) => line.startsWith(`${name}=`));

describe("POST /auth/signout (Qavren mode)", () => {
  beforeEach(() => {
    getToken.mockReset();
    authjsSignOut.mockReset();
    getToken.mockResolvedValue({ sub: "u", idToken: "idt" });
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    vi.stubEnv("QAVREN_REALM", "stackalchemist-dev");
    vi.stubEnv("NEXT_PUBLIC_APP_URL", "http://localhost:3000");
    vi.stubEnv("AUTH_SECRET", SECRET);
  });
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("refuses a cross-origin or cross-site post without reading the token or signing out", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const cookie = "authjs.session-token=abc";
    expect((await POST(post({ cookie, origin: "https://evil.example" }))).status).toBe(403);
    expect((await POST(post({ cookie, "sec-fetch-site": "cross-site" }))).status).toBe(403);
    expect(getToken).not.toHaveBeenCalled();
    expect(authjsSignOut).not.toHaveBeenCalled();
  });

  it("ends the keycloak session with the id token hint and returns home", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const req = post({ origin: "http://localhost:3000", cookie: "authjs.session-token=abc" });
    const res = await POST(req);
    expect(getToken).toHaveBeenCalledWith({ req, secret: SECRET, secureCookie: false });
    expect(authjsSignOut).toHaveBeenCalledWith({ redirect: false });
    expect(res.status).toBe(303);
    const end = new URL(res.headers.get("location")!);
    expect(`${end.origin}${end.pathname}`).toBe(LOGOUT);
    expect(end.searchParams.get("id_token_hint")).toBe("idt");
    expect(end.searchParams.get("post_logout_redirect_uri")).toBe("http://localhost:3000/");
  });

  it("deletes every session cookie the request carried, chunks included, and nothing else", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const res = await POST(
      post({
        origin: "http://localhost:3000",
        cookie: "authjs.session-token=abc; authjs.session-token.1=def; theme=dark; authjs.csrf-token=x",
      }),
    );
    expect(res.status).toBe(303);
    for (const name of ["authjs.session-token", "authjs.session-token.1"]) {
      const line = setCookie(res, name);
      expect(line, name).toMatch(/^[^=]+=;/);
      expect(line, name).toMatch(/Max-Age=0/i);
      expect(line, name).toMatch(/Path=\//i);
      expect(line, name).toMatch(/HttpOnly/i);
      expect(line, name).not.toMatch(SECURE_ATTR);
    }
    expect(setCookie(res, "theme")).toBeUndefined();
    expect(setCookie(res, "authjs.csrf-token")).toBeUndefined();
  });

  it("follows the __Secure- cookie the browser sent, for getToken and for the deletion", async () => {
    // NEXT_PUBLIC_APP_URL says http; the cookie name is what Auth.js actually chose.
    const { POST } = await import("@/app/auth/signout/route");
    const req = post({ origin: "http://localhost:3000", cookie: "__Secure-authjs.session-token=abc" });
    const res = await POST(req);
    expect(getToken).toHaveBeenCalledWith({ req, secret: SECRET, secureCookie: true });
    const line = setCookie(res, "__Secure-authjs.session-token");
    expect(line).toMatch(/Max-Age=0/i);
    expect(line).toMatch(SECURE_ATTR);
  });

  it("falls back to client_id when no id token is on the jwt", async () => {
    getToken.mockResolvedValue({ sub: "u" });
    const { POST } = await import("@/app/auth/signout/route");
    const end = new URL((await POST(post({ "sec-fetch-site": "same-origin" }))).headers.get("location")!);
    expect(end.searchParams.get("client_id")).toBe("stackalchemist-dev-web");
    expect(end.searchParams.has("id_token_hint")).toBe(false);
  });
});

describe("POST /auth/signout (Supabase mode)", () => {
  beforeEach(() => {
    getToken.mockReset();
    authjsSignOut.mockReset();
    createServerClient.mockClear();
    supabaseSignOut.mockClear();
    vi.stubEnv("QAVREN_AUTH_URL", "");
    vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", "https://stub.supabase.co");
    vi.stubEnv("NEXT_PUBLIC_SUPABASE_ANON_KEY", "sb_publishable_stub");
  });
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("signs out of Supabase, redirects home and never touches Auth.js", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const res = await POST(post({ origin: "http://localhost:3000", cookie: "authjs.session-token=abc" }));
    expect(createServerClient).toHaveBeenCalledWith(
      "https://stub.supabase.co",
      "sb_publishable_stub",
      expect.objectContaining({ cookies: expect.any(Object) }),
    );
    expect(supabaseSignOut).toHaveBeenCalledOnce();
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("http://localhost:3000/");
    expect(authjsSignOut).not.toHaveBeenCalled();
    expect(getToken).not.toHaveBeenCalled();
  });
});
