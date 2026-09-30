// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const { getToken, signOut } = vi.hoisted(() => ({ getToken: vi.fn(), signOut: vi.fn() }));
vi.mock("next-auth/jwt", () => ({ getToken }));
vi.mock("@/auth", () => ({ signOut }));

const post = (headers: Record<string, string> = {}) =>
  new NextRequest("http://localhost:3000/auth/signout", { method: "POST", headers });
const LOGOUT = "http://localhost:8090/realms/stackalchemist-dev/protocol/openid-connect/logout";

describe("POST /auth/signout (Qavren mode)", () => {
  beforeEach(() => {
    getToken.mockReset();
    signOut.mockReset();
    getToken.mockResolvedValue({ sub: "u", idToken: "idt" });
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    vi.stubEnv("QAVREN_REALM", "stackalchemist-dev");
    vi.stubEnv("NEXT_PUBLIC_APP_URL", "http://localhost:3000");
  });
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("refuses a cross-origin or cross-site post without signing out", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    expect((await POST(post({ origin: "https://evil.example" }))).status).toBe(403);
    expect((await POST(post({ "sec-fetch-site": "cross-site" }))).status).toBe(403);
    expect(signOut).not.toHaveBeenCalled();
  });

  it("ends the keycloak session with the id token hint and returns home", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const res = await POST(post({ origin: "http://localhost:3000" }));
    expect(signOut).toHaveBeenCalledWith({ redirect: false });
    expect(res.status).toBe(303);
    const end = new URL(res.headers.get("location")!);
    expect(`${end.origin}${end.pathname}`).toBe(LOGOUT);
    expect(end.searchParams.get("id_token_hint")).toBe("idt");
    expect(end.searchParams.get("post_logout_redirect_uri")).toBe("http://localhost:3000/");
  });

  it("falls back to client_id when no id token is on the jwt", async () => {
    getToken.mockResolvedValue({ sub: "u" });
    const { POST } = await import("@/app/auth/signout/route");
    const end = new URL((await POST(post({ "sec-fetch-site": "same-origin" }))).headers.get("location")!);
    expect(end.searchParams.get("client_id")).toBe("stackalchemist-dev-web");
    expect(end.searchParams.has("id_token_hint")).toBe(false);
  });

  it("Supabase mode never loads Auth.js", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    // The Supabase branch needs its own env to do anything; without it, it throws or redirects —
    // we only assert Auth.js stayed cold.
    const { POST } = await import("@/app/auth/signout/route");
    await POST(post({ origin: "http://localhost:3000" })).catch(() => undefined);
    expect(signOut).not.toHaveBeenCalled();
    expect(getToken).not.toHaveBeenCalled();
  });
});
