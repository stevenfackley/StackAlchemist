import type { Metadata } from "next";
import { Suspense } from "react";
import SimpleModePage from "./SimpleModePage";

export const metadata: Metadata = {
  title: "Simple Mode — Natural-Language SaaS Generator",
  description:
    "Describe your product in plain English. Your free Spark build boots a demo app in the browser; paid tiers hand back a compiled .NET + Next.js repo built from your schema.",
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
