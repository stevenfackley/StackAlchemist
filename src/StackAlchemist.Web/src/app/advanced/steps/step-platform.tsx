"use client";

import type { ProjectType } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Eyebrow, Cluster, Stack } from "@/components/ui";

export function StepPlatformSelection({
  selectedProjectType,
  setSelectedProjectType,
}: {
  selectedProjectType: ProjectType;
  setSelectedProjectType: (projectType: ProjectType) => void;
}) {
  const platforms: Array<{
    id: ProjectType;
    title: string;
    badge: string;
    description: string;
    details: string[];
  }> = [
    {
      id: "DotNetNextJs",
      title: ".NET 10 + Next.js 15",
      badge: "Default",
      description: "C# minimal API backend with a Next.js App Router frontend.",
      details: ["Backend: .NET 10", "Frontend: Next.js 15", "Data access: Dapper + PostgreSQL"],
    },
    {
      id: "PythonReact",
      title: "FastAPI + React/Vite",
      badge: "New",
      description: "Python API stack with a Vite React frontend and modern query tooling.",
      details: ["Backend: FastAPI + SQLAlchemy", "Frontend: React + Vite", "Tooling: Alembic + TanStack Query"],
    },
  ];

  return (
    <Stack gap="md">
      <Eyebrow>Select the platform you want StackAlchemist to generate</Eyebrow>
      <Stack gap="md">
        {platforms.map((platform) => (
          <button
            key={platform.id}
            type="button"
            onClick={() => setSelectedProjectType(platform.id)}
            className={cn(
              "rounded-panel border p-5 text-left transition-all duration-300",
              selectedProjectType === platform.id
                ? "border-accent/60 bg-accent/10 shadow-[0_0_20px_rgba(77,166,255,0.15)]"
                : "border-border bg-surface-2/30 hover:border-accent/30",
            )}
          >
            <div className="flex items-start justify-between gap-3">
              <Stack gap="xs">
                <p className="font-mono text-sm font-bold uppercase tracking-[0.15em] text-ink">{platform.title}</p>
                <p className="text-sm text-ink-muted">{platform.description}</p>
              </Stack>
              <span className="rounded-full border border-accent/30 bg-accent/10 px-2 py-1 font-mono text-[0.625rem] uppercase tracking-[0.15em] text-accent">
                {platform.badge}
              </span>
            </div>
            <Cluster gap="xs" className="mt-4">
              {platform.details.map((detail) => (
                <span
                  key={detail}
                  className="rounded-full border border-border-strong bg-surface-0/50 px-2.5 py-1 font-mono text-[0.625rem] text-ink-muted"
                >
                  {detail}
                </span>
              ))}
            </Cluster>
          </button>
        ))}
      </Stack>
    </Stack>
  );
}
