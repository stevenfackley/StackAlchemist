// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const auth = vi.fn();
vi.mock("@/auth", () => ({ auth }));

const SUB = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

describe("getSessionUser", () => {
  beforeEach(() => { auth.mockReset(); });
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); });

  it("demo mode (no QAVREN_AUTH_URL) has no user and never calls auth()", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    auth.mockResolvedValue({ user: { id: SUB, email: "a@example.test" } });
    const { getSessionUser } = await import("@/lib/session");
    await expect(getSessionUser()).resolves.toBeNull();
    expect(auth).not.toHaveBeenCalled();
  });

  it("Qavren mode returns the keycloak sub and email", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    auth.mockResolvedValue({ user: { id: SUB, email: "a@example.test" } });
    const { getSessionUser } = await import("@/lib/session");
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: "a@example.test" });
    auth.mockResolvedValue({ user: { id: SUB } });
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: null });
  });

  it("Qavren mode: no session, or an id that is not a UUID, is no user", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    const { getSessionUser } = await import("@/lib/session");
    auth.mockResolvedValue(null);
    await expect(getSessionUser()).resolves.toBeNull();
    for (const id of ["a@example.test", "", "undefined", "../../etc/passwd", SUB.slice(0, -1)]) {
      auth.mockResolvedValue({ user: { id, email: "a@example.test" } });
      await expect(getSessionUser()).resolves.toBeNull();
    }
  });
});
