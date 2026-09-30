// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/data/drizzle-store", () => ({ DrizzleStore: class { kind = "drizzle" as const; } }));
vi.mock("@/lib/data/supabase-store", () => ({ SupabaseStore: class { kind = "supabase" as const; } }));

describe("getDataStore", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("picks Drizzle when DATABASE_URL is set", async () => {
    vi.stubEnv("DATABASE_URL", "postgres://u:p@localhost:5432/db");
    const { getDataStore } = await import("@/lib/data");
    expect(getDataStore().kind).toBe("drizzle");
  });

  it("re-reads DATABASE_URL on every call, not at module load", async () => {
    const { getDataStore } = await import("@/lib/data");

    vi.stubEnv("DATABASE_URL", "postgres://u:p@localhost:5432/db");
    expect(getDataStore().kind).toBe("drizzle");

    vi.stubEnv("DATABASE_URL", "");
    expect(getDataStore().kind).toBe("supabase");
  });

  it("falls back to Supabase when DATABASE_URL is unset", async () => {
    vi.stubEnv("DATABASE_URL", "");
    const { getDataStore } = await import("@/lib/data");
    expect(getDataStore().kind).toBe("supabase");
  });
});
