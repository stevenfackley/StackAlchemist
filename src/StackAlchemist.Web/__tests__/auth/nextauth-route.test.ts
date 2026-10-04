// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const { loaded, AUTHJS_SPY } = vi.hoisted(() => ({
  loaded: vi.fn(),
  AUTHJS_SPY: vi.fn(async () => new Response("authjs", { status: 200 })),
}));
// The factory runs when @/auth is imported, so `loaded` records a load. Vitest
// caches a factory's result across vi.resetModules(), so the mock is registered
// afresh in every test (and dropped in afterEach) to keep `loaded` per-test.
const mockAuth = () =>
  vi.doMock("@/auth", () => {
    loaded();
    return { handlers: { GET: AUTHJS_SPY, POST: AUTHJS_SPY } };
  });

const req = (method: "GET" | "POST") =>
  new NextRequest("http://localhost:3000/api/auth/session", { method });

describe("/api/auth/[...nextauth]", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.doUnmock("@/auth");
    vi.resetModules();
    loaded.mockClear();
    AUTHJS_SPY.mockClear();
  });

  it.each(["GET", "POST"] as const)("%s 404s in demo mode (no QAVREN_AUTH_URL) without loading Auth.js", async (method) => {
    mockAuth();
    vi.stubEnv("QAVREN_AUTH_URL", "");
    const route = await import("@/app/api/auth/[...nextauth]/route");
    const res = await route[method](req(method));
    expect(res.status).toBe(404);
    expect(await res.text()).toBe("");
    expect(loaded).not.toHaveBeenCalled();
    expect(AUTHJS_SPY).not.toHaveBeenCalled();
  });

  it.each(["GET", "POST"] as const)("%s delegates to Auth.js in Qavren mode", async (method) => {
    mockAuth();
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    const route = await import("@/app/api/auth/[...nextauth]/route");
    const res = await route[method](req(method));
    expect(res.status).toBe(200);
    expect(loaded).toHaveBeenCalledOnce();
    expect(AUTHJS_SPY).toHaveBeenCalledOnce();
  });
});
