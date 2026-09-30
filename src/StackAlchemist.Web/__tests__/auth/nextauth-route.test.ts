// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const GET_SPY = vi.fn(async () => new Response("authjs", { status: 200 }));
vi.mock("@/auth", () => ({ handlers: { GET: GET_SPY, POST: GET_SPY } }));

describe("/api/auth/[...nextauth]", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
    GET_SPY.mockClear();
  });

  it("404s in Supabase mode without touching Auth.js", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    const { GET } = await import("@/app/api/auth/[...nextauth]/route");
    const res = await GET(new NextRequest("http://localhost:3000/api/auth/session"));
    expect(res.status).toBe(404);
    expect(GET_SPY).not.toHaveBeenCalled();
  });

  it("delegates to Auth.js in Qavren mode", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    const { GET } = await import("@/app/api/auth/[...nextauth]/route");
    const res = await GET(new NextRequest("http://localhost:3000/api/auth/session"));
    expect(res.status).toBe(200);
    expect(GET_SPY).toHaveBeenCalledOnce();
  });
});
