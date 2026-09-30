# Qavren Re-platform — Phase C: Auth → Keycloak realm `stackalchemist` (dual-mode)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When `QAVREN_AUTH_URL` is set, StackAlchemist signs users in through the Qavren Auth realm `stackalchemist` (Keycloak, Auth.js v5 via `@qavren/auth-next`) and every server-side identity read yields the Keycloak `sub`; when it is unset, every Supabase Auth path runs exactly as today. The merge is a no-op in prod.

**Architecture:** A second strangler seam next to phase B's `DATABASE_URL`. `usesQavrenAuth()` (server-only, `QAVREN_AUTH_URL` present) selects the Keycloak implementation of four things: the session read (`getSessionUser()`), the route guard (`proxy.ts`, which replaces `middleware.ts`), the auth pages (`/login`, `/register` become one-button Server-Action pages; forgot/reset/callback redirect to `/login`) and sign-out (RP-initiated logout). The Supabase implementations stay in place, byte-for-byte, and are deleted in phase F. Prod flips `DATABASE_URL` and `QAVREN_AUTH_URL` together in phase E.

**Tech Stack:** Next 16.3 (`proxy.ts`, Server Actions), `next-auth@5.0.0-beta.32`, `@qavren/auth-next@^0.1.2` (public npm), Keycloak 26 realm `stackalchemist` (prod) / `stackalchemist-dev` (localhost:3000), Drizzle (`ensureProfile`), vitest + RTL.

Parent plan: `2026-09-28-qavren-replatform.md` (phase C section). Phase B record: `2026-09-28-qavren-replatform-B-data.md`. Reference implementation: recharacter `web/src/{auth.config.ts,auth.ts,proxy.ts,lib/session.ts,app/auth/signout/route.ts,app/(auth)/login,app/(auth)/signup}` and their tests — copy their shape, not their copy.

---

## Decisions specific to this phase

