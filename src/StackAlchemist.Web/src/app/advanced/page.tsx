import type { Metadata } from "next";
import { Suspense } from "react";
import AdvancedModePage from "./AdvancedModePage";

export const metadata: Metadata = {
  title: "Advanced Mode",
  description:
    "Model entities, relationships, and API endpoints in the visual wizard.",
};

export default function AdvancedPage() {
  return (
    <Suspense fallback={null}>
      <AdvancedModePage />
    </Suspense>
  );
}
