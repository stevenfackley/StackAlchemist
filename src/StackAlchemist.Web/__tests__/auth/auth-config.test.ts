// @vitest-environment node
import { beforeAll, expect, test, vi } from "vitest";
import { buildAuthConfig } from "@qavren/auth-next";
import type { Session } from "next-auth";
import type { JWT } from "next-auth/jwt";

const SUB = "11111111-2222-4333-8444-555555555555";
let authConfig: typeof import("@/auth.config").authConfig;

beforeAll(async () => {
  vi.stubEnv("QAVREN_AUTH_URL", "https://auth.stackalchemist.app");
  vi.stubEnv("QAVREN_REALM", "");
  vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "true");
  ({ authConfig } = await import("@/auth.config"));
});

// The `session` callback's parameter type is the union of the jwt- and
// database-strategy shapes, so it demands adapter fields we never see under
// `strategy: "jwt"`. Build the jwt-strategy half and cast once, here.
type SessionParams = Parameters<NonNullable<NonNullable<typeof authConfig.callbacks>["session"]>>[0];
const sessionParams = (session: Partial<Session>, token: Partial<JWT>) =>
  ({ session, token, newSession: undefined }) as unknown as SessionParams;

// Likewise `jwt`: the type demands `user`, but Auth.js omits it on every call
// after sign-in, which is exactly the case worth testing.
type JwtParams = Parameters<NonNullable<NonNullable<typeof authConfig.callbacks>["jwt"]>>[0];
const refreshParams = (token: Partial<JWT>) => ({ token }) as unknown as JwtParams;

/** A structurally real access token: the SDK base64url-decodes segment 1. */
const accessToken = (claims: unknown) => {
  const b64url = (o: unknown) => Buffer.from(JSON.stringify(o)).toString("base64url");
  return `${b64url({ alg: "RS256" })}.${b64url(claims)}.sig`;
};

test("the SDK wires the realm as a public PKCE client", () => {
  // Auth.js keeps caller-supplied provider settings under `options`.
  const provider = buildAuthConfig({ realm: "stackalchemist" }).providers[0] as {
    id: string;
    options: { clientId: string; clientSecret?: string; client: Record<string, unknown> };
  };
  expect(provider.id).toBe("keycloak");
  expect(provider.options.clientId).toBe("stackalchemist-web");
  expect(provider.options.clientSecret).toBeUndefined();
  expect(provider.options.client).toMatchObject({ token_endpoint_auth_method: "none" });
});

test("our config points at the stackalchemist realm with a week-long jwt session", () => {
  expect(authConfig.providers[0]).toMatchObject({
    id: "keycloak",
    options: {
      clientId: "stackalchemist-web",
      issuer: "https://auth.stackalchemist.app/realms/stackalchemist",
    },
  });
  expect(authConfig.session).toEqual({ strategy: "jwt", maxAge: 60 * 60 * 24 * 7 });
  expect(authConfig.trustHost).toBe(true);
  expect(authConfig.pages).toEqual({ signIn: "/login" });
});

test("jwt keeps the sdk behaviour and stashes the id token", async () => {
  const token = await authConfig.callbacks!.jwt!({
    token: {} as JWT,
    user: { id: SUB },
    account: {
      provider: "keycloak",
      providerAccountId: SUB,
      type: "oidc",
      id_token: "idt",
      // Realm roles arrive inside the access token, not as a top-level field.
      access_token: accessToken({ realm_access: { roles: ["user"] } }),
    },
    profile: { sub: SUB, email: "a@example.test" },
  });
  expect(token!.sub).toBe(SUB);
  expect(token!.email).toBe("a@example.test");
  expect(token!.roles).toEqual(["user"]);
  expect(token!.idToken).toBe("idt");
});

test("a refresh call with no account keeps what the token carries", async () => {
  // Every request after sign-in calls `jwt` with no account and no profile.
  // Losing roles or the id token there would sign the user out of the realm
  // half-way through a session.
  const token = await authConfig.callbacks!.jwt!(
    refreshParams({ sub: SUB, roles: ["user"], idToken: "idt" }),
  );
  expect(token!.sub).toBe(SUB);
  expect(token!.roles).toEqual(["user"]);
  expect(token!.idToken).toBe("idt");
});

test("a null from the sdk destroys the session instead of resurrecting it", async () => {
  // The shipped SDK always returns a token, so stub one that does not: a null
  // return means "drop this session", and falling back to the incoming token
  // would hand it straight back.
  vi.resetModules();
  vi.doMock("@qavren/auth-next", () => ({
    buildAuthConfig: () => ({
      providers: [],
      callbacks: { jwt: () => null, session: (p: SessionParams) => p.session },
    }),
  }));

  const { authConfig: stubbed } = await import("@/auth.config");
  await expect(stubbed.callbacks!.jwt!(refreshParams({ sub: SUB }))).resolves.toBeNull();

  vi.doUnmock("@qavren/auth-next");
  vi.resetModules();
});

test("session.user.id is the keycloak sub and the id token never reaches the session", async () => {
  // Auth.js serves whatever this callback returns verbatim at
  // GET /api/auth/session, which any script on the page can fetch. An ID token
  // there is a bearer credential handed to the browser.
  const session = (await authConfig.callbacks!.session!(
    sessionParams(
      { user: { email: "a@example.test", roles: [] }, expires: "" },
      { sub: SUB, roles: ["user"], idToken: "idt" },
    ),
  )) as Session;
  expect(session.user!.id).toBe(SUB);
  expect(JSON.stringify(session)).not.toContain("idt");
  expect("idToken" in session).toBe(false);
});
