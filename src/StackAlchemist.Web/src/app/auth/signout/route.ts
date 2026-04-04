import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Phase 6 — Sign-Out Route Handler
 *
 * Called via a POST form submission from the navbar's "Sign Out" button.
 * Using a Route Handler (not a Server Action) keeps the sign-out logic server-
 * side and avoids shipping Supabase credentials to the browser bundle.
 */
export async function POST(request: NextRequest) {
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

  await supabase.auth.signOut();

  // Redirect to home after sign-out.
  return NextResponse.redirect(new URL("/", request.url));
}
