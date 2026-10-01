/**
 * The closed set of auth failures the app will name in a URL. Auth.js redirects
 * to `pages.signIn` with `?error=<code>`; only these codes render copy, and
 * only this copy — never the parameter itself (free text echoed onto
 * stackalchemist.app is a phishing surface).
 */
export const AUTH_ERRORS = {
  Configuration: "Sign-in is temporarily unavailable. Please try again in a few minutes.",
  AccessDenied: "Sign-in was refused for this account.",
  OAuthSignin: "Sign-in did not complete. Please try again.",
  OAuthCallbackError: "Sign-in did not complete. Please try again.",
  Callback: "Sign-in did not complete. Please try again.",
  Default: "Sign-in did not complete. Please try again.",
  session_expired: "Your session expired. Sign in again to continue.",
} as const;

export type AuthErrorCode = keyof typeof AUTH_ERRORS;

/** Copy for a known code, or `null` — never the caller's own string. */
export function authErrorMessage(code: unknown): string | null {
  return typeof code === "string" && Object.hasOwn(AUTH_ERRORS, code)
    ? AUTH_ERRORS[code as AuthErrorCode]
    : null;
}