1. **Dual-mode, not a hard swap (deviates from the parent plan's wording).** On Supabase, `profiles.id references auth.users(id)` (`supabase/migrations/20260404000001_create_profiles.sql`) and `handle_new_user` creates the row on signup. A Keycloak `sub` can never own a row there, so "Keycloak auth + Supabase data" is not a runnable combination. Phase C therefore ships behind `QAVREN_AUTH_URL`, merges as a no-op, and phase E flips it together with `DATABASE_URL`. The parent plan's "delete the login/register/forgot/reset/callback pages and `oauth-buttons.tsx`", "remove `@supabase/ssr` and `@supabase/supabase-js`" and the Supabase CSP removals move to **phase F**. `@supabase/supabase-js` is also still the Supabase data path (`SupabaseStore`) until then.
2. **Consistency guard.** `assertAuthModeConsistent()` throws at server start when `QAVREN_AUTH_URL` is set and `DATABASE_URL` is not. Called from `src/instrumentation.ts` `register()` (Node runtime only).
3. **Server-only flag.** `QAVREN_AUTH_URL` is not `NEXT_PUBLIC_*`: the mode never reaches a client bundle, so no Docker build arg, no rebuild to flip. Consequence: every page that branches on the mode must be `export const dynamic = "force-dynamic"`, otherwise `next build` (which has no runtime secrets) prerenders it in Supabase mode forever. Consequence two: `_autoDemo` still keys off `NEXT_PUBLIC_SUPABASE_URL`, so **local Keycloak-mode dev sets `NEXT_PUBLIC_DEMO_MODE=false` explicitly** (documented in the runbook and `.env.example`). Adding a server-only var to `_autoDemo` would compute differently on server and client and break hydration.
4. **Env contract (Qavren mode):** `QAVREN_AUTH_URL` (prod `https://auth.stackalchemist.app`, dev `http://localhost:8090`), `QAVREN_REALM` (default `stackalchemist`; dev `stackalchemist-dev`), `AUTH_SECRET` (≥ 32 random bytes, base64; Auth.js reads it itself), `NEXT_PUBLIC_APP_URL` (already exists; the app origin for the post-logout redirect and the sign-out same-origin check). `trustHost: true` because the reverse proxy + Cloudflare terminate in front of the app. Public PKCE client `stackalchemist-web` (no client secret; phase A registered `https://stackalchemist.app/api/auth/callback/keycloak`, the dev realm `http://localhost:3000/api/auth/callback/keycloak`).
5. **`returnTo` stays the parameter name** (dashboard, proxy and the Supabase pages already use it). Qavren mode sanitises it with `safeReturnTo()` (same-origin absolute path or `/`). The Supabase-mode client's unsanitised `window.location.assign(returnTo)` is pre-existing; file it, do not touch it (byte-for-byte).
6. **Identity shape.** `getSessionUser(): Promise<{ id: string; email: string | null } | null>`. Callers today use only `user.id` and `user.email ?? …`, so the Supabase `User` collapses to that shape with no call-site semantics change. In Qavren mode a non-UUID `id` is treated as no session (it becomes `user_id` on every owned row).
7. **Profiles on qavren-db.** No `auth.users` trigger exists there, and `generations.user_id` / `transactions.user_id` reference `profiles.id`. `DataStore.ensureProfile({ id, email })` (insert-if-missing, never overwrites) runs before the three generation-creating actions **whenever `usesPostgresStore()`** — that also fixes the phase B gap where the `Test` environment (Postgres store + Supabase auth) would hit the FK on first insert. Supabase-store mode never calls it (`handle_new_user` already made the row).
8. **Lazy `@/auth`.** `next-auth` is imported only inside the Qavren branches (`await import("@/auth")`) so Supabase-mode processes and unit tests never load it, and `/api/auth/*` returns 404 in Supabase mode instead of a `MissingSecret` 500.
9. **`middleware.ts` → `proxy.ts` now.** Next 16 forbids both files, and the guard is being restructured anyway. The Supabase branch is moved verbatim; the matcher gains only `api/auth` (Auth.js routes must never be gated).
10. **Register = `prompt=create`.** Keycloak ≥ 26.1 opens the registration form for the OIDC `prompt=create` hint (the realm has `registrationAllowed: true`); Google lives on the Keycloak screen. Password reset lives there too, so `/forgot-password` and `/auth/reset-password` redirect to `/login` in Qavren mode.
11. **Auth error copy is a closed set** (`auth-errors.ts`): only known Auth.js codes and our own map to text; arbitrary `?error=` strings render nothing (a free-text echo on stackalchemist.app is a phishing surface).
12. **Realtime is untouched.** `useGenerationRealtime` already falls back to polling when the channel fails or the browser client is null; in Qavren mode there is no Supabase session so the channel never delivers and polling carries the page. Phase F removes it. The `isDemoMode || !supabase` hard-nav guards in `SimpleModePage`/`AdvancedModePage` become "always hard-nav" in phase F.
13. **Engine and Worker: no change** (the web calls the Engine with `X-Engine-Key`; the Engine never validated user tokens).

## Coupling this phase removes (from `origin/main`, 2026-09-30)

- `src/lib/supabase-server.ts#getServerUser` — the only identity read; 16 call sites (`actions.ts` ×15, `dashboard/page.tsx`).
- `src/middleware.ts` — Basic Auth for the test mirror + Supabase session refresh + `/simple|/advanced|/generate` gate.
- `src/app/{login,register,forgot-password}/…`, `src/app/auth/{callback,reset-password,signout}/…`, `src/components/oauth-buttons.tsx` — Supabase Auth UI and flows (stay; branch).
- `next.config.ts` CSP `form-action` — must allow the Keycloak host for the post-Server-Action redirect.
- `src/app/privacy/page.tsx` — names Supabase as the auth processor.

## File map

| File | Responsibility |
|---|---|
| `src/lib/runtime-config.ts` | + `usesQavrenAuth()`, `getQavrenAuthUrl()`, `getQavrenRealm()`, `assertAuthModeConsistent()` |
| `src/instrumentation.ts` | `register()` calls `assertAuthModeConsistent()` (create if absent) |
| `src/lib/auth-errors.ts` | closed error-code → copy map |
| `src/lib/proxy-utils.ts` | `isProtectedRoute`, `timingSafeEqual`, `safeReturnTo` (pure, tested directly) |
| `src/auth.config.ts`, `src/auth.ts` | Auth.js config composed over `@qavren/auth-next`; NextAuth instance |
| `src/app/api/auth/[...nextauth]/route.ts` | Auth.js handlers, 404 in Supabase mode |
| `src/lib/session.ts` | `getSessionUser()` dual-mode |
| `src/lib/data/{store,drizzle-store,supabase-store}.ts` | + `ensureProfile` |
| `src/lib/actions.ts`, `src/app/dashboard/page.tsx` | use `getSessionUser`; `ensureProfileRow` before inserts |
| `src/proxy.ts` (replaces `src/middleware.ts`) | Basic Auth → mode branch |
| `src/app/login/{page.tsx,QavrenLoginPage.tsx,actions.ts}`, `src/app/register/{page.tsx,QavrenRegisterPage.tsx,actions.ts}` | dual-mode pages + Server Actions |
| `src/app/forgot-password/page.tsx`, `src/app/auth/reset-password/page.tsx`, `src/app/auth/callback/route.ts` | redirect to `/login` in Qavren mode |
| `src/app/auth/signout/route.ts` | dual-mode; RP-initiated logout |
| `next.config.ts`, `src/app/privacy/page.tsx` | CSP `form-action`; processor copy branch |
| `.github/actions/setup-env/action.yml`, `docker-compose.prod.yml`, `.github/workflows/deploy-prod.yml`, `.env.example` | inert plumbing |
| `docs/runbooks/qavren-auth.md` | mode flag, env, local recipe, phase E flip order, troubleshooting |
| Tests | `__tests__/lib/runtime-config.test.ts` (+cases), `__tests__/lib/auth-errors.test.ts`, `__tests__/proxy/{proxy-utils,proxy}.test.ts` (replace `__tests__/middleware/middleware-utils.test.ts`), `__tests__/auth/{auth-config,nextauth-route,session,login-actions,signout-route,qavren-pages}.test.ts(x)`, `__tests__/data/drizzle-store.integration.test.ts` (+ensureProfile), `__tests__/data/supabase-store.test.ts` (+ensureProfile) |

## Environment for every task below

- Worktree: `C:\Users\steve\projects\_wt\StackAlchemist-qavren-auth`, branch `feat/qavren-auth-strangler` off `origin/main` (create with `git -C C:\Users\steve\projects\StackAlchemist worktree add ..\_wt\StackAlchemist-qavren-auth -b feat/qavren-auth-strangler origin/main`).
- Web dir `<worktree>\src\StackAlchemist.Web`. Run `npm ci` once there (public registry; `.npmrc` has `legacy-peer-deps=true`). vitest must run with cwd = the web dir (PowerShell `Push-Location`); `npm --prefix … exec vitest` cannot resolve the `@/` alias.
- Local Postgres for the integration test: container `sa-pg`, `TEST_DATABASE_URL=postgres://postgres:postgres@127.0.0.1:55440/stackalchemist_test` (migrated).
- Never `cd` in the Bash tool. LF everywhere. Conventional Commits, subject + body, **no trailers of any kind**. Do not push until Task 9.
- Byte-for-byte rule for the Supabase path: the Supabase-mode tests under `__tests__/auth/*.test.tsx`, `__tests__/lib/actions-*.test.ts` and `__tests__/dashboard/*` must pass with no assertion edits.

---

### Task 1: Runtime flags, error copy, proxy utilities

**Files:**
- Modify: `src/StackAlchemist.Web/src/lib/runtime-config.ts`
- Create: `src/StackAlchemist.Web/src/instrumentation.ts` (or modify if it exists)
- Create: `src/StackAlchemist.Web/src/lib/auth-errors.ts`, `src/StackAlchemist.Web/src/lib/proxy-utils.ts`
- Modify: `src/StackAlchemist.Web/.env.example`
- Test: `__tests__/lib/runtime-config.test.ts`, create `__tests__/lib/auth-errors.test.ts`, create `__tests__/proxy/proxy-utils.test.ts`, delete `__tests__/middleware/middleware-utils.test.ts`

- [ ] **Step 1: Failing tests for the flags**

Append to `__tests__/lib/runtime-config.test.ts` (extend the import list with `usesQavrenAuth, getQavrenAuthUrl, getQavrenRealm, assertAuthModeConsistent`):

```ts
  it('usesQavrenAuth follows QAVREN_AUTH_URL presence and ignores whitespace', () => {
    vi.stubEnv('QAVREN_AUTH_URL', '');
    expect(usesQavrenAuth()).toBe(false);
    vi.stubEnv('QAVREN_AUTH_URL', '   ');
    expect(usesQavrenAuth()).toBe(false);
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    expect(usesQavrenAuth()).toBe(true);
  });

  it('getQavrenAuthUrl strips a trailing slash and getQavrenRealm defaults to stackalchemist', () => {
    vi.stubEnv('QAVREN_AUTH_URL', 'https://auth.stackalchemist.app/');
    vi.stubEnv('QAVREN_REALM', '');
    expect(getQavrenAuthUrl()).toBe('https://auth.stackalchemist.app');
    expect(getQavrenRealm()).toBe('stackalchemist');
    vi.stubEnv('QAVREN_REALM', 'stackalchemist-dev');
    expect(getQavrenRealm()).toBe('stackalchemist-dev');
  });

  it('assertAuthModeConsistent refuses Qavren Auth without the qavren-db store', () => {
    vi.stubEnv('QAVREN_AUTH_URL', 'http://localhost:8090');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertAuthModeConsistent()).toThrow(/DATABASE_URL/);
    vi.stubEnv('DATABASE_URL', 'postgres://u:p@localhost:5432/db');
    expect(() => assertAuthModeConsistent()).not.toThrow();
    vi.stubEnv('QAVREN_AUTH_URL', '');
    vi.stubEnv('DATABASE_URL', '');
    expect(() => assertAuthModeConsistent()).not.toThrow();
  });
```

- [ ] **Step 2: Run → FAIL** (`usesQavrenAuth is not a function`)

- [ ] **Step 3: Implement** — append to `runtime-config.ts` after `hasDataStoreConfig()`:

```ts
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
```

Also widen the `_autoDemo` warning text (log-only): `"Set it, add NEXT_PUBLIC_DEMO_MODE=true to silence this warning, or set NEXT_PUBLIC_DEMO_MODE=false when running against Qavren Auth (QAVREN_AUTH_URL)."`

- [ ] **Step 4: `instrumentation.ts`** — if `src/instrumentation.ts` exists, add the call inside its `register()`; otherwise create:

```ts
import { assertAuthModeConsistent } from "@/lib/runtime-config";

/** Runs once per server start (Next instrumentation hook). Fail fast on an impossible mode. */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") assertAuthModeConsistent();
}
```

- [ ] **Step 5: `auth-errors.ts` + test**

```ts
/**
 * The closed set of auth failures the app will name in a URL. Auth.js redirects
 * to `pages.signIn` with `?error=<code>`; only these codes render copy, and
 * only this copy — never the parameter itself (free text echoed onto
 * stackalchemist.app is a phishing surface).
 */
export const AUTH_ERRORS = {
  Configuration: "Sign-in is temporarily unavailable. Please try again in a few minutes.",
  AccessDenied: "Sign-in was refused for this account.",
  OAuthSignin: "Sign-in did not complete. Please try again.",
  OAuthCallbackError: "Sign-in did not complete. Please try again.",
  Callback: "Sign-in did not complete. Please try again.",
  Default: "Sign-in did not complete. Please try again.",
  session_expired: "Your session expired. Sign in again to continue.",
} as const;

export type AuthErrorCode = keyof typeof AUTH_ERRORS;

/** Copy for a known code, or `null` — never the caller's own string. */
export function authErrorMessage(code: unknown): string | null {
  return typeof code === "string" && Object.hasOwn(AUTH_ERRORS, code)
    ? AUTH_ERRORS[code as AuthErrorCode]
    : null;
}
```

`__tests__/lib/auth-errors.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { AUTH_ERRORS, authErrorMessage } from "@/lib/auth-errors";

describe("authErrorMessage", () => {
  it("maps known Auth.js and app codes to their own copy", () => {
    expect(authErrorMessage("Configuration")).toBe(AUTH_ERRORS.Configuration);
    expect(authErrorMessage("OAuthCallbackError")).toBe(AUTH_ERRORS.OAuthCallbackError);
    expect(authErrorMessage("session_expired")).toBe(AUTH_ERRORS.session_expired);
  });
  it("renders nothing for unknown codes, inherited keys and non-strings", () => {
    for (const bad of ["Your bank needs your password", "", "constructor", "__proto__", "toString"]) {
      expect(authErrorMessage(bad)).toBeNull();
    }
    expect(authErrorMessage(undefined)).toBeNull();
    expect(authErrorMessage(["Default"])).toBeNull();
  });
});
```

- [ ] **Step 6: `proxy-utils.ts`** — move the two private functions out of `middleware.ts` verbatim and add `safeReturnTo`:

```ts
/** Routes that require an authenticated user (prefix match on whole segments). */
export const PROTECTED_PREFIXES = ["/simple", "/advanced", "/generate"] as const;

export function isProtectedRoute(pathname: string): boolean {
  return PROTECTED_PREFIXES.some((p) => pathname === p || pathname.startsWith(`${p}/`));
}

/** Constant-time string comparison for the test-mirror Basic-Auth check. */
export function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let result = 0;
  for (let i = 0; i < a.length; i++) result |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return result === 0;
}

/**
 * Only same-origin absolute paths survive; anything else becomes `fallback`.
 * `returnTo` is attacker-supplied by definition — an open redirect off a
 * sign-in page is how a credential-phishing chain starts.
 */
export function safeReturnTo(value: unknown, fallback = "/"): string {
  return typeof value === "string" &&
    value.startsWith("/") &&
    !value.startsWith("//") &&
    !value.includes("\\")
    ? value
    : fallback;
}
```

`__tests__/proxy/proxy-utils.test.ts`: port the existing `isProtectedRoute` / `timingSafeEqual` cases from `__tests__/middleware/middleware-utils.test.ts` to import the real functions from `@/lib/proxy-utils` (delete the mirrors and the old file), and add:

```ts
describe("safeReturnTo", () => {
  it("keeps same-origin absolute paths", () => {
    expect(safeReturnTo("/dashboard")).toBe("/dashboard");
    expect(safeReturnTo("/generate/abc?tier=0")).toBe("/generate/abc?tier=0");
  });
  it("rejects everything that could leave the origin", () => {
    for (const bad of ["//evil.example", "https://evil.example", "/\\evil.example", "dashboard", "", null, undefined, 42]) {
      expect(safeReturnTo(bad)).toBe("/");
    }
    expect(safeReturnTo("//evil", "/dashboard")).toBe("/dashboard");
  });
});
```

- [ ] **Step 7: `.env.example`** — add a block:

```
# ── Qavren Auth (phase C; unset = Supabase Auth) ───────────────────────────
# Setting QAVREN_AUTH_URL switches sign-in to the Keycloak realm. Requires DATABASE_URL.
# Local: the qavren-auth compose Keycloak on :8090 with realm stackalchemist-dev,
# and NEXT_PUBLIC_DEMO_MODE=false (demo mode auto-enables when NEXT_PUBLIC_SUPABASE_URL is unset).
# QAVREN_AUTH_URL=http://localhost:8090
# QAVREN_REALM=stackalchemist-dev
# AUTH_SECRET=            # openssl rand -base64 32
```

- [ ] **Step 8: Run** `npx vitest run __tests__/lib __tests__/proxy` → PASS; `npx tsc --noEmit` clean (middleware.ts still imports its own private copies until Task 4 — leave it).

- [ ] **Step 9: Commit** `feat(web): Qavren Auth mode flags, closed auth-error copy, proxy utilities`

### Task 2: Auth.js over `@qavren/auth-next`

**Files:**
- Modify: `src/StackAlchemist.Web/package.json` (+ lockfile)
- Create: `src/StackAlchemist.Web/src/auth.config.ts`, `src/StackAlchemist.Web/src/auth.ts`, `src/StackAlchemist.Web/src/app/api/auth/[...nextauth]/route.ts`
- Test: create `__tests__/auth/auth-config.test.ts`, `__tests__/auth/nextauth-route.test.ts`

- [ ] **Step 1: Dependencies** — from the web dir: `npm install next-auth@5.0.0-beta.32 @qavren/auth-next@^0.1.2` (public registry). Confirm `package-lock.json` resolves both, `npm audit --omit=dev` adds nothing HIGH, and `npm ls next-auth` shows one copy.

- [ ] **Step 2: Failing config test** `__tests__/auth/auth-config.test.ts` (mirrors recharacter's):

```ts
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

type SessionParams = Parameters<NonNullable<NonNullable<typeof authConfig.callbacks>["session"]>>[0];
const sessionParams = (session: Partial<Session>, token: Partial<JWT>) =>
  ({ session, token, newSession: undefined }) as unknown as SessionParams;
type JwtParams = Parameters<NonNullable<NonNullable<typeof authConfig.callbacks>["jwt"]>>[0];
const refreshParams = (token: Partial<JWT>) => ({ token }) as unknown as JwtParams;
const accessToken = (claims: unknown) => {
  const b64url = (o: unknown) => Buffer.from(JSON.stringify(o)).toString("base64url");
  return `${b64url({ alg: "RS256" })}.${b64url(claims)}.sig`;
};

test("the SDK wires the realm as a public PKCE client", () => {
  const provider = buildAuthConfig({ realm: "stackalchemist" }).providers[0] as {
    id: string; options: { clientId: string; clientSecret?: string; client: Record<string, unknown> };
  };
  expect(provider.id).toBe("keycloak");
  expect(provider.options.clientId).toBe("stackalchemist-web");
  expect(provider.options.clientSecret).toBeUndefined();
  expect(provider.options.client).toMatchObject({ token_endpoint_auth_method: "none" });
});

test("our config points at the stackalchemist realm with a week-long jwt session", () => {
  expect(authConfig.providers[0]).toMatchObject({
    id: "keycloak",
    options: { clientId: "stackalchemist-web", issuer: "https://auth.stackalchemist.app/realms/stackalchemist" },
  });
  expect(authConfig.session).toEqual({ strategy: "jwt", maxAge: 60 * 60 * 24 * 7 });
  expect(authConfig.trustHost).toBe(true);
  expect(authConfig.pages).toEqual({ signIn: "/login" });
});

test("jwt keeps the sdk behaviour and stashes the id token", async () => {
  const token = await authConfig.callbacks!.jwt!({
    token: {} as JWT,
    user: { id: SUB },
    account: { provider: "keycloak", providerAccountId: SUB, type: "oidc", id_token: "idt",
      access_token: accessToken({ realm_access: { roles: ["user"] } }) },
    profile: { sub: SUB, email: "a@example.test" },
  });
  expect(token!.sub).toBe(SUB);
  expect(token!.email).toBe("a@example.test");
  expect(token!.roles).toEqual(["user"]);
  expect(token!.idToken).toBe("idt");
});

test("a refresh call with no account keeps what the token carries", async () => {
  const token = await authConfig.callbacks!.jwt!(refreshParams({ sub: SUB, roles: ["user"], idToken: "idt" }));
  expect(token!.sub).toBe(SUB);
  expect(token!.roles).toEqual(["user"]);
  expect(token!.idToken).toBe("idt");
});

test("session.user.id is the keycloak sub and the id token never reaches the session", async () => {
  const session = (await authConfig.callbacks!.session!(
    sessionParams({ user: { email: "a@example.test", roles: [] }, expires: "" }, { sub: SUB, roles: ["user"], idToken: "idt" }),
  )) as Session;
  expect(session.user!.id).toBe(SUB);
  expect(JSON.stringify(session)).not.toContain("idt");
});
```

- [ ] **Step 3: Run → FAIL** (module not found)

- [ ] **Step 4: `src/auth.config.ts`**

```ts
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
```

`src/auth.ts`:

```ts
import NextAuth from "next-auth";
import { authConfig } from "./auth.config";

// Imported lazily (await import("@/auth")) by every Qavren-mode branch so a
// Supabase-mode process never loads next-auth.
export const { handlers, auth, signIn, signOut } = NextAuth(authConfig);
export { authConfig };
```

- [ ] **Step 5: Route handler test** `__tests__/auth/nextauth-route.test.ts`:

```ts
// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const GET_SPY = vi.fn(async () => new Response("authjs", { status: 200 }));
vi.mock("@/auth", () => ({ handlers: { GET: GET_SPY, POST: GET_SPY } }));

describe("/api/auth/[...nextauth]", () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); GET_SPY.mockClear(); });

  it("404s in Supabase mode without touching Auth.js", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "");
    const { GET } = await import("@/app/api/auth/[...nextauth]/route");
    const res = await GET(new NextRequest("http://localhost:3000/api/auth/session"));
    expect(res.status).toBe(404);
    expect(GET_SPY).not.toHaveBeenCalled();
  });

  it("delegates to Auth.js in Qavren mode", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    const { GET } = await import("@/app/api/auth/[...nextauth]/route");
    const res = await GET(new NextRequest("http://localhost:3000/api/auth/session"));
    expect(res.status).toBe(200);
    expect(GET_SPY).toHaveBeenCalledOnce();
  });
});
```

`src/app/api/auth/[...nextauth]/route.ts`:

```ts
import { NextResponse, type NextRequest } from "next/server";
import { usesQavrenAuth } from "@/lib/runtime-config";

// Auth.js exists only in Qavren Auth mode. In Supabase mode the module is never
// loaded, so a stray request cannot surface a MissingSecret 500.
export async function GET(req: NextRequest) {
  if (!usesQavrenAuth()) return new NextResponse(null, { status: 404 });
  const { handlers } = await import("@/auth");
  return handlers.GET(req);
}

export async function POST(req: NextRequest) {
  if (!usesQavrenAuth()) return new NextResponse(null, { status: 404 });
  const { handlers } = await import("@/auth");
  return handlers.POST(req);
}
```

- [ ] **Step 6: Run** `npx vitest run __tests__/auth/auth-config.test.ts __tests__/auth/nextauth-route.test.ts` → PASS. `npx tsc --noEmit` clean (if `token.idToken` needs a type, augment `next-auth/jwt` in `src/types/next-auth.d.ts` with `interface JWT { idToken?: string }` — only if `tsc` demands it).

- [ ] **Step 7: Commit** `feat(web): Auth.js config over @qavren/auth-next for the stackalchemist realm`

### Task 3: `getSessionUser()` seam, `ensureProfile`, call sites

**Files:**
- Create: `src/StackAlchemist.Web/src/lib/session.ts`
- Modify: `src/lib/data/store.ts`, `src/lib/data/drizzle-store.ts`, `src/lib/data/supabase-store.ts`, `src/lib/actions.ts`, `src/app/dashboard/page.tsx`
- Test: create `__tests__/auth/session.test.ts`; extend `__tests__/data/drizzle-store.integration.test.ts`, `__tests__/data/supabase-store.test.ts`

- [ ] **Step 1: Failing session tests** `__tests__/auth/session.test.ts`:

```ts
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
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: `src/lib/session.ts`**

```ts
import { getServerUser } from "./supabase-server";
import { usesQavrenAuth } from "./runtime-config";

export type SessionUser = { id: string; email: string | null };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * The one place session identity is read.
 * Qavren Auth mode: the Keycloak `sub` — it becomes `user_id` on every owned
 * row, so anything that is not a UUID is treated as no session at all.
 * Supabase mode: today's `getServerUser()`, unchanged, collapsed to the same shape.
 */
export async function getSessionUser(): Promise<SessionUser | null> {
  if (!usesQavrenAuth()) {
    const user = await getServerUser();
    return user ? { id: user.id, email: user.email ?? null } : null;
  }
  // Lazy: keeps next-auth out of Supabase-mode processes and unit tests.
  const { auth } = await import("@/auth");
  const session = await auth();
  const id = session?.user?.id;
  if (!id || !UUID.test(id)) return null;
  return { id, email: session!.user!.email ?? null };
}
```

- [ ] **Step 4: Switch the call sites** — `actions.ts`: replace `import { getServerUser } from "./supabase-server";` with `import { getSessionUser, type SessionUser } from "./session";` and every `await getServerUser()` with `await getSessionUser()`. `dashboard/page.tsx`: same import swap and call. No other line changes (the `user.id` / `user.email ?? …` reads already fit). Run `npx vitest run __tests__/lib __tests__/dashboard` → the existing `actions-*.test.ts` still mock `@/lib/supabase-server` and must pass untouched (session.ts delegates to that mocked module in Supabase mode).

- [ ] **Step 5: `ensureProfile`** — failing integration test first. In `__tests__/data/drizzle-store.integration.test.ts` add (inside the existing describe, using its store/db helpers):

```ts
  it("ensureProfile inserts a first-seen user once and never overwrites settings", async () => {
    const id = crypto.randomUUID();
    await store.ensureProfile({ id, email: "first@example.test" });
    await store.ensureProfile({ id, email: "second@example.test" });
    expect(await store.getProfile(id)).toMatchObject({ email: "first@example.test" });
    await store.upsertProfile({ id, email: "first@example.test", preferred_model: "claude-opus-5-5" });
    await store.ensureProfile({ id, email: "third@example.test" });
    expect(await store.getProfile(id)).toMatchObject({ email: "first@example.test", preferred_model: "claude-opus-5-5" });
  });
```

Interface (`store.ts`, after `upsertProfile`):

```ts
  /**
   * Insert the profile row for a first-seen user; a no-op when it exists (settings
   * live there and must not be overwritten). qavren-db has no auth.users trigger,
   * and generations.user_id references profiles.id.
   */
  ensureProfile(profile: { id: string; email: string }): Promise<void>;
```

`drizzle-store.ts` (follow the file's existing `db`/`driverError`/`DataStoreError` conventions; supply every NOT NULL column without a default exactly as `upsertProfile` does):

```ts
  async ensureProfile({ id, email }: { id: string; email: string }): Promise<void> {
    try {
      await this.db.insert(profiles).values({ id, email }).onConflictDoNothing({ target: profiles.id });
    } catch (err) {
      throw new DataStoreError("Failed to create profile", driverError(err));
    }
  }
```

`supabase-store.ts` (same client/error conventions as `upsertProfile`):

```ts
  async ensureProfile({ id, email }: { id: string; email: string }): Promise<void> {
    const { error } = await this.client
      .from("profiles")
      .upsert({ id, email }, { onConflict: "id", ignoreDuplicates: true });
    if (error) throw new DataStoreError("Failed to create profile", error);
  }
```

Add a `supabase-store.test.ts` case asserting the chain `from("profiles").upsert({id,email}, { onConflict: "id", ignoreDuplicates: true })` is issued and a returned error becomes `DataStoreError`.

- [ ] **Step 6: Call sites in `actions.ts`** — add once, near the other helpers:

```ts
/**
 * qavren-db has no auth.users trigger creating profiles, and the generations FK
 * needs the row first. Supabase-store mode is untouched: handle_new_user made it.
 */
async function ensureProfileRow(user: SessionUser): Promise<void> {
  if (!usesPostgresStore()) return;
  await getDataStore().ensureProfile({ id: user.id, email: user.email ?? "" });
}
```

(import `usesPostgresStore` from `./runtime-config`). Call `await ensureProfileRow(user);` immediately after the `user` null-check in `submitSimpleGeneration`, `submitAdvancedGeneration` and `createPendingGeneration`, before their insert. Add one unit test per action in the existing `actions-generation.test.ts` style: with `DATABASE_URL` stubbed, the store's `ensureProfile` is called before `insertGeneration`; with it unset, `ensureProfile` is never called (construction-only additions; no existing assertion changes).

- [ ] **Step 7: Run** `npx vitest run __tests__/auth/session.test.ts __tests__/lib __tests__/dashboard __tests__/data` (with `TEST_DATABASE_URL`) → PASS; `npx tsc --noEmit`.

- [ ] **Step 8: Commit** `feat(web): getSessionUser seam (Qavren sub or Supabase user) and ensureProfile before first insert`

### Task 4: `proxy.ts` replaces `middleware.ts`

**Files:**
- Create: `src/StackAlchemist.Web/src/proxy.ts`; Delete: `src/StackAlchemist.Web/src/middleware.ts`
- Test: create `__tests__/proxy/proxy.test.ts`

- [ ] **Step 1: Failing tests** `__tests__/proxy/proxy.test.ts`:

```ts
// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

// Auth.js's `auth(handler)` wraps the handler with session resolution; unwrap
// so the guard itself is under test. The session is supplied per case as req.auth.
const authMock = vi.fn((handler: unknown) => handler);
vi.mock("@/auth", () => ({ auth: authMock }));

type Authed = NextRequest & { auth?: { user?: { id?: string } } | null };
const SUB = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
const req = (url: string, session: { user?: { id?: string } } | null | undefined = undefined) => {
  const r = new NextRequest(url) as Authed;
  if (session !== undefined) r.auth = session;
  return r;
};
const run = async (r: NextRequest) => {
  const { proxy } = await import("@/proxy");
  return (proxy as unknown as (r: NextRequest, e: unknown) => Promise<Response>)(r, {});
};

describe("proxy", () => {
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); authMock.mockClear(); });

  it("Supabase mode with no Supabase env passes through and never loads @/auth", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", ""); vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", "");
    const res = await run(req("http://localhost:3000/generate/abc"));
    expect(res.status).toBe(200);
    expect(authMock).not.toHaveBeenCalled();
  });

  it("Qavren mode: an anonymous protected request redirects to /login with returnTo", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false");
    const res = await run(req("http://localhost:3000/generate/abc?tier=0", null));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toBe("http://localhost:3000/login?returnTo=%2Fgenerate%2Fabc%3Ftier%3D0");
  });

  it("Qavren mode: a signed-in request and every public route pass through", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false");
    expect((await run(req("http://localhost:3000/simple", { user: { id: SUB } }))).status).toBe(200);
    for (const p of ["/", "/pricing", "/login", "/register", "/dashboard", "/simplex"]) {
      expect((await run(req(`http://localhost:3000${p}`, null))).status, p).toBe(200);
    }
  });

  it("Qavren mode: a session without a user id is not a session", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("NEXT_PUBLIC_DEMO_MODE", "false");
    expect((await run(req("http://localhost:3000/advanced", { user: {} }))).status).toBe(307);
  });

  it("test-mirror Basic Auth runs before either mode", async () => {
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090");
    vi.stubEnv("NEXT_PUBLIC_IS_TEST_SITE", "true"); vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("TEST_SITE_BASIC_AUTH_USER", "u"); vi.stubEnv("TEST_SITE_BASIC_AUTH_PASS", "p");
    const denied = await run(req("http://localhost:3000/", null));
    expect(denied.status).toBe(401);
    expect(denied.headers.get("www-authenticate")).toContain("Basic");
    const ok = new NextRequest("http://localhost:3000/", { headers: { authorization: `Basic ${Buffer.from("u:p").toString("base64")}` } }) as Authed;
    ok.auth = null;
    expect((await run(ok)).status).toBe(200);
    expect((await run(req("http://localhost:3000/api/healthz", null))).status).toBe(200);
  });

  it("the matcher skips Auth.js routes and static assets but still catches guarded paths", async () => {
    const { config } = await import("@/proxy");
    const m = new RegExp(`^${config.matcher[0]}$`);
    for (const skipped of ["/api/auth/callback/keycloak", "/api/auth/session", "/_next/static/c.js", "/favicon.ico", "/logo.svg"]) {
      expect(m.test(skipped), skipped).toBe(false);
    }
    for (const kept of ["/generate/abc", "/simple", "/dashboard", "/api/healthz"]) expect(m.test(kept), kept).toBe(true);
  });
});
```

- [ ] **Step 2: Run → FAIL** (`@/proxy` missing)

- [ ] **Step 3: `src/proxy.ts`** — move `basicAuthChallenge` and `checkTestSiteBasicAuth` from `middleware.ts` verbatim (they now import `timingSafeEqual` from `./lib/proxy-utils`), move the whole Supabase body (everything after the Basic-Auth check) verbatim into `supabaseSessionRefresh(request)` (it imports `isProtectedRoute` from `./lib/proxy-utils`), then:

```ts
import { NextResponse, type NextFetchEvent, type NextRequest } from "next/server";
import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { isDemoMode, usesQavrenAuth } from "./lib/runtime-config";
import { isProtectedRoute, timingSafeEqual } from "./lib/proxy-utils";

