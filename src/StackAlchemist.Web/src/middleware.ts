import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Constant-time string comparison — mitigates timing attacks on the
 * Basic-Auth credentials check below. Works on edge + node runtimes.
 */
function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let result = 0;
  for (let i = 0; i < a.length; i++) result |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return result === 0;
}

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

/**
 * Phase 6 — Supabase SSR session propagation middleware.
 *
 * On every matched request the middleware:
 *  1. Creates a lightweight Supabase client backed by the request/response
 *     cookie jar.
 *  2. Calls `getUser()` which silently refreshes the JWT if it has expired and
 *     writes the updated session back via Set-Cookie headers.
 *
 * This keeps the server-side session in sync without any extra round-trips in
 * Server Components or Server Actions.
 */
export async function middleware(request: NextRequest) {
  // Run Basic Auth first so unauthenticated traffic never touches Supabase.
  const authChallenge = checkTestSiteBasicAuth(request);
  if (authChallenge) return authChallenge;

  let supabaseResponse = NextResponse.next({ request });

  // Bail out early when Supabase public vars are not configured (demo / CI).
  if (!process.env.NEXT_PUBLIC_SUPABASE_URL || !process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY) {
    return supabaseResponse;
  }

  try {
    const supabase = createServerClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY,
      {
        cookies: {
          getAll() {
            return request.cookies.getAll();
          },
          setAll(cookiesToSet: { name: string; value: string; options: CookieOptions }[]) {
            // Write cookies back onto the request (so Server Actions see them)
            // and onto the response (so the browser receives Set-Cookie headers).
            cookiesToSet.forEach(({ name, value }) =>
              request.cookies.set(name, value)
            );
            supabaseResponse = NextResponse.next({ request });
            cookiesToSet.forEach(({ name, value, options }) =>
              supabaseResponse.cookies.set(name, value, options)
            );
          },
        },
      }
    );

    // Refresh the session — do NOT use getSession() here; getUser() is required
    // to avoid trusting stale cookie data.
    await supabase.auth.getUser();
  } catch (error) {
    console.error("[middleware] Supabase session refresh failed:", error);
    return NextResponse.next({ request });
  }

  return supabaseResponse;
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
