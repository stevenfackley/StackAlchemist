// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const { createServerClient, cookies } = vi.hoisted(() => ({ createServerClient: vi.fn(), cookies: vi.fn() }));
vi.mock("@supabase/ssr", () => ({ createServerClient }));
vi.mock("next/headers", () => ({ cookies }));

describe("GET /auth/callback (Qavren mode)", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("sends the browser to /login without touching Supabase", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    vi.stubEnv("NEXT_PUBLIC_APP_URL", "https://stackalchemist.app/");
    const { GET } = await import("@/app/auth/callback/route");
    const res = await GET(new NextRequest("http://0.0.0.0:3000/auth/callback?code=abc&next=/dashboard"));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("https://stackalchemist.app/login");
    expect(cookies).not.toHaveBeenCalled();
    expect(createServerClient).not.toHaveBeenCalled();
  });
});
