"use server";
import { safeReturnTo } from "@/lib/proxy-utils";
import { usesQavrenAuth } from "@/lib/runtime-config";

/**
 * Same authorization request as sign-in with the OIDC `prompt=create` hint —
 * Keycloak >= 26.1 opens the registration form instead of the login form.
 */
export async function signupAction(formData: FormData) {
  // A server action is a public endpoint. Without QAVREN_AUTH_URL (demo mode, never production:
  // assertProductionConfig requires it there) there is no realm and Auth.js has no secret.
  if (!usesQavrenAuth()) return;
  const { signIn } = await import("@/auth");
  await signIn("keycloak", { redirectTo: safeReturnTo(formData.get("returnTo")) }, { prompt: "create" });
}
