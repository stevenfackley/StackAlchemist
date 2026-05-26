import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Login",
  description:
    "Sign in to StackAlchemist to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

export { default } from "./LoginPageClient";
