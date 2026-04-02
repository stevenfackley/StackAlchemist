"use client";

import { Suspense } from "react";
import SimpleModePage from "./SimpleModePage";

export default function SimplePage() {
  return (
    <Suspense fallback={null}>
      <SimpleModePage />
    </Suspense>
  );
}
