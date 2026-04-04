export const isDemoMode =
  process.env.NEXT_PUBLIC_DEMO_MODE === "true" ||
  (!process.env.NEXT_PUBLIC_SUPABASE_URL && process.env.NODE_ENV !== "production");

export function hasPublicSupabaseConfig() {
  return Boolean(
    process.env.NEXT_PUBLIC_SUPABASE_URL && process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY
  );
}

export function hasServerSupabaseConfig() {
  return Boolean(
    process.env.NEXT_PUBLIC_SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY
  );
}

export function hasEngineConfig() {
  return Boolean(process.env.ENGINE_API_URL) || process.env.NODE_ENV !== "production";
}