// … basicAuthChallenge, checkTestSiteBasicAuth, supabaseSessionRefresh: moved verbatim …

type Gate = (req: NextRequest, event: NextFetchEvent) => Promise<Response> | Response;
let qavrenGate: Gate | null = null;

/**
 * Built on first use so a Supabase-mode process never loads next-auth. `auth()`
 * resolves the session onto `req.auth`. This is a redirect convenience, not the
 * authorization boundary: every action and page reads getSessionUser() itself.
 */
async function getQavrenGate(): Promise<Gate> {
  if (qavrenGate) return qavrenGate;
  const { auth } = await import("@/auth");
  qavrenGate = auth((req) => {
    const { pathname, search } = req.nextUrl;
    if (isDemoMode || !isProtectedRoute(pathname) || req.auth?.user?.id) return NextResponse.next();
    const login = new URL("/login", req.nextUrl.origin);
    login.searchParams.set("returnTo", pathname + search);
    return NextResponse.redirect(login);
  }) as unknown as Gate;
  return qavrenGate;
}

/** Next 16 proxy (replaces middleware.ts). Basic Auth first, then the auth mode. */
export async function proxy(request: NextRequest, event: NextFetchEvent) {
  const challenge = checkTestSiteBasicAuth(request);
  if (challenge) return challenge;
  if (usesQavrenAuth()) return (await getQavrenGate())(request, event);
  return supabaseSessionRefresh(request);
}

