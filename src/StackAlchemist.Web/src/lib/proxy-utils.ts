/** Routes that require an authenticated user (prefix match on whole segments). */
export const PROTECTED_PREFIXES = ["/simple", "/advanced", "/generate"] as const;

/**
 * The generation flow (prompt → build → preview → download) is gated so every
 * creation is tied to an account — a prerequisite for the per-account free-tier
 * quota.
 */
export function isProtectedRoute(pathname: string): boolean {
  return PROTECTED_PREFIXES.some((p) => pathname === p || pathname.startsWith(`${p}/`));
}

/**
 * Constant-time string comparison for the test-mirror Basic-Auth check.
 * Works on edge + node runtimes.
 */
export function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let result = 0;
  for (let i = 0; i < a.length; i++) result |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return result === 0;
}

/**
 * Only same-origin absolute paths survive; anything else becomes `fallback`.
 * `returnTo` is attacker-supplied by definition — an open redirect off a
 * sign-in page is how a credential-phishing chain starts.
 *
 * Consumers use the returned string as-is (`redirect(returnTo)` /
 * `new URL(returnTo, origin)`), never re-derive it from `.pathname`.
 */
export function safeReturnTo(value: unknown, fallback = "/"): string {
  if (
    typeof value !== "string" ||
    !value.startsWith("/") ||
    value.startsWith("//") ||
    value.includes("\\")
  ) {
    return fallback;
  }
  for (let i = 0; i < value.length; i++) {
    const c = value.charCodeAt(i);
    // URL parsers strip \t \n \r, so "/\t/evil" would become "//evil" after the check.
    if (c < 0x20 || c === 0x7f) return fallback;
  }
  return value;
}
