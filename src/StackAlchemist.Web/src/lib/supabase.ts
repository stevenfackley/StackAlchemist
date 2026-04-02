import { createClient } from "@supabase/supabase-js";
import type { Database } from "./types";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

function hasPublicSupabaseConfig() {
  return Boolean(supabaseUrl && supabaseAnonKey);
}

/**
 * Browser-side Supabase client (uses anon key).
 * Typed with Database for real-time subscription type safety in Client Components.
 * Returns `null` when public env vars are not configured so the UI can fail closed.
 */
export const supabase = hasPublicSupabaseConfig()
  ? createClient<Database>(supabaseUrl!, supabaseAnonKey!)
  : null;

/**
 * Server-side Supabase client (service role).
 * Never expose this client to the browser.
 */
export function createServerClient() {
  const serviceRoleKey = process.env.SUPABASE_SERVICE_ROLE_KEY;

  if (!supabaseUrl || !serviceRoleKey) {
    throw new Error(
      "Supabase server configuration is missing. Set NEXT_PUBLIC_SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY."
    );
  }

  // Server actions currently write against a hand-maintained schema shape.
  // Keep runtime env validation strict, but avoid over-constraining writes until
  // generated Supabase types are added to the repo.
  return createClient(supabaseUrl, serviceRoleKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false,
    },
  });
}
