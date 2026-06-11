import type { Generation, Tier } from "@/lib/types";

export const STATUS_STEPS: Generation["status"][] = [
  "pending",
  "extracting_schema",
  "generating_code",
  "building",
  "packing",
  "uploading",
  "success",
];

export const STATUS_LABELS: Record<Generation["status"], string> = {
  pending: "Queued — waiting to start",
  extracting_schema: "Extracting schema from prompt",
  generating_code: "Synthesizing code with Claude Sonnet 4.6",
  generating: "Regenerating with compiler feedback",
  building: "Compile Guarantee — running dotnet build",
  packing: "Packaging your codebase",
  uploading: "Uploading your download",
  success: "Complete",
  failed: "Build failed — triggering auto-correction",
};

export const STATUS_DESCRIPTIONS: Record<Generation["status"], string> = {
  pending: "Your generation is in the queue. It'll kick off within seconds.",
  extracting_schema:
    "Claude is reading your prompt and identifying entities, relationships, and API endpoints.",
  generating_code:
    "Generating the full source tree — API controllers, repositories, models, frontend pages, and styles.",
  generating:
    "A build error came back — Claude is regenerating the affected files using the compiler output.",
  building:
    "Running dotnet build inside a container. If it fails, we auto-correct with the compiler output and retry (up to 3×).",
  packing: "Bundling the generated source tree into a downloadable archive.",
  uploading: "Uploading your archive to storage and finalizing the download link.",
  success: "Your codebase is ready.",
  failed:
    "The build failed. We're retrying automatically. If it still fails after 3 attempts, you'll get a full refund.",
};

export function stepIndex(status: Generation["status"]): number {
  // "generating" is the build-retry alias of "generating_code" — same ladder rung.
  const rung = status === "generating" ? "generating_code" : status;
  return STATUS_STEPS.indexOf(rung);
}

export function isFreeGeneration(gen: Generation): boolean {
  return gen.tier === 0;
}

export const TIER_NAMES: Record<number, string> = {
  0: "Spark",
  1: "Blueprint",
  2: "Boilerplate",
  3: "Infrastructure",
};

export const PAID_TIERS: { id: Tier; name: string; price: string; tagline: string }[] = [
  { id: 1, name: "Blueprint", price: "$299", tagline: "Schema + API docs + SQL scripts" },
  { id: 2, name: "Boilerplate", price: "$599", tagline: "Full source + Compile Guarantee" },
  { id: 3, name: "Infrastructure", price: "$999", tagline: "Everything + IaC + Runbook" },
];
