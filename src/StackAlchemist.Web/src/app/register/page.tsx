import type { Metadata } from "next";
import { usesQavrenAuth } from "@/lib/runtime-config";
import RegisterPageClient from "./RegisterPageClient";
import { QavrenRegisterPage } from "./QavrenRegisterPage";

export const metadata: Metadata = {
  title: "Register",
  description:
    "Create a StackAlchemist account to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

// The auth mode is a runtime secret (QAVREN_AUTH_URL) that `next build` never
// sees; a prerendered page would freeze Supabase mode in at build time.
export const dynamic = "force-dynamic";

export default async function RegisterPage(props: {
  searchParams: Promise<{ error?: string; returnTo?: string }>;
}) {
  if (!usesQavrenAuth()) return <RegisterPageClient />;
  return <QavrenRegisterPage params={await props.searchParams} />;
}
