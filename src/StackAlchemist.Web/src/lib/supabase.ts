import { createClient } from "@supabase/supabase-js";
import type { Database } from "./types";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

/**
 * Browser-side Supabase client (uses anon key).
 * Use this in Client Components for real-time subscriptions, auth, etc.
 */
export const supabase = createClient<Database>(supabaseUrl, supabaseAnonKey);

/**
 * Helper to get the server-side Supabase client (service role).
 * Only use in Server Components / Server Actions — never expose to the browser.
 */
export function createServerClient() {
  const serviceRoleKey = process.env.SUPABASE_SERVICE_ROLE_KEY!;
  return createClient<Database>(supabaseUrl, serviceRoleKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false,
    },
  });
}
