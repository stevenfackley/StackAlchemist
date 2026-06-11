"use client";

import { useCallback, useEffect, useMemo, useRef, useState, useTransition } from "react";
import { useSearchParams } from "next/navigation";
import { Loader2 } from "lucide-react";
import { submitAdvancedGeneration, createPendingGeneration, createCheckoutSession } from "@/lib/actions";
import { useGenerationRealtime } from "@/lib/hooks/use-generation-realtime";
import { useFreeQuota } from "@/lib/hooks/use-free-quota";
import { useLocalStorageDraft } from "@/lib/hooks/use-local-storage-draft";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import { PersonalizationModal } from "@/components/personalization-modal";
import { Alert } from "@/components/ui";
import { StepRail, Workspace, WizardFooter } from "@/components/workspace";
import type { Entity, Relationship, Endpoint, Tier, Generation, ProjectType, PersonalizationData } from "@/lib/types";
import { DEFAULT_PERSONALIZATION } from "@/lib/types";
import { StepEntities } from "./steps/step-entities";
import { StepEndpoints } from "./steps/step-endpoints";
import { StepPlatformSelection } from "./steps/step-platform";
import { StepTier } from "./steps/step-tier";
import { StepPersonalize } from "./steps/step-personalize";
import { ERPreview } from "./er-preview";
import { SubmittingPanel } from "./submit-phases";

const STEPS = ["Define Entities", "Platform Selection", "Configure API", "Personalize", "Select Tier & Pay"];

// Everything the user builds in the wizard, consolidated so ONE localStorage
// draft covers it — a refresh, crash, or checkout login-bounce previously wiped
// all of it (the state lived only in React memory).
interface AdvancedDraft {
  entities: Entity[];
  relationships: Relationship[];
  endpoints: Endpoint[];
  projectType: ProjectType;
  tier: Tier;
  personalization: PersonalizationData;
  step: number;
}

const DEFAULT_ENTITIES: Entity[] = [{
  name: "Product",
  fields: [
    { name: "id", type: "UUID", pk: true },
    { name: "name", type: "String", pk: false },
    { name: "price", type: "Decimal", pk: false },
  ],
}];

const ADVANCED_DRAFT_KEY = "sa:draft:advanced:v1";

const clampStep = (raw: number) => Math.min(Math.max(Number.isFinite(raw) ? raw : 1, 1), 5);

