import type { NextAuthConfig, Session } from "next-auth";
import { buildAuthConfig } from "@qavren/auth-next";
import { getQavrenAuthUrl, getQavrenRealm } from "@/lib/runtime-config";

// proxy.ts pulls this module in: its import graph must stay free of the DB
// client and of anything else a request-path module has no business loading.
const realm = getQavrenRealm();
const baseUrl = getQavrenAuthUrl();

// buildAuthConfig spreads overrides LAST, so passing `callbacks` would REPLACE
// the SDK's sub/email/roles callbacks. Take the base and compose explicitly.
const base = buildAuthConfig({
  realm,
  baseUrl,
  // The reverse proxy + Cloudflare terminate in front of the app; the Host
  // header is the public one and Auth.js has to be told to believe it.
  trustHost: true,
  pages: { signIn: "/login" },
});

/** Kept apart from auth.ts so it can be imported and unit-tested without NextAuth's runtime. */
export const authConfig: NextAuthConfig = {
  ...base,
  session: { strategy: "jwt", maxAge: 60 * 60 * 24 * 7 },
  callbacks: {
    ...base.callbacks,
    async jwt(params) {
      const token = await base.callbacks!.jwt!(params);
      // null = "destroy this session"; falling back to the incoming token would resurrect it.
      if (token === null) return null;
      // The Keycloak ID token, needed only as id_token_hint on RP-initiated
      // logout. It stays on the encrypted httpOnly JWT cookie and is never
      // copied onto the session object (served verbatim at GET /api/auth/session).
      if (params.account?.id_token) token.idToken = params.account.id_token;
      return token;
    },
    async session(params) {
      const session = (await base.callbacks!.session!(params)) as Session;
      // session.user.id is the Keycloak sub; it becomes user_id on every owned row.
      if (session.user && params.token.sub) session.user.id = params.token.sub;
      return session;
    },
  },
};
