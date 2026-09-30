import type { Metadata } from "next";
import { usesQavrenAuth } from "@/lib/runtime-config";
import LoginPageClient from "./LoginPageClient";
import { QavrenLoginPage } from "./QavrenLoginPage";

export const metadata: Metadata = {
  title: "Login",
  description:
    "Sign in to StackAlchemist to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

// The auth mode is a runtime secret (QAVREN_AUTH_URL) that `next build` never
// sees; a prerendered page would freeze Supabase mode in at build time.
export const dynamic = "force-dynamic";

export default async function LoginPage(props: {
  searchParams: Promise<{ error?: string; returnTo?: string }>;
}) {
  if (!usesQavrenAuth()) return <LoginPageClient />;
  return <QavrenLoginPage params={await props.searchParams} />;
}
