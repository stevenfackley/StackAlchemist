const _autoDemo =
  !process.env.NEXT_PUBLIC_DEMO_MODE &&
  !process.env.NEXT_PUBLIC_SUPABASE_URL &&
  process.env.NODE_ENV !== "production";

if (_autoDemo && typeof window === "undefined") {
  console.warn(
    "[runtime-config] Demo mode auto-enabled: NEXT_PUBLIC_SUPABASE_URL is not set. " +
    "Set it, add NEXT_PUBLIC_DEMO_MODE=true to silence this warning, or set NEXT_PUBLIC_DEMO_MODE=false when running against Qavren Auth (QAVREN_AUTH_URL)."
  );
}

export const isDemoMode =
  process.env.NEXT_PUBLIC_DEMO_MODE === "true" || _autoDemo;

// Real Supabase anon keys are either a legacy JWT ("eyJ…") or a modern
// publishable key ("sb_publishable_…"). A stub secret like "placeholder_value"
// is truthy but not a real key — accepting it builds a browser client that
// can't authenticate Realtime or pass RLS, which fails *silently* in prod.
function isLikelyValidAnonKey(key: string | undefined): key is string {
  return !!key && (key.startsWith("eyJ") || key.startsWith("sb_publishable_"));
}

let _warnedInvalidAnonKey = false;

export function hasPublicSupabaseConfig() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const key = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if (!url) return false;
  if (!isLikelyValidAnonKey(key)) {
    // URL is set (Supabase is clearly intended) but the anon key is malformed —
    // a real misconfiguration, not demo mode. Make it loud so a bad deploy
    // surfaces in build/server logs instead of shipping a dead client.
    if (key && !_warnedInvalidAnonKey) {
      _warnedInvalidAnonKey = true;
      console.error(
        "[runtime-config] NEXT_PUBLIC_SUPABASE_URL is set but NEXT_PUBLIC_SUPABASE_ANON_KEY " +
        "is not a valid Supabase key (expected 'eyJ…' JWT or 'sb_publishable_…'). " +
        "Supabase client disabled — auth, Realtime, and RLS reads will not work. " +
        "Fix the environment secret SUPABASE_ANON_KEY and redeploy."
      );
    }
    return false;
  }
  return true;
}

export function hasServerSupabaseConfig() {
  return Boolean(
    process.env.NEXT_PUBLIC_SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY
  );
}

/** Server data goes to qavren-db (Postgres) rather than Supabase. */
export function usesPostgresStore() {
  return Boolean(process.env.DATABASE_URL?.trim());
}

/** A server-side store is reachable: qavren-db (DATABASE_URL) or the Supabase service role pair. */
export function hasDataStoreConfig() {
  return usesPostgresStore() || hasServerSupabaseConfig();
}

export const QAVREN_AUTH_URL_DEFAULT = "https://auth.stackalchemist.app";
export const QAVREN_REALM_DEFAULT = "stackalchemist";

/**
 * Sign-in goes through the Qavren Auth realm (Keycloak, Auth.js) instead of
 * Supabase Auth. Server-only: the mode never reaches a client bundle, so every
 * page that branches on it must be dynamic (see docs/runbooks/qavren-auth.md).
 */
export function usesQavrenAuth() {
  return Boolean(process.env.QAVREN_AUTH_URL?.trim());
}

/** Auth server base URL without a trailing slash (falls back to prod's hostname). */
export function getQavrenAuthUrl() {
  return (process.env.QAVREN_AUTH_URL?.trim() || QAVREN_AUTH_URL_DEFAULT).replace(/\/+$/, "");
}

export function getQavrenRealm() {
  return process.env.QAVREN_REALM?.trim() || QAVREN_REALM_DEFAULT;
}

/**
 * Qavren Auth identities are Keycloak `sub`s. On Supabase, `profiles.id`
 * references `auth.users`, so such an identity could never own a row; refuse
 * the combination at boot rather than on the first insert.
 */
export function assertAuthModeConsistent() {
  if (usesQavrenAuth() && !usesPostgresStore()) {
    throw new Error(
      "QAVREN_AUTH_URL is set but DATABASE_URL is not: Qavren Auth requires the qavren-db store. " +
        "Set both (phase E) or neither (Supabase mode)."
    );
  }
}

export function hasEngineConfig() {
  return Boolean(process.env.ENGINE_API_URL) || process.env.NODE_ENV !== "production";
}

export function hasStripeConfig() {
  return Boolean(process.env.STRIPE_SECRET_KEY);
}

export function getEngineServiceKey() {
  return process.env.ENGINE_SERVICE_KEY ?? "";
}
