const _autoDemo =
  !process.env.NEXT_PUBLIC_DEMO_MODE &&
  !process.env.NEXT_PUBLIC_SUPABASE_URL &&
  process.env.NODE_ENV !== "production";

if (_autoDemo && typeof window === "undefined") {
  console.warn(
    "[runtime-config] Demo mode auto-enabled: NEXT_PUBLIC_SUPABASE_URL is not set. " +
    "Set it or add NEXT_PUBLIC_DEMO_MODE=true to silence this warning."
  );
}

export const isDemoMode =
  process.env.NEXT_PUBLIC_DEMO_MODE === "true" || _autoDemo;

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

export function hasStripeConfig() {
  return Boolean(process.env.STRIPE_SECRET_KEY);
}

export function getEngineServiceKey() {
  return process.env.ENGINE_SERVICE_KEY ?? "";
}
