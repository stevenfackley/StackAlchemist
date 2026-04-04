import { createServerClient } from "@supabase/ssr";
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Phase 6 — Auth Callback Route Handler
 *
 * Handles the PKCE code-exchange step for:
 *  - Magic-link sign-ins  (Supabase sends ?code=... after OTP verification)
 *  - Email confirmations  (new accounts click the confirmation email)
 *  - OAuth providers      (future: GitHub, Google)
 *
 * The `emailRedirectTo` option in signInWithOtp() and signUp() must point here,
 * e.g. `${origin}/auth/callback?next=/dashboard`.
 */
export async function GET(request: NextRequest) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get("code");
  const next = searchParams.get("next") ?? "/";

  if (code) {
    const cookieStore = await cookies();

    const supabase = createServerClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
      {
        cookies: {
          getAll() {
            return cookieStore.getAll();
          },
          setAll(cookiesToSet) {
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
      return NextResponse.redirect(`${origin}${next}`);
    }

    console.error("[auth/callback] Code exchange failed:", error.message);
  }

  // No code param or exchange failure — send back to login with an error hint.
  return NextResponse.redirect(`${origin}/login?error=auth_callback_failed`);
}
