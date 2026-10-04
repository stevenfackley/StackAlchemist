import type { Metadata } from "next";
import { QavrenLoginPage } from "./QavrenLoginPage";

export const metadata: Metadata = {
  title: "Login",
  description:
    "Sign in to StackAlchemist to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

// The page shows the sign-in host from QAVREN_AUTH_URL, a runtime secret that
// `next build` never sees; a prerendered page would freeze the build's value in.
export const dynamic = "force-dynamic";

export default async function LoginPage(props: {
  searchParams: Promise<{ error?: string; returnTo?: string }>;
}) {
  return <QavrenLoginPage params={await props.searchParams} />;
}
