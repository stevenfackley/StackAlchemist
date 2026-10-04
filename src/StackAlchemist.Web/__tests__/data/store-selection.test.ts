// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/data/drizzle-store", () => ({ DrizzleStore: class {} }));

describe("getDataStore", () => {
  afterEach(() => {
    vi.resetModules();
  });

  it("returns the qavren-db (Drizzle) store", async () => {
    const { getDataStore } = await import("@/lib/data");
    const { DrizzleStore } = await import("@/lib/data/drizzle-store");
    expect(getDataStore()).toBeInstanceOf(DrizzleStore);
  });

  it("builds a store per call, not one at module load", async () => {
    const { getDataStore } = await import("@/lib/data");
    expect(getDataStore()).not.toBe(getDataStore());
  });
});
