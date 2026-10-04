// Demo mode is explicit (NEXT_PUBLIC_DEMO_MODE=true) or local: outside production
// an unset NEXT_PUBLIC_DEMO_MODE means demo. Stays client-visible (a server-only
// variable would make server and client render differently and break hydration).
const _autoDemo =
  !process.env.NEXT_PUBLIC_DEMO_MODE &&
  process.env.NODE_ENV !== "production";

if (_autoDemo && typeof window === "undefined") {
  console.warn(
    "[runtime-config] Demo mode auto-enabled: NEXT_PUBLIC_DEMO_MODE is not set outside production. " +
    "Set NEXT_PUBLIC_DEMO_MODE=true to silence this warning, or NEXT_PUBLIC_DEMO_MODE=false with " +
    "DATABASE_URL and QAVREN_AUTH_URL to run against qavren-db and Qavren Auth."
  );
}

export const isDemoMode =
  process.env.NEXT_PUBLIC_DEMO_MODE === "true" || _autoDemo;

/** Server data goes to qavren-db (Postgres, DATABASE_URL). */
export function usesPostgresStore() {
  return Boolean(process.env.DATABASE_URL?.trim());
}

/** A server-side store is reachable: qavren-db (DATABASE_URL). */
export function hasDataStoreConfig() {
  return usesPostgresStore();
}

export const QAVREN_AUTH_URL_DEFAULT = "https://auth.stackalchemist.app";
export const QAVREN_REALM_DEFAULT = "stackalchemist";

/**
 * Sign-in through the Qavren Auth realm (Keycloak, Auth.js) is configured.
 * Always true in production (assertProductionConfig); false only in demo mode,
 * where nothing loads Auth.js. Server-only: never reaches a client bundle.
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
 * Boot-time configuration check (src/instrumentation.ts). Production has one
 * shape: DATABASE_URL, QAVREN_AUTH_URL (an absolute http(s) URL) and AUTH_SECRET,
 * all three. Outside production every one may be unset (demo mode), but a
 * QAVREN_AUTH_URL that is set must still come with the other two: a Keycloak
 * `sub` can only own rows in the qavren-db store. Messages never echo a value.
 */
export function assertProductionConfig() {
  const production = process.env.NODE_ENV === "production";
  if (production && !usesPostgresStore()) {
    throw new Error("DATABASE_URL is not set: production requires the qavren-db store.");
  }
  if (production && !usesQavrenAuth()) {
    throw new Error("QAVREN_AUTH_URL is not set: production signs in through Qavren Auth.");
  }
  if (usesQavrenAuth() && !usesPostgresStore()) {
    throw new Error(
      "QAVREN_AUTH_URL is set but DATABASE_URL is not: Qavren Auth requires the qavren-db store. " +
        "Set both, or neither for demo mode."
    );
  }
  // A malformed value (e.g. "/" or a bare hostname) must fail loudly here.
  if (usesQavrenAuth() && !/^https?:\/\/[^/\s]+\S*$/i.test(getQavrenAuthUrl())) {
    throw new Error("QAVREN_AUTH_URL must be an absolute http(s) URL (the configured value is not).");
  }
  // Auth.js reads AUTH_SECRET itself; without it the first /api/auth request 500s
  // with MissingSecret. Fail at boot instead.
  if (usesQavrenAuth() && !process.env.AUTH_SECRET?.trim()) {
    throw new Error(
      "AUTH_SECRET is not set: Auth.js needs it to encrypt the session cookie (openssl rand -base64 32). " +
        "Set it with QAVREN_AUTH_URL."
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
