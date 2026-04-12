import type { Metadata } from "next";
import { Suspense } from "react";
import SimpleModePage from "./SimpleModePage";

export const metadata: Metadata = {
  title: "Simple Mode",
  description:
    "Describe your product in plain English and generate a schema-first SaaS workspace.",
};

export default function SimplePage() {
  return (
    <Suspense fallback={null}>
      <SimpleModePage />
    </Suspense>
  );
}