export const config = {
  // Today's matcher plus Auth.js's own routes, which must never be gated.
  matcher: ["/((?!_next/static|_next/image|favicon\\.ico|api/auth|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)"],
};
```

Delete `src/middleware.ts` (Next 16 refuses a build with both files).

- [ ] **Step 4: Run** `npx vitest run __tests__/proxy` → PASS; `npx tsc --noEmit`; `npm run build` locally once (Supabase mode, no env) → must build and print the `proxy` route in the output, no "middleware" deprecation line.

- [ ] **Step 5: Commit** `feat(web): proxy.ts with a Qavren Auth gate; Supabase session refresh moved verbatim`

### Task 5: Pages and sign-out

**Files:**
- Modify: `src/app/login/page.tsx`, `src/app/register/page.tsx`, `src/app/forgot-password/page.tsx`, `src/app/auth/reset-password/page.tsx`, `src/app/auth/callback/route.ts`, `src/app/auth/signout/route.ts`
- Create: `src/app/login/QavrenLoginPage.tsx`, `src/app/login/actions.ts`, `src/app/register/QavrenRegisterPage.tsx`, `src/app/register/actions.ts`
- Test: create `__tests__/auth/login-actions.test.ts`, `__tests__/auth/signout-route.test.ts`, `__tests__/auth/qavren-pages.test.tsx`

- [ ] **Step 1: Failing action tests** `__tests__/auth/login-actions.test.ts`:

```ts
// @vitest-environment node
import { beforeEach, describe, expect, it, vi } from "vitest";
const signIn = vi.fn();
vi.mock("@/auth", () => ({ signIn }));
const form = (returnTo: unknown) => { const f = new FormData(); if (returnTo !== undefined) f.set("returnTo", String(returnTo)); return f; };

