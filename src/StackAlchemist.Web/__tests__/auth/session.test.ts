// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const auth = vi.fn();
const getServerUser = vi.fn();
vi.mock("@/auth", () => ({ auth }));
vi.mock("@/lib/supabase-server", () => ({ getServerUser }));

const SUB = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

describe("getSessionUser", () => {
  beforeEach(() => { auth.mockReset(); getServerUser.mockReset(); });
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); });

  it("Supabase mode collapses the Supabase user to { id, email } and never calls auth()", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    getServerUser.mockResolvedValue({ id: SUB, email: "a@example.test", user_metadata: {} });
    const { getSessionUser } = await import("@/lib/session");
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: "a@example.test" });
    getServerUser.mockResolvedValue({ id: SUB });
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: null });
    getServerUser.mockResolvedValue(null);
    await expect(getSessionUser()).resolves.toBeNull();
    expect(auth).not.toHaveBeenCalled();
    expect(getServerUser).toHaveBeenCalledTimes(3);
  });

  it("Qavren mode returns the keycloak sub and email", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    auth.mockResolvedValue({ user: { id: SUB, email: "a@example.test" } });
    const { getSessionUser } = await import("@/lib/session");
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: "a@example.test" });
    auth.mockResolvedValue({ user: { id: SUB } });
    await expect(getSessionUser()).resolves.toEqual({ id: SUB, email: null });
    expect(getServerUser).not.toHaveBeenCalled();
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
