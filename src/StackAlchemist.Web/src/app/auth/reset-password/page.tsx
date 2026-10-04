import { redirect } from "next/navigation";

export default function ResetPasswordPage() {
  // Keycloak owns password reset ("Forgot password?" on the realm's sign-in screen).
  redirect("/login");
}
