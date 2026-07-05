/**
 * actions.ts — demo-mode / missing-config short circuits.
 *
 * When `isDemoMode` is true (or required config is absent) the server actions
 * must never touch Supabase or the .NET Engine — they return a synthetic
 * "demo-*" result instead. These tests assert both the returned shape *and*
 * that no network call or DB client was ever constructed.
 */
import { getServerUser } from "@/lib/supabase-server";
import { createServerClient } from "@/lib/supabase";
import {
  createCheckoutSession,
  createPendingGeneration,
  extractSchema,
  getFreeQuotaStatus,
  getMyGenerations,
  getProfileSettings,
  retryGeneration,
  submitAdvancedGeneration,
  submitSimpleGeneration,
} from "@/lib/actions";
import type { GenerationSchema } from "@/lib/types";

vi.mock("@/lib/runtime-config", () => ({
  isDemoMode: true,
  hasEngineConfig: () => false,
  hasServerSupabaseConfig: () => false,
  hasStripeConfig: () => false,
  getEngineServiceKey: () => "",
}));

vi.mock("@/lib/supabase-server", () => ({ getServerUser: vi.fn() }));
vi.mock("@/lib/supabase", () => ({ createServerClient: vi.fn() }));

const VALID_SCHEMA: GenerationSchema = {
  entities: [{ name: "Widget", fields: [{ name: "id", type: "UUID", pk: true }] }],
  relationships: [],
  endpoints: [],
};

describe("actions.ts — demo mode short circuits", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
    vi.mocked(getServerUser).mockReset();
    vi.mocked(createServerClient).mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("submitSimpleGeneration returns a demo generation id/redirect and never calls fetch or Supabase", async () => {
    const result = await submitSimpleGeneration("Build me a task tracker app please", 0);

    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.generationId).toMatch(/^demo-simple-/);
      expect(result.redirectUrl).toContain("demo=1");
      expect(result.redirectUrl).toContain("tier=0");
    }
    expect(fetchMock).not.toHaveBeenCalled();
    expect(getServerUser).not.toHaveBeenCalled();
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("submitSimpleGeneration still rejects a too-short prompt in demo mode", async () => {
    const result = await submitSimpleGeneration("hi", 0);
    expect(result).toEqual({
      success: false,
      error: "Please provide a more detailed description (at least 10 characters).",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("submitAdvancedGeneration returns a demo generation id/redirect and never calls fetch or Supabase", async () => {
    const result = await submitAdvancedGeneration(VALID_SCHEMA, 2, "DotNetNextJs");

    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.generationId).toMatch(/^demo-advanced-/);
      expect(result.redirectUrl).toContain("demo=1");
      expect(result.redirectUrl).toContain("tier=2");
    }
    expect(fetchMock).not.toHaveBeenCalled();
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("submitAdvancedGeneration rejects an empty entity list before even checking demo mode", async () => {
    const result = await submitAdvancedGeneration(
      { entities: [], relationships: [], endpoints: [] },
      1
    );
    expect(result).toEqual({
      success: false,
      error: "Please define at least one entity before proceeding.",
    });
  });

  it("submitAdvancedGeneration rejects blank entity names in demo mode too (validation runs before the short-circuit)", async () => {
    // Input validation runs before the demo short-circuit, so a malformed schema
    // is rejected identically in demo mode and in a live generation.
    const result = await submitAdvancedGeneration(
      { entities: [{ name: "   ", fields: [] }], relationships: [], endpoints: [] },
      1
    );
    expect(result).toEqual({ success: false, error: "All entities must have a name." });
  });

  it("createPendingGeneration returns a demo id without touching Supabase", async () => {
    const result = await createPendingGeneration("simple", 1, "a prompt");
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.generationId).toMatch(/^demo-pending-/);
    }
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("retryGeneration short-circuits to success without contacting Supabase or the engine", async () => {
    const result = await retryGeneration("demo-simple-123");
    expect(result).toEqual({ success: true });
    expect(createServerClient).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("createCheckoutSession still rejects tier 0 even in demo mode", async () => {
    const result = await createCheckoutSession("gen-1", 0);
    expect(result).toEqual({
      success: false,
      error: "Tier 0 (Spark) is free — no checkout required.",
    });
  });

  it("createCheckoutSession returns a demo redirect URL for paid tiers", async () => {
    const result = await createCheckoutSession("gen-1", 2);
    expect(result).toEqual({
      success: true,
      sessionUrl: "/generate/gen-1?demo=1&tier=2",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("extractSchema returns the canned demo schema without calling the engine", async () => {
    const result = await extractSchema("demo-simple-1", "a prompt");
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.schema.entities.length).toBeGreaterThan(0);
    }
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("getFreeQuotaStatus returns a full untouched quota", async () => {
    const status = await getFreeQuotaStatus();
    expect(status.used).toBe(0);
    expect(status.remaining).toBe(status.limit);
    expect(createServerClient).not.toHaveBeenCalled();
  });

  it("getProfileSettings returns an anonymous fallback when there's no user and no config", async () => {
    const settings = await getProfileSettings();
    expect(settings).toEqual({
      email: "",
      hasApiKeyOverride: false,
      preferredModel: "claude-sonnet-4-6",
    });
  });

  it("getMyGenerations returns an empty page when there's no authenticated user", async () => {
    const result = await getMyGenerations();
    expect(result).toEqual({ generations: [], total: 0 });
  });
});
