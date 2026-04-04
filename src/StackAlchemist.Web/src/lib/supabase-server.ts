import { createServerClient, type CookieOptions } from "@supabase/ssr";
import { cookies } from "next/headers";
import type { Database } from "./types";
import { hasPublicSupabaseConfig } from "./runtime-config";

/**
 * Creates a Supabase client suitable for use in Server Components, Server
 * Actions, and Route Handlers.  It reads and writes session cookies via the
 * Next.js `cookies()` API so the user's JWT is automatically refreshed.
 *
 * Phase 6: replaces the hand-rolled `createServerClient()` in supabase.ts for
 * all auth-aware server-side code paths.  The service-role client in
 * supabase.ts is kept separately for engine-triggered writes that must bypass
 * RLS.
 */
export async function createSupabaseServerClient() {
  const cookieStore = await cookies();

  return createServerClient<Database>(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll() {
          return cookieStore.getAll();
        },
        setAll(cookiesToSet: { name: string; value: string; options: CookieOptions }[]) {
          try {
            cookiesToSet.forEach(({ name, value, options }) =>
              cookieStore.set(name, value, options)
            );
          } catch {
            // Called from a Server Component — cookie writes are fine from
            // middleware and Route Handlers; ignore the error here.
          }
        },
      },
    }
  );
}

/**
 * Returns the currently authenticated Supabase user, or null when the visitor
 * is anonymous or Supabase is not configured.
 *
 * Always use `getUser()` (not `getSession()`) on the server — it validates the
 * JWT with the Supabase Auth server instead of trusting the cookie value alone.
 */
export async function getServerUser() {
  if (!hasPublicSupabaseConfig()) return null;

  try {
    const supabase = await createSupabaseServerClient();
    const {
      data: { user },
    } = await supabase.auth.getUser();
    return user ?? null;
  } catch {
    return null;
  }
}
