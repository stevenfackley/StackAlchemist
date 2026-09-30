import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { usesQavrenAuth } from "@/lib/runtime-config";
import ForgotPasswordClient from "./ForgotPasswordClient";

export const metadata: Metadata = {
  title: "Reset Password — Stack Alchemist",
  robots: { index: false, follow: true },
};

// The auth mode is a runtime secret (QAVREN_AUTH_URL) that `next build` never
// sees; a prerendered page would freeze Supabase mode in at build time.
export const dynamic = "force-dynamic";

export default function ForgotPasswordPage() {
  // Keycloak owns password reset ("Forgot password?" on the realm's sign-in screen).
  if (usesQavrenAuth()) redirect("/login");
  return <ForgotPasswordClient />;
}
