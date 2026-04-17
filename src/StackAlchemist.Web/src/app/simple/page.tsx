import type { Metadata } from "next";
import { Suspense } from "react";
import SimpleModePage from "./SimpleModePage";

export const metadata: Metadata = {
  title: "Simple Mode — Natural-Language SaaS Generator",
  description:
    "Describe your product in plain English. StackAlchemist extracts the schema and hands back a compiled .NET + Next.js repo.",
  alternates: { canonical: "/simple" },
};

export default function SimplePage() {
  return (
    <>
      <h1 className="sr-only">Simple Mode — Generate a Full-Stack App From a Prompt</h1>
      <Suspense fallback={null}>
        <SimpleModePage />
      </Suspense>
    </>
  );
}