describe("Qavren sign-in actions", () => {
  beforeEach(() => signIn.mockReset());
  it("loginAction hands off to keycloak with a sanitised returnTo", async () => {
    const { loginAction } = await import("@/app/login/actions");
    await loginAction(form("/dashboard"));
    expect(signIn).toHaveBeenCalledWith("keycloak", { redirectTo: "/dashboard" });
    await loginAction(form("https://evil.example"));
    expect(signIn).toHaveBeenLastCalledWith("keycloak", { redirectTo: "/" });
  });
  it("signupAction adds the OIDC prompt=create hint", async () => {
    const { signupAction } = await import("@/app/register/actions");
    await signupAction(form("/simple"));
    expect(signIn).toHaveBeenCalledWith("keycloak", { redirectTo: "/simple" }, { prompt: "create" });
  });
});
```

`src/app/login/actions.ts`:

```ts
"use server";
import { safeReturnTo } from "@/lib/proxy-utils";

/** Hands off to the realm's sign-in screen; Auth.js completes the PKCE dance. */
export async function loginAction(formData: FormData) {
  const { signIn } = await import("@/auth");
  await signIn("keycloak", { redirectTo: safeReturnTo(formData.get("returnTo")) });
}
```

`src/app/register/actions.ts`:

```ts
"use server";
import { safeReturnTo } from "@/lib/proxy-utils";

