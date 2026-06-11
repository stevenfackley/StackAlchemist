"use client";

import { Zap } from "lucide-react";
import type { PersonalizationData } from "@/lib/types";
import { Button, Cluster, Eyebrow, Panel, Stack } from "@/components/ui";
import { DEFAULT_PERSONALIZATION } from "@/lib/types";

interface StepPersonalizeProps {
  personalization: PersonalizationData;
  setPersonalization: React.Dispatch<React.SetStateAction<PersonalizationData>>;
  onOpenModal: () => void;
}

export function StepPersonalize({ personalization, setPersonalization, onOpenModal }: StepPersonalizeProps) {
  return (
    <Stack gap="md">
      <Eyebrow>Make your generated project unique</Eyebrow>
      <Panel data-testid="advanced-personalization-section" padding="lg">
        <Stack gap="md">
          <div className="flex items-start gap-3">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-control border border-accent/30 bg-accent/10">
              <Zap className="h-4 w-4 text-accent" />
            </div>
            <div>
              <h3 className="font-mono text-sm font-bold text-ink">Personalization Wizard</h3>
              <p className="mt-1 text-sm text-ink-muted">
                Define your business identity, color theme, domain vocabulary, and feature preferences.
                The generated code will use your brand name, domain language, and selected color tokens.
              </p>
            </div>
          </div>
          {personalization.businessDescription ? (
            <Stack gap="xs">
              <p className="font-mono text-[0.625rem] uppercase tracking-[0.15em] text-success">Configured</p>
              <div className="grid grid-cols-2 gap-2 font-mono text-[0.6875rem]">
                {personalization.projectName && (
                  <div className="rounded-control border border-border bg-surface-0/50 px-3 py-2">
                    <p className="text-ink-faint">Project Name</p>
                    <p className="text-ink">{personalization.projectName}</p>
                  </div>
                )}
                <div className="rounded-control border border-border bg-surface-0/50 px-3 py-2">
                  <p className="text-ink-faint">Color Theme</p>
                  <div className="mt-0.5 flex items-center gap-1.5">
                    <div className="h-2.5 w-2.5 rounded-full" style={{ background: personalization.colorScheme.primary }} />
                    <p className="text-ink">{personalization.colorScheme.name}</p>
                  </div>
                </div>
                <div className="rounded-control border border-border bg-surface-0/50 px-3 py-2">
                  <p className="text-ink-faint">Auth</p>
                  <p className="uppercase text-ink">{personalization.featureFlags.authMethod}</p>
                </div>
              </div>
            </Stack>
          ) : (
            <p className="font-mono text-xs text-ink-faint">Not configured — defaults will be applied.</p>
          )}
          <Cluster gap="xs">
            <Button
              type="button"
              variant="primary"
              size="sm"
              onClick={onOpenModal}
              data-testid="advanced-personalize-button"
            >
              {personalization.businessDescription ? "Edit Personalization" : "Personalize →"}
            </Button>
            {personalization.businessDescription && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setPersonalization(DEFAULT_PERSONALIZATION)}
              >
                Reset
              </Button>
            )}
          </Cluster>
        </Stack>
      </Panel>
    </Stack>
  );
}
