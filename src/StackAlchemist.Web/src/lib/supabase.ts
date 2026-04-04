import { createBrowserClient } from "@supabase/ssr";
import { createClient } from "@supabase/supabase-js";
import type { Database } from "./types";
import { hasPublicSupabaseConfig, isDemoMode } from "./runtime-config";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

/**
 * Browser-side Supabase client (uses anon key).
 *
 * Phase 6: migrated from `createClient` → `createBrowserClient` (@supabase/ssr)
 * so the client automatically reads/writes the cookie-based session that
 * middleware maintains on the server side.  All Client Components that call
 * `supabase.auth.*` or `supabase.channel(...)` should import this singleton.
 *
 * Returns `null` when public env vars are not configured so the UI fails closed.
 */
export const supabase = hasPublicSupabaseConfig()
  ? createBrowserClient<Database>(supabaseUrl!, supabaseAnonKey!)
  : null;

/**
 * Server-side Supabase client using the **service role** key.
 * Bypasses RLS — never expose this client to the browser.
 *
 * Used only by server actions that need to write on behalf of the system
 * (e.g. creating a generation row triggered by a Stripe webhook) where the
 * anon-key SSR client in supabase-server.ts would be blocked by RLS.
 */
export function createServerClient() {
  const serviceRoleKey = process.env.SUPABASE_SERVICE_ROLE_KEY;

  if (!supabaseUrl || !serviceRoleKey) {
    if (isDemoMode) {
      throw new Error("Supabase is unavailable in demo mode.");
    }

    throw new Error(
      "Supabase server configuration is missing. Set NEXT_PUBLIC_SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY."
    );
  }

  return createClient(supabaseUrl, serviceRoleKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false,
    },
  });
}
