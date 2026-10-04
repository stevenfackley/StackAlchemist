"use server";
import { safeReturnTo } from "@/lib/proxy-utils";
import { usesQavrenAuth } from "@/lib/runtime-config";

/** Hands off to the realm's sign-in screen; Auth.js completes the PKCE dance. */
export async function loginAction(formData: FormData) {
  // A server action is a public endpoint. Without QAVREN_AUTH_URL (demo mode, never production:
  // assertProductionConfig requires it there) there is no realm and Auth.js has no secret.
  if (!usesQavrenAuth()) return;
  const { signIn } = await import("@/auth");
  await signIn("keycloak", { redirectTo: safeReturnTo(formData.get("returnTo")) });
}
