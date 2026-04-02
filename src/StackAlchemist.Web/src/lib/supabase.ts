import { createClient } from "@supabase/supabase-js";
import type { Database } from "./types";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL ?? "";
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "";

/**
 * Browser-side Supabase client (uses anon key).
 * Typed with Database for real-time subscription type safety in Client Components.
 */
export const supabase = createClient<Database>(supabaseUrl, supabaseAnonKey);

/**
 * Server-side Supabase client (service role).
 * Intentionally untyped — Supabase v2 generic inference is too strict for
 * hand-written Database types; use explicit casts in server actions instead.
 * Never expose this client to the browser.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function createServerClient(): ReturnType<typeof createClient<any>> {
  const serviceRoleKey = process.env.SUPABASE_SERVICE_ROLE_KEY ?? "";
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return createClient<any>(supabaseUrl, serviceRoleKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false,
    },
  });
}