/**
 * Same authorization request as sign-in with the OIDC `prompt=create` hint —
 * Keycloak >= 26.1 opens the registration form instead of the login form.
 */
export async function signupAction(formData: FormData) {
  const { signIn } = await import("@/auth");
  await signIn("keycloak", { redirectTo: safeReturnTo(formData.get("returnTo")) }, { prompt: "create" });
}
```

- [ ] **Step 2: Qavren pages** — `src/app/login/QavrenLoginPage.tsx` (server component; reuse the header markup and classes from `ForgotPasswordClient` so the page matches the existing auth screens):

```tsx
import Link from "next/link";
import { Alert, Button } from "@/components/ui";
import { Logo } from "@/components/logo";
import { authErrorMessage } from "@/lib/auth-errors";
import { safeReturnTo } from "@/lib/proxy-utils";
import { getQavrenAuthUrl } from "@/lib/runtime-config";
import { loginAction } from "./actions";

export function QavrenLoginPage({ params }: { params: { error?: string; returnTo?: string } }) {
  const message = authErrorMessage(params.error);
  const returnTo = safeReturnTo(params.returnTo);
  const authHost = new URL(getQavrenAuthUrl()).host;
  const registerHref = `/register${returnTo !== "/" ? `?returnTo=${encodeURIComponent(returnTo)}` : ""}`;
  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-header">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4"><Logo variant="mono" size={28} /></div>
      </header>
      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-md space-y-6">
          <h1 className="font-mono text-xs text-slate-400 uppercase tracking-widest">Sign in</h1>
          {message && <Alert variant="error" data-testid="login-auth-error">{message}</Alert>}
          <p className="text-sm text-slate-400">
            Sign-in happens on the StackAlchemist sign-in service at <strong>{authHost}</strong>. The address bar
            changes to that name while you enter your password or continue with Google, then brings you back here.
          </p>
          <form action={loginAction}>
            <input type="hidden" name="returnTo" value={returnTo} />
            <Button type="submit" className="w-full">Sign in</Button>
          </form>
          <p className="text-xs text-slate-500">Forgot your password? Reset it on the sign-in screen.</p>
          <p className="text-xs text-slate-500">New here? <Link href={registerHref} className="text-accent">Create an account</Link></p>
        </div>
      </main>
    </div>
  );
}
```

`src/app/register/QavrenRegisterPage.tsx`: identical shape with title "Create your account", copy "Accounts are created on the StackAlchemist sign-in service at …", `signupAction`, button "Create your account", link "I already have an account" → `/login` (carrying `returnTo` the same way).

Page wrappers — `src/app/login/page.tsx`:

```tsx
import type { Metadata } from "next";
import LoginPageClient from "./LoginPageClient";
import { QavrenLoginPage } from "./QavrenLoginPage";
import { usesQavrenAuth } from "@/lib/runtime-config";

