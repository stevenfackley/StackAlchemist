import { redirect } from "next/navigation";
import { usesQavrenAuth } from "@/lib/runtime-config";
import ResetPasswordClient from "./ResetPasswordClient";

// The auth mode is a runtime secret (QAVREN_AUTH_URL) that `next build` never
// sees; a prerendered page would freeze Supabase mode in at build time.
export const dynamic = "force-dynamic";

export default function ResetPasswordPage() {
  // Supabase recovery links land here; in Qavren mode Keycloak owns password reset.
  if (usesQavrenAuth()) redirect("/login");
  return <ResetPasswordClient />;
}
