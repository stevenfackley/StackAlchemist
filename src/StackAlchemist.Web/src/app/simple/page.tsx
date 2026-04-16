import type { Metadata } from "next";
import { Suspense } from "react";
import SimpleModePage from "./SimpleModePage";

export const metadata: Metadata = {
  title: "Simple Mode — Natural-Language SaaS Generator",
  description:
    "Describe your product in plain English. StackAlchemist's AI app builder extracts the schema, generates a Next.js frontend, and hands back a compiled .NET + PostgreSQL repository.",
  alternates: { canonical: "/simple" },
};

export default function SimplePage() {
  return (
    <Suspense fallback={null}>
      <SimpleModePage />
    </Suspense>
  );
}