export const metadata: Metadata = { /* unchanged */ };
// The auth mode is a runtime secret; a prerendered page would freeze it at build time.
export const dynamic = "force-dynamic";

export default async function LoginPage(props: { searchParams: Promise<{ error?: string; returnTo?: string }> }) {
  if (!usesQavrenAuth()) return <LoginPageClient />;
  return <QavrenLoginPage params={await props.searchParams} />;
}
```

`register/page.tsx`: same pattern with `RegisterPageClient` / `QavrenRegisterPage`. `forgot-password/page.tsx` and `auth/reset-password/page.tsx` (this one is a client component today — wrap it: rename the existing default export to `ResetPasswordClient` in the same file or move it to `ResetPasswordClient.tsx`, then the page does):

```tsx
export const dynamic = "force-dynamic";
export default function Page() {
  if (usesQavrenAuth()) redirect("/login"); // Keycloak owns password reset
  return <ForgotPasswordClient />;           // or <ResetPasswordClient />
}
```

`auth/callback/route.ts`: first statement in `GET`, before reading `code`:

```ts
  // Supabase PKCE only. In Qavren mode the callback is /api/auth/callback/keycloak.
  if (usesQavrenAuth()) return NextResponse.redirect(`${publicOrigin}/login`);
```

(compute `publicOrigin` before it, as today.)

- [ ] **Step 3: Sign-out** — failing test `__tests__/auth/signout-route.test.ts`:

```ts
// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
const getToken = vi.fn(); const signOut = vi.fn();
vi.mock("next-auth/jwt", () => ({ getToken }));
vi.mock("@/auth", () => ({ signOut }));
const post = (headers: Record<string, string> = {}) => new NextRequest("http://localhost:3000/auth/signout", { method: "POST", headers });
const LOGOUT = "http://localhost:8090/realms/stackalchemist-dev/protocol/openid-connect/logout";

describe("POST /auth/signout (Qavren mode)", () => {
  beforeEach(() => {
    getToken.mockReset(); signOut.mockReset();
    getToken.mockResolvedValue({ sub: "u", idToken: "idt" });
    vi.stubEnv("QAVREN_AUTH_URL", "http://localhost:8090"); vi.stubEnv("QAVREN_REALM", "stackalchemist-dev");
    vi.stubEnv("NEXT_PUBLIC_APP_URL", "http://localhost:3000");
  });
  afterEach(() => { vi.unstubAllEnvs(); vi.resetModules(); });

  it("refuses a cross-origin or cross-site post without signing out", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    expect((await POST(post({ origin: "https://evil.example" }))).status).toBe(403);
    expect((await POST(post({ "sec-fetch-site": "cross-site" }))).status).toBe(403);
    expect(signOut).not.toHaveBeenCalled();
  });

  it("ends the keycloak session with the id token hint and returns home", async () => {
    const { POST } = await import("@/app/auth/signout/route");
    const res = await POST(post({ origin: "http://localhost:3000" }));
    expect(signOut).toHaveBeenCalledWith({ redirect: false });
    expect(res.status).toBe(303);
    const end = new URL(res.headers.get("location")!);
    expect(`${end.origin}${end.pathname}`).toBe(LOGOUT);
    expect(end.searchParams.get("id_token_hint")).toBe("idt");
    expect(end.searchParams.get("post_logout_redirect_uri")).toBe("http://localhost:3000/");
  });

  it("falls back to client_id when no id token is on the jwt", async () => {
    getToken.mockResolvedValue({ sub: "u" });
    const { POST } = await import("@/app/auth/signout/route");
    const end = new URL((await POST(post({ "sec-fetch-site": "same-origin" }))).headers.get("location")!);
    expect(end.searchParams.get("client_id")).toBe("stackalchemist-dev-web");
    expect(end.searchParams.has("id_token_hint")).toBe(false);
  });
});
```

`src/app/auth/signout/route.ts` — keep the existing `POST` body verbatim under the Supabase branch:

```ts
export async function POST(request: NextRequest) {
  if (usesQavrenAuth()) return qavrenSignOut(request);
  // … today's Supabase body, unchanged …
}

/**
 * Sign out of StackAlchemist AND of the realm. Dropping our cookie alone leaves
 * Keycloak's SSO cookie alive, so the next "sign in" on a shared machine would
 * hand the account straight back. RP-initiated logout ends the realm session.
 */
async function qavrenSignOut(request: NextRequest) {
  const appBaseUrl = (process.env.NEXT_PUBLIC_APP_URL?.trim() || new URL(request.url).origin).replace(/\/+$/, "");
  const appOrigin = new URL(appBaseUrl).origin;
  // POST-only plus two cross-site checks: a cross-site form must not log someone out.
  const origin = request.headers.get("origin");
  const site = request.headers.get("sec-fetch-site");
  if ((origin && origin !== appOrigin) || (site && site !== "same-origin" && site !== "none")) {
    return NextResponse.json({ error: "forbidden" }, { status: 403 });
  }
  const [{ getToken }, { signOut }, { issuerFor }] = await Promise.all([
    import("next-auth/jwt"), import("@/auth"), import("@qavren/auth-next"),
  ]);
  // The ID token lives only on the JWT cookie (never on the session object);
  // read it before signOut clears the cookie.
  const jwt = await getToken({ req: request, secret: process.env.AUTH_SECRET ?? "", secureCookie: appBaseUrl.startsWith("https:") });
  const idToken = typeof jwt?.idToken === "string" ? jwt.idToken : undefined;
  await signOut({ redirect: false });
  const realm = getQavrenRealm();
  const end = new URL(`${issuerFor(realm, getQavrenAuthUrl())}/protocol/openid-connect/logout`);
  end.searchParams.set("post_logout_redirect_uri", `${appBaseUrl}/`);
  if (idToken) end.searchParams.set("id_token_hint", idToken);
  else end.searchParams.set("client_id", `${realm}-web`); // Keycloak needs one of the two to honour the redirect
  return NextResponse.redirect(end, 303);
}
```

- [ ] **Step 4: Page tests** `__tests__/auth/qavren-pages.test.tsx`: with `QAVREN_AUTH_URL` stubbed and `@/auth` mocked, `render(await LoginPage({ searchParams: Promise.resolve({ error: "Configuration", returnTo: "//evil" }) }))` shows the Configuration copy, a hidden `returnTo` input with value `/`, a "Sign in" submit button and the register link; an unknown `error` renders no alert; with `QAVREN_AUTH_URL` empty, `LoginPage` renders the Supabase `LoginPageClient` (assert the existing email input is present). Same two-mode check for `RegisterPage`. `forgot-password` page in Qavren mode calls `redirect("/login")` (mock `next/navigation`).

- [ ] **Step 5: Run** `npx vitest run __tests__/auth` → PASS (the five existing Supabase-mode files included, untouched); `npx tsc --noEmit`; e2e smoke is demo mode and unaffected — run `npx playwright test e2e/smoke/dashboard.spec.ts` if the browsers are installed.

- [ ] **Step 6: Commit** `feat(web): Qavren-mode sign-in, registration and RP-initiated sign-out; Supabase pages branch untouched`

### Task 6: CSP and privacy copy

**Files:** `next.config.ts`, `src/app/privacy/page.tsx`; test: create `__tests__/privacy-page.test.tsx`

- [ ] **Step 1:** `next.config.ts` CSP: `form-action 'self' https://stackblitz.com https://auth.stackalchemist.app` (the Server Action redirects the browser to the realm; `form-action` is checked on redirects). Leave `connect-src`'s Supabase entries for phase F.
- [ ] **Step 2:** `privacy/page.tsx`: `export const dynamic = "force-dynamic";` and branch the three Supabase-Auth sentences on `usesQavrenAuth()`: "Auth is handled by **Qavren Auth**, a Keycloak sign-in service operated by Qavren Solutions LLC."; subprocessor list gains "**Qavren Auth (Keycloak)** — authentication" and Supabase's line reads "— database" only; the cookies sentence names "a session cookie set by our sign-in service" instead of Supabase tokens. Supabase-mode text unchanged.
- [ ] **Step 3:** Test renders the page in both modes and asserts the processor sentence for each.
- [ ] **Step 4: Commit** `feat(web): CSP form-action for the auth host; privacy copy names Qavren Auth in Keycloak mode`

