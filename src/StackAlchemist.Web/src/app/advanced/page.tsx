import type { Metadata } from "next";
import { Suspense } from "react";
import AdvancedModePage from "./AdvancedModePage";

type AdvancedSearchParams = { step?: string; tier?: string };

const STEP_TITLES: Record<string, string> = {
  "1": "Entities — Advanced Mode",
  "2": "Relationships — Advanced Mode",
  "3": "API Endpoints — Advanced Mode",
  "4": "Tier Selection — Advanced Mode",
};

const STEP_DESCRIPTIONS: Record<string, string> = {
  "1": "Define your domain entities in StackAlchemist's visual wizard — typed fields, PKs, nullability.",
  "2": "Model one-to-many and many-to-many relationships between entities before code generation.",
  "3": "Design the REST endpoints that will be compiled into your .NET 10 + Next.js 15 repository.",
  "4": "Pick the handoff depth: Blueprint, Boilerplate, or full Infrastructure runbook.",
};

export async function generateMetadata({
  searchParams,
}: {
  searchParams: Promise<AdvancedSearchParams>;
}): Promise<Metadata> {
  const sp = await searchParams;
  const step = sp.step ?? "1";
  const title = STEP_TITLES[step] ?? "Advanced Mode — Visual Schema & API Designer";
  const description =
    STEP_DESCRIPTIONS[step] ??
    "Model entities, relationships, and API endpoints in StackAlchemist's visual wizard.";

  return {
    title,
    description,
    alternates: { canonical: "/advanced" },
    openGraph: { title, description, url: "/advanced" },
  };
}

export default function AdvancedPage() {
  return (
    <>
      <h1 className="sr-only">Advanced Mode — Design Entities, Relationships, and Endpoints</h1>
      <Suspense fallback={null}>
        <AdvancedModePage />
      </Suspense>
    </>
  );
}
