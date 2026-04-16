import type { Metadata } from "next";
import { Suspense } from "react";
import AdvancedModePage from "./AdvancedModePage";

export const metadata: Metadata = {
  title: "Advanced Mode — Visual Schema & API Designer",
  description:
    "Model entities, relationships, and API endpoints in StackAlchemist's visual wizard. Export compiled .NET 10 + Next.js 15 + PostgreSQL source you own — no SaaS lock-in.",
  alternates: { canonical: "/advanced" },
};

export default function AdvancedPage() {
  return (
    <Suspense fallback={null}>
      <AdvancedModePage />
    </Suspense>
  );
}
