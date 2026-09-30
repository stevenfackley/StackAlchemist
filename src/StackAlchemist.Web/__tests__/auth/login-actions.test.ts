// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const signIn = vi.hoisted(() => vi.fn());
vi.mock("@/auth", () => ({ signIn }));

const form = (returnTo: unknown) => {
  const f = new FormData();
  if (returnTo !== undefined) f.set("returnTo", String(returnTo));
  return f;
};

describe("Qavren sign-in actions", () => {
  beforeEach(() => {
    signIn.mockReset();
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
  });
  afterEach(() => vi.unstubAllEnvs());

  it("loginAction hands off to keycloak with a sanitised returnTo", async () => {
    const { loginAction } = await import("@/app/login/actions");
    await loginAction(form("/dashboard"));
    expect(signIn).toHaveBeenCalledWith("keycloak", { redirectTo: "/dashboard" });
    await loginAction(form("https://evil.example"));
    expect(signIn).toHaveBeenLastCalledWith("keycloak", { redirectTo: "/" });
    await loginAction(form(undefined));
    expect(signIn).toHaveBeenLastCalledWith("keycloak", { redirectTo: "/" });
  });

  it("signupAction adds the OIDC prompt=create hint", async () => {
    const { signupAction } = await import("@/app/register/actions");
    await signupAction(form("/simple"));
    expect(signIn).toHaveBeenCalledWith("keycloak", { redirectTo: "/simple" }, { prompt: "create" });
  });

  it("both actions are inert in Supabase mode (a server action is reachable in every mode)", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    const { loginAction } = await import("@/app/login/actions");
    const { signupAction } = await import("@/app/register/actions");
    await loginAction(form("/dashboard"));
    await signupAction(form("/dashboard"));
    expect(signIn).not.toHaveBeenCalled();
  });
});
