import { redirect } from "next/navigation";

export default function ForgotPasswordPage() {
  // Keycloak owns password reset ("Forgot password?" on the realm's sign-in screen).
  redirect("/login");
}
