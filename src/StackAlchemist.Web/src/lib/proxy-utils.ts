/** Routes that require an authenticated user (prefix match on whole segments). */
export const PROTECTED_PREFIXES = ["/simple", "/advanced", "/generate"] as const;

export function isProtectedRoute(pathname: string): boolean {
  return PROTECTED_PREFIXES.some((p) => pathname === p || pathname.startsWith(`${p}/`));
}

/** Constant-time string comparison for the test-mirror Basic-Auth check. */
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
 */
export function safeReturnTo(value: unknown, fallback = "/"): string {
  return typeof value === "string" &&
    value.startsWith("/") &&
    !value.startsWith("//") &&
    !value.includes("\\")
    ? value
    : fallback;
}
