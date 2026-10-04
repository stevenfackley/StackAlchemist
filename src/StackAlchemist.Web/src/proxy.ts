import { NextResponse, type NextFetchEvent, type NextMiddleware, type NextRequest } from "next/server";
import { isDemoMode } from "./lib/runtime-config";
import { isProtectedRoute, timingSafeEqual } from "./lib/proxy-utils";

/**
 * Gate the test mirror behind HTTP Basic Auth. Prevents casual discovery of
 * unreleased work and — combined with the layout's `noindex` directive and
 * the `X-Robots-Tag` response header — stops search engines from indexing it.
 *
 * Env contract:
 *   NEXT_PUBLIC_IS_TEST_SITE=true  — flag that this deployment is the mirror
 *   TEST_SITE_BASIC_AUTH_USER      — username (server-only)
 *   TEST_SITE_BASIC_AUTH_PASS      — password (server-only)
 *
 * If the flag is true but the credentials are not configured, the site stays
 * open (fail-open) and logs a warning — never fail-closed on a missing env,
 * that'd take the whole test mirror down if a secret is misplaced.
 *
 * Health endpoints (/api/healthz) are exempt so Docker healthchecks work.
 */
function basicAuthChallenge(): NextResponse {
  return new NextResponse("Authentication required.", {
    status: 401,
    headers: {
      "WWW-Authenticate": 'Basic realm="StackAlchemist Test"',
      "content-type": "text/plain; charset=utf-8",
    },
  });
}

function checkTestSiteBasicAuth(request: NextRequest): NextResponse | null {
  if (process.env.NEXT_PUBLIC_IS_TEST_SITE !== "true") return null;

  const user = process.env.TEST_SITE_BASIC_AUTH_USER;
  const pass = process.env.TEST_SITE_BASIC_AUTH_PASS;
  if (!user || !pass) {
    if (typeof process !== "undefined" && process.env.NODE_ENV !== "production") {
      return null;
    }
    console.warn("[middleware] test site flagged but TEST_SITE_BASIC_AUTH_USER/PASS missing — running open");
    return null;
  }

  // Exempt Docker/tunnel healthcheck so deployments don't break.
  if (request.nextUrl.pathname.startsWith("/api/healthz")) return null;
  // Exempt CSP violation reports — browsers don't send credentials with them.
  if (request.nextUrl.pathname.startsWith("/api/csp-report")) return null;

  const header = request.headers.get("authorization") ?? "";
  if (!header.toLowerCase().startsWith("basic ")) return basicAuthChallenge();

  const encoded = header.slice(6).trim();
  let decoded: string;
  try {
    decoded = atob(encoded);
  } catch {
    return basicAuthChallenge();
  }
  const separatorIdx = decoded.indexOf(":");
  if (separatorIdx === -1) return basicAuthChallenge();
  const providedUser = decoded.slice(0, separatorIdx);
  const providedPass = decoded.slice(separatorIdx + 1);
  if (!timingSafeEqual(providedUser, user) || !timingSafeEqual(providedPass, pass)) {
    return basicAuthChallenge();
  }
  return null;
}

// The slice of Auth.js's NextAuthRequest the gate reads. Declared here rather than
// imported so nothing outside auth.ts/auth.config.ts names next-auth, even as a type;
// tsc still checks it, because Auth.js's request type must be assignable to it.
type AuthedRequest = NextRequest & { auth: { user?: { id?: string } } | null };
let qavrenGate: NextMiddleware | null = null;

/**
 * Built on first use so a demo-mode process never loads next-auth (see the
 * invariant in src/auth.config.ts). `auth()` resolves the session onto `req.auth`.
 * Gates the creation flow: a signed-out visitor who tries to generate is bounced
 * to /login and returned to where they were (the prompt rides along in the query
 * string) once they sign in. This is a redirect convenience, not the authorization
 * boundary: every action and page reads getSessionUser() itself and enforces
 * ownership there.
 */
async function getQavrenGate(): Promise<NextMiddleware> {
  if (qavrenGate) return qavrenGate;
  const { auth } = await import("@/auth");
  // The (req, event) signature selects Auth.js's middleware overload, which returns
  // a NextMiddleware; no cast needed.
  qavrenGate = auth((req: AuthedRequest, _event: NextFetchEvent) => {
    const { pathname, search } = req.nextUrl;
    // Next runs the proxy when the raw OR the decoded path matches the matcher, but
    // hands over the raw pathname, and serves /%73imple as /simple. Guard the decoded form.
    let path = pathname;
    try {
      path = decodeURIComponent(pathname);
    } catch {
      // Malformed escape (e.g. /%zz): keep the raw path.
    }
    if (!isProtectedRoute(path) || req.auth?.user?.id) return NextResponse.next();
    // Bounce to /login and come back afterwards.
    const login = new URL("/login", req.nextUrl.origin);
    login.searchParams.set("returnTo", pathname + search);
    return NextResponse.redirect(login);
  });
  return qavrenGate;
}

// Non-empty values only: a deletion (`name=; Max-Age=0`) can never resurrect a session and
// must pass through, e.g. Auth.js clearing a cookie that no longer decrypts after a secret rotation.
const SESSION_COOKIE_HEADER = /^(?:__Secure-)?authjs\.session-token(?:\.\d+)?=[^;]/;

/**
 * Auth.js's middleware wrapper re-encodes the JWT and appends a refreshed session
 * cookie to EVERY gated response (sliding expiry). That makes any late response —
 * a Server Action or a router refresh still in flight when the user signs out —
 * resurrect the session the sign-out just deleted. The proxy therefore never
 * re-issues session cookies: sign-in sets the cookie through Auth.js's own route,
 * sign-out deletes it, and the session lives a fixed maxAge (7 days) in between.
 * Other cookies the wrapper may set (callback-url, csrf) pass through untouched.
 */
function stripSessionRefresh<T>(res: T): T {
  if (!(res instanceof Response)) return res;
  const cookies = res.headers.getSetCookie();
  if (!cookies.some((c) => SESSION_COOKIE_HEADER.test(c))) return res;
  res.headers.delete("set-cookie");
  for (const c of cookies) if (!SESSION_COOKIE_HEADER.test(c)) res.headers.append("set-cookie", c);
  return res;
}

/** Next 16 proxy (replaces middleware.ts). Basic Auth first, then the Auth.js gate. */
export async function proxy(request: NextRequest, event: NextFetchEvent) {
  // Run Basic Auth first so unauthenticated traffic never touches Auth.js.
  const challenge = checkTestSiteBasicAuth(request);
  if (challenge) return challenge;
  // Auth.js's own routes are never gated (they create the session the gate reads),
  // but they stay behind the test mirror's Basic Auth above. Exact prefix so a
  // future /api/author… route is not silently exempted. The sign-out route deletes the
  // session cookie; the gate would otherwise append a refreshed one to the same response
  // and the browser's header order decides who wins.
  const { pathname } = request.nextUrl;
  if (pathname === "/api/auth" || pathname.startsWith("/api/auth/") || pathname === "/auth/signout") {
    return NextResponse.next();
  }
  // Demo mode never gates, so it never needs to load Auth.js or decrypt a cookie.
  if (isDemoMode) return NextResponse.next();
  return stripSessionRefresh(await (await getQavrenGate())(request, event));
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     *  - _next/static  (Next.js static assets)
     *  - _next/image   (Next.js image optimization)
     *  - favicon.ico and other public image assets
     */
    "/((?!_next/static|_next/image|favicon\\.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)",
  ],
};
