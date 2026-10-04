import type { Metadata } from "next";
import { QavrenRegisterPage } from "./QavrenRegisterPage";

export const metadata: Metadata = {
  title: "Register",
  description:
    "Create a StackAlchemist account to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

// The page shows the sign-in host from QAVREN_AUTH_URL, a runtime secret that
// `next build` never sees; a prerendered page would freeze the build's value in.
export const dynamic = "force-dynamic";

export default async function RegisterPage(props: {
  searchParams: Promise<{ error?: string; returnTo?: string }>;
}) {
  return <QavrenRegisterPage params={await props.searchParams} />;
}