export default function AdvancedModePage() {
  const searchParams = useSearchParams();
  const initialStep = clampStep(Number(searchParams?.get("step") ?? "1"));
  const initialTier = Number(searchParams?.get("tier") ?? "2") as Tier;
  const initialProjectType = (searchParams?.get("projectType") as ProjectType | null) ?? "DotNetNextJs";

  const { quota } = useFreeQuota();

  const {
    value: draft,
    setValue: setDraft,
    noticeVisible: draftNoticeVisible,
    restoredAt: draftRestoredAt,
    dismissNotice: dismissDraftNotice,
    clearDraft,
  } = useLocalStorageDraft<AdvancedDraft>(
    ADVANCED_DRAFT_KEY,
    () => ({
      entities: DEFAULT_ENTITIES,
      relationships: [],
      endpoints: [],
      projectType: initialProjectType,
      tier: initialTier,
      personalization: DEFAULT_PERSONALIZATION,
      step: initialStep,
    }),
    {
      version: 1,
      // Don't litter storage (or show restore notices) for visits that never
      // edited anything beyond navigating steps / picking a tier via URL.
      isDefault: (d) =>
        JSON.stringify(d.entities) === JSON.stringify(DEFAULT_ENTITIES) &&
        d.relationships.length === 0 &&
        d.endpoints.length === 0 &&
        JSON.stringify(d.personalization) === JSON.stringify(DEFAULT_PERSONALIZATION),
      // Deep links beat drafts: a pricing-page link to ?step=5&tier=1 must land
      // there even when a draft remembers step 2 / tier 2. Only params literally
      // present in the URL override.
      transformOnRestore: (d) => ({
        ...d,
        step: searchParams?.has("step") ? initialStep : clampStep(d.step),
        tier: searchParams?.has("tier") ? initialTier : d.tier,
        projectType: searchParams?.has("projectType") ? initialProjectType : d.projectType,
      }),
    },
  );

  const { entities, relationships, endpoints, personalization, step } = draft;
  const selectedProjectType = draft.projectType;
  const selectedTier = draft.tier;

  // Slice setters keep the child step components' Dispatch<SetStateAction<…>>
  // prop contracts intact while all state lives in the single draft object.
  const makeSliceSetter = useCallback(
    <K extends keyof AdvancedDraft>(key: K): React.Dispatch<React.SetStateAction<AdvancedDraft[K]>> =>
      (action) =>
        setDraft((d) => ({
          ...d,
          [key]:
            typeof action === "function"
              ? (action as (prev: AdvancedDraft[K]) => AdvancedDraft[K])(d[key])
              : action,
        })),
    [setDraft],
  );

  const setEntities = useMemo(() => makeSliceSetter("entities"), [makeSliceSetter]);
  const setRelationships = useMemo(() => makeSliceSetter("relationships"), [makeSliceSetter]);
  const setEndpoints = useMemo(() => makeSliceSetter("endpoints"), [makeSliceSetter]);
  const setSelectedProjectType = useMemo(() => makeSliceSetter("projectType"), [makeSliceSetter]);
  const setSelectedTier = useMemo(() => makeSliceSetter("tier"), [makeSliceSetter]);
  const setPersonalization = useMemo(() => makeSliceSetter("personalization"), [makeSliceSetter]);
  const setStep = useMemo(() => makeSliceSetter("step"), [makeSliceSetter]);

  // Keep ?step= (and tier) honest in the URL so refresh and shared links land on
  // the same step. Native replaceState, NOT router.push: this flow is
  // COOP-sensitive and must not soft-navigate.
  useEffect(() => {
    const url = new URL(window.location.href);
    const current = url.searchParams.get("step");
    const currentTier = url.searchParams.get("tier");
    if (current === String(step) && currentTier === String(selectedTier)) return;
    url.searchParams.set("step", String(step));
    url.searchParams.set("tier", String(selectedTier));
    window.history.replaceState(null, "", url);
  }, [step, selectedTier]);

  const [showPersonalizationModal, setShowPersonalizationModal] = useState(false);
  // Snapshot for cancel-restore: the modal live-patches parent state while
  // editing (so the draft persists keystrokes), and cancel rolls back to this.
  const personalizationSnapshotRef = useRef<PersonalizationData>(DEFAULT_PERSONALIZATION);
  const [submitPhase, setSubmitPhase] = useState<"idle" | "submitting" | "submitted" | "error">("idle");
  const [generationId, setGenerationId] = useState<string | null>(null);
  const [liveStatus, setLiveStatus] = useState<Generation["status"] | null>(null);
  const [liveBuildLog, setLiveBuildLog] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  // Real-time subscription (resilient: catch-up fetch, re-subscribe, polling
  // fallback — see useGenerationRealtime). The `done` ref guards against
  // double-acting: the live event and the catch-up fetch can both report success.
  const doneRef = useRef(false);
  const applyGenerationUpdate = useCallback(
    (row: Generation) => {
      if (doneRef.current) return;
      setLiveStatus(row.status);
      setLiveBuildLog(row.build_log);
      if (row.status === "success" && (row.download_url || selectedTier === 0)) {
        // Spark (free): redirect when preview_files_json is ready (no download_url).
        // Paid tiers: redirect when download_url is ready.
        doneRef.current = true;
        // The build shipped — the draft served its purpose. Clear BEFORE the hard
        // nav (side effects after window.location.href may never run).
        clearDraft();
        // Full-page load (NOT router.push): the result page must arrive as a fresh
        // document so its COOP/COEP headers apply and window.crossOriginIsolated is
        // true — required by StackBlitz WebContainers. A soft nav reuses the current
        // (non-isolated) document → StackBlitz errors "without isolation headers".
        window.location.href = `/generate/${generationId}`;
      } else if (row.status === "failed") {
        doneRef.current = true;
        setErrorMsg(row.error_message ?? "Generation failed.");
        setSubmitPhase("error");
      }
    },
    [generationId, selectedTier, clearDraft]
  );

  useGenerationRealtime({
    generationId,
    enabled: !!generationId,
    onUpdate: applyGenerationUpdate,
  });

  function handleCheckout() {
    startTransition(async () => {
      setSubmitPhase("submitting");
      setErrorMsg(null);

      const schema = { entities, relationships, endpoints };

      if (selectedTier === 0) {
        // Free tier: create generation + fire engine immediately
        const result = await submitAdvancedGeneration(schema, 0, selectedProjectType, personalization);
        if (result.success) {
          setGenerationId(result.generationId);
          if (isDemoMode || !supabase) {
            // Build submitted — clear the draft BEFORE the hard nav.
            clearDraft();
            // Hard nav (see realtime handler above) so the preview page is isolated.
            window.location.href = `${result.redirectUrl}${result.redirectUrl.includes("?") ? "&" : "?"}tier=0`;
            return;
          }
          setSubmitPhase("submitted");
        } else {
          setErrorMsg(result.error);
          setSubmitPhase("error");
        }
      } else {
        // Paid tiers 1–3: create pending row → Stripe Checkout → Engine fires via webhook
        const pending = await createPendingGeneration("advanced", selectedTier, undefined, schema, selectedProjectType, personalization);
        if (!pending.success) {
          setErrorMsg(pending.error);
          setSubmitPhase("error");
          return;
        }

        setGenerationId(pending.generationId);

        const session = await createCheckoutSession(
          pending.generationId,
          selectedTier,
          undefined,
          selectedProjectType,
          "advanced"
        );
        if (!session.success) {
          setErrorMsg(session.error);
          setSubmitPhase("error");
          return;
        }

        // Redirect to Stripe-hosted Checkout (or demo bypass URL)
        window.location.href = session.sessionUrl;
      }
    });
  }

  if (submitPhase === "submitting" || submitPhase === "submitted") {
    return (
      <SubmittingPanel
        generationId={generationId}
        selectedProjectType={selectedProjectType}
        selectedTier={selectedTier}
        liveStatus={liveStatus}
        liveBuildLog={liveBuildLog}
      />
    );
  }

  if (submitPhase === "error") {
    return (
      <GenerationErrorPanel
        testId="advanced-phase-error"
        errorMessage={errorMsg}
        onRetry={() => { setSubmitPhase("idle"); setErrorMsg(null); }}
      />
    );
  }

  return (
    <>
      {showPersonalizationModal && (
        <PersonalizationModal
          entityNames={entities.map((e) => e.name).filter(Boolean)}
          initialData={personalization}
          onChange={setPersonalization}
          onComplete={() => setShowPersonalizationModal(false)}
          onCancel={() => {
            // Roll the live-patched edits back to the pre-open snapshot.
            setPersonalization(personalizationSnapshotRef.current);
            setShowPersonalizationModal(false);
          }}
        />
      )}
      <div data-testid="advanced-mode-page" className="flex h-full min-h-0 flex-col">
        <div className="flex min-h-0 flex-1">
          {/* Chunk 1 — Step navigation */}
          <StepRail steps={STEPS} current={step} onSelect={setStep} />

          <div className="flex min-h-0 flex-1 flex-col xl:flex-row">
            {/* Chunk 2 — Workspace (active step) */}
            <Workspace>
              {draftNoticeVisible && (
                <Alert variant="info" data-testid="advanced-draft-restored" className="mb-4">
                  <span>
                    Draft restored{draftRestoredAt ? ` from ${draftRestoredAt.toLocaleString()}` : ""} —{" "}
                    <button
                      type="button"
                      onClick={() => {
                        clearDraft();
                        setDraft({
                          entities: DEFAULT_ENTITIES,
                          relationships: [],
                          endpoints: [],
                          projectType: initialProjectType,
                          tier: initialTier,
                          personalization: DEFAULT_PERSONALIZATION,
                          step: 1,
                        });
                      }}
                      className="underline underline-offset-2 transition-colors hover:text-ink"
                    >
                      start fresh
                    </button>
                  </span>
                  <button
                    type="button"
                    aria-label="Dismiss draft notice"
                    onClick={dismissDraftNotice}
                    className="ml-auto font-mono text-ink-faint transition-colors hover:text-ink"
                  >
                    ✕
                  </button>
                </Alert>
              )}
              {step === 1 && (
                <div data-testid="advanced-step-1">
                  <StepEntities entities={entities} setEntities={setEntities} relationships={relationships} setRelationships={setRelationships} />
                </div>
              )}
              {step === 2 && (
                <div data-testid="advanced-step-2">
                  <StepPlatformSelection selectedProjectType={selectedProjectType} setSelectedProjectType={setSelectedProjectType} />
                </div>
              )}
              {step === 3 && (
                <div data-testid="advanced-step-3">
                  <StepEndpoints endpoints={endpoints} setEndpoints={setEndpoints} entityNames={entities.map((e) => e.name).filter(Boolean)} />
                </div>
              )}
              {step === 4 && (
                <div data-testid="advanced-step-4">
                  <StepPersonalize
                    personalization={personalization}
                    setPersonalization={setPersonalization}
                    onOpenModal={() => {
                      personalizationSnapshotRef.current = personalization;
                      setShowPersonalizationModal(true);
                    }}
                  />
                </div>
              )}
              {step === 5 && (
                <div data-testid="advanced-step-5">
                  <StepTier selectedTier={selectedTier} setSelectedTier={setSelectedTier} quota={quota} />
                </div>
              )}
            </Workspace>

            {/* Chunk 3 — Live ER preview (deferred / collapsible) */}
            <ERPreview entities={entities} relationships={relationships} />
          </div>
        </div>

        {/* Quota-exhausted + Spark alert — only visible on step 5 */}
        {step === 5 && selectedTier === 0 && quota?.remaining === 0 && (
          <Alert variant="warning" data-testid="advanced-quota-exhausted" className="mx-4 mb-2">
            Free build limit reached. Resets {quota.resetsAtLabel}.{" "}
            <a href="#pricing" className="underline underline-offset-2">View paid tiers →</a>
          </Alert>
        )}

        {/* Chunk 4 — Action bar */}
        <WizardFooter
          step={step}
          totalSteps={5}
          backHref="/"
          onPrevious={() => setStep((s) => Math.max(1, s - 1))}
          onNext={() => setStep((s) => Math.min(5, s + 1))}
          onCheckout={handleCheckout}
          checkoutDisabled={isPending || (selectedTier === 0 && quota?.remaining === 0)}
          checkoutVariant={selectedTier === 0 ? "success" : "primary"}
          checkoutLabel={
            isPending
              ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Processing...</>
              : selectedTier === 0
                ? "Launch Free Preview →"
                : "Proceed to Checkout →"
          }
        />
      </div>
    </>
  );
}
