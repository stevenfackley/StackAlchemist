import type { Metadata } from "next";

// Title + description intentionally inherit the layout defaults so the
// homepage gets the keyword-rich "AI SaaS Generator with a Compile Guarantee"
// framing (instead of the previous "Home | StackAlchemist").
export const metadata: Metadata = {
  alternates: { canonical: "/" },
};

export { default } from "./HomePageClient";