### Task 7: Deploy plumbing (inert) + runbook

**Files:** `.github/actions/setup-env/action.yml`, `docker-compose.prod.yml`, `.github/workflows/deploy-prod.yml`, create `docs/runbooks/qavren-auth.md`

- [ ] **Step 1: `setup-env`** — inputs `qavren_auth_url`, `qavren_realm`, `auth_secret` (all `required: false`, `default: ''`; descriptions say "Empty = Supabase Auth"); `env:` entries; heredoc lines under a `# Qavren Auth (phase C). Empty keeps Supabase Auth; set with DATABASE_URL in phase E.` comment: `QAVREN_AUTH_URL=${QAVREN_AUTH_URL}`, `QAVREN_REALM=${QAVREN_REALM}`, `AUTH_SECRET=${AUTH_SECRET}` (8-space indent like their neighbours).
- [ ] **Step 2: compose** `sa-web.environment`: `QAVREN_AUTH_URL: ${QAVREN_AUTH_URL:-}`, `QAVREN_REALM: ${QAVREN_REALM:-stackalchemist}`, `AUTH_SECRET: ${AUTH_SECRET:-}`.
- [ ] **Step 3: `deploy-prod.yml`** `Setup .env (prod)` `with:`: `qavren_auth_url: ${{ secrets.QAVREN_AUTH_URL }}`, `qavren_realm: ${{ vars.QAVREN_REALM || '' }}`, `auth_secret: ${{ secrets.AUTH_SECRET }}`. Nothing else: the web reads them at runtime (no build arg, no rebuild to flip).
- [ ] **Step 4: Runbook** `docs/runbooks/qavren-auth.md`: the flag and the consistency rule; the env contract (decision 4) and how to mint `AUTH_SECRET`; realm facts (client `stackalchemist-web`, callback `/api/auth/callback/keycloak`, post-logout `/`); the local recipe (qavren-auth compose Keycloak on `:8090`, realm `stackalchemist-dev` applied with the platform's realm apply script, `.env.local` with `NEXT_PUBLIC_DEMO_MODE=false`, `DATABASE_URL` on `sa-pg`); phase E flip order (set `DATABASE_URL` + `DATABASE_URL_MIGRATE` + `QAVREN_AUTH_URL` + `AUTH_SECRET` in one deploy; Google works only after phase A task 6); troubleshooting (`?error=Configuration` = `AUTH_SECRET` missing or issuer unreachable; "client not found" = realm not applied; a redirect loop = `trustHost`/`NEXT_PUBLIC_APP_URL` mismatch); what phase F deletes.
- [ ] **Step 5:** `actionlint` (with the two `-ignore` label flags used in phase B) unchanged; PyYAML parse; commit `ci(deploy): Qavren Auth env pass-through (inert) and the qavren-auth runbook`.

### Task 8: Smoke against a real realm (Toby, not a subagent)

- [ ] If the qavren-auth local Keycloak (`:8090`) is running and the `stackalchemist-dev` realm is applied: `.env.local` per the runbook, `npm run dev`, then with the Playwright MCP: `/dashboard` → redirected to `/login?returnTo=%2Fdashboard` → "Sign in" → Keycloak login screen (dark skin) → register a throwaway user → back on `/dashboard` showing the email → one Tier-0 generation reaches the preview (proves `ensureProfile` + FK) → Sign Out → `/` and a second "Sign in" asks for the password again (realm session ended). Record results here.
- [ ] Otherwise record that the smoke moves to phase E's cutover checklist against `auth.stackalchemist.app` (owner tasks 5–7 of phase A must be done first) and say so in the PR.

### Task 9: PR, review, merge, verify the deploy was a no-op

- [ ] PR title `feat: dual-mode auth over Qavren Auth / Keycloak (re-platform phase C)`; body: decisions 1–13, the deviation from the parent plan, the smoke result, the Supabase-path byte-for-byte claim with the exact list of what changed on that path (expected: only the `middleware.ts → proxy.ts` move, the `api/auth` matcher exclusion, and `force-dynamic` on five pages), follow-ups (Supabase-mode unsanitised `returnTo`; phase F deletions list).
- [ ] Opus review blocking classes: any changed assertion in `__tests__/auth/*.test.tsx`, `__tests__/lib/actions-*.test.ts`, `__tests__/dashboard/*`; any `@/auth` or `next-auth` import that is not inside a `usesQavrenAuth()` branch (or the two Auth.js files themselves); any page branching on the mode without `force-dynamic`; `returnTo` reaching `signIn`/`redirect` unsanitised in Qavren mode; the ID token reaching the session object.
- [ ] Merge under the standing StackAlchemist authorization; `deploy-prod` green; `/api/healthz` 200; `/login` still renders the Supabase client (email input present); `/api/auth/session` → 404.
- [ ] Vault `Projects/stack-alchemist/stack-alchemist.md` phase C section; StackAlchemist memory note; this file's dated results.

## Exit criteria

- [ ] With `QAVREN_AUTH_URL` set (and `DATABASE_URL`), sign-in, registration and sign-out run through the realm and every server identity read is the Keycloak `sub`; with it unset, every Supabase Auth path is unchanged and the five Supabase-mode auth test files pass untouched.
- [ ] `assertAuthModeConsistent` refuses Keycloak auth without the qavren-db store at boot.
- [ ] `deploy-prod` after merge: green, prod unchanged (Supabase mode; `/api/auth/*` 404).
- [ ] Smoke against a real realm recorded (local dev realm now, or scheduled for phase E).

Phase D (CI: a live-Keycloak e2e lane and the Postgres-mode e2e) can start once the first line is true.
