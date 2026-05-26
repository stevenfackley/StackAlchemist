import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Register",
  description:
    "Create a StackAlchemist account to track generations and retrieve downloads.",
  robots: { index: false, follow: true },
};

export { default } from "./RegisterPageClient";
