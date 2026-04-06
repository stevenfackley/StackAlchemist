import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

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
