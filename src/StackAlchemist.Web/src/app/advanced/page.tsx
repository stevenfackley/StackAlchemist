"use client";

import { Suspense } from "react";
import AdvancedModePage from "./AdvancedModePage";

export default function AdvancedPage() {
  return (
    <Suspense fallback={null}>
      <AdvancedModePage />
    </Suspense>
  );
}
