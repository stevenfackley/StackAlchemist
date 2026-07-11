import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Phase 6 — Auth Callback Route Handler
 *
 * Handles the PKCE code-exchange step for:
 *  - Magic-link sign-ins  (Supabase sends ?code=... after OTP verification)
 *  - Email confirmations  (new accounts click the confirmation email)
 *  - OAuth providers      (Google enabled 2026-07-11; Apple)
 *
 * The `emailRedirectTo` option in signInWithOtp() and signUp() must point here,
 * e.g. `${origin}/auth/callback?next=/dashboard`.
 */
export async function GET(request: NextRequest) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get("code");

  // Behind nginx + Cloudflare Tunnel the request's own origin resolves to the
  // container bind address (http://0.0.0.0:3000) — never a valid browser
  // destination. The public origin is deploy-time config; the request origin
  // is only a fallback for local dev, where the two match.
  const publicOrigin =
    process.env.NEXT_PUBLIC_APP_URL?.replace(/\/+$/, "") || origin;

  // Open-redirect guard: `next` must be an app-internal absolute path.
  const rawNext = searchParams.get("next") ?? "/";
  const next =
    rawNext.startsWith("/") && !rawNext.startsWith("//") ? rawNext : "/";

  const fail = (reason: string) =>
    NextResponse.redirect(
      `${publicOrigin}/login?error=auth_callback_failed&reason=${encodeURIComponent(reason)}`
    );

  if (!code) {
    // Supabase redirects here with error params instead of a code when the
    // provider flow itself failed (e.g. ?error=access_denied&error_code=
    // otp_expired). Surface the most specific one instead of a generic hint.
    const reason =
      searchParams.get("error_code") ??
      searchParams.get("error_description") ??
      searchParams.get("error") ??
      "missing_code";
    return fail(reason);
  }

  const cookieStore = await cookies();

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll() {
          return cookieStore.getAll();
        },
        setAll(cookiesToSet: { name: string; value: string; options: CookieOptions }[]) {
          cookiesToSet.forEach(({ name, value, options }) =>
            cookieStore.set(name, value, options)
          );
        },
      },
    }
  );

  const { error } = await supabase.auth.exchangeCodeForSession(code);

  if (!error) {
    // Successful exchange — redirect to the intended destination.
    return NextResponse.redirect(`${publicOrigin}${next}`);
  }

  // A duplicate hit on this route (browser preload, back/forward retry)
  // consumes the one-time code on the first pass and fails the second with
  // "invalid flow state". If a session already exists the sign-in actually
  // succeeded — proceed instead of bouncing an authed user to an error.
  const {
    data: { user },
  } = await supabase.auth.getUser();
  if (user) {
    return NextResponse.redirect(`${publicOrigin}${next}`);
  }

  console.error("[auth/callback] Code exchange failed:", error.message);
  return fail(error.code ?? error.message);
}
