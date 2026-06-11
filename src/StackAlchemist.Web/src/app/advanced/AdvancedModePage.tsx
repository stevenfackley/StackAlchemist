"use client";

import { useCallback, useEffect, useMemo, useRef, useState, useTransition } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ReactFlow,
  Background,
  Controls,
  addEdge,
  useNodesState,
  useEdgesState,
  type Node,
  type Edge,
  type Connection,
  BackgroundVariant,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { CheckCircle2, Loader2, Zap } from "lucide-react";
import { cn } from "@/lib/utils";
import { submitAdvancedGeneration, createPendingGeneration, createCheckoutSession } from "@/lib/actions";
import { useGenerationRealtime } from "@/lib/hooks/use-generation-realtime";
import { useFreeQuota } from "@/lib/hooks/use-free-quota";
import { useLocalStorageDraft } from "@/lib/hooks/use-local-storage-draft";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";
import { BuildLogConsole } from "@/components/build-log-console";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import { PersonalizationModal } from "@/components/personalization-modal";
import { Alert, Button, Cluster, Eyebrow, Label, Panel, Select, Stack, TextInput, Toggle } from "@/components/ui";
import { ContextPanel, StepRail, WizardFooter, Workspace } from "@/components/workspace";
import type { Entity, Relationship, Endpoint, Tier, Generation, ProjectType, PersonalizationData } from "@/lib/types";
import type { FreeQuotaStatus } from "@/lib/actions";
import { DEFAULT_PERSONALIZATION } from "@/lib/types";

const FIELD_TYPES = ["UUID", "String", "Integer", "Decimal", "Boolean", "Timestamp", "Text", "JSON"];
const REL_TYPES: Relationship["type"][] = ["Has Many", "Belongs To", "Has One", "Many To Many"];
const HTTP_METHODS: Endpoint["method"][] = ["GET", "POST", "PUT", "DELETE", "PATCH"];

// Shared micro-control affordances — all on the token palette + spacing scale.
const ICON_BTN = "px-1 font-mono text-danger/60 transition-colors hover:text-danger";
const ADD_BLOCK =
  "w-full rounded-control border border-accent/30 px-4 py-2.5 font-mono text-xs uppercase tracking-[0.15em] text-accent transition-colors hover:bg-accent/10";
const ADD_INLINE =
  "self-start font-mono text-[0.625rem] uppercase tracking-[0.15em] text-accent transition-colors hover:text-accent/80";

// ─── Step 1: Entities + Relationships ─────────────────────────────────────────
function StepEntities({ entities, setEntities, relationships, setRelationships }: {
  entities: Entity[];
  setEntities: React.Dispatch<React.SetStateAction<Entity[]>>;
  relationships: Relationship[];
  setRelationships: React.Dispatch<React.SetStateAction<Relationship[]>>;
}) {
  const entityNames = entities.map((e) => e.name).filter(Boolean);
  return (
    <Stack gap="lg">
      {/* Chunk A — Entities */}
      <Stack gap="md">
        <Eyebrow>Entities</Eyebrow>
        {entities.map((entity, eidx) => (
          <Panel key={eidx}>
            <Stack gap="sm">
              <Cluster gap="sm" className="flex-nowrap">
                <Label className="w-16 shrink-0">Entity</Label>
                <TextInput
                  size="sm"
                  value={entity.name}
                  onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, name: e.target.value } : x))}
                  placeholder="EntityName"
                  className="flex-1"
                />
                <button type="button" onClick={() => setEntities((p) => p.filter((_, i) => i !== eidx))} className={ICON_BTN}>✕</button>
              </Cluster>
              <div className="flex flex-col gap-2 border-l border-border pl-4">
                {entity.fields.map((field, fidx) => (
                  <Cluster key={fidx} gap="xs">
                    <TextInput
                      size="sm"
                      value={field.name}
                      onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, name: e.target.value } : f) } : x))}
                      placeholder="field_name"
                      className="w-28"
                    />
                    <Select
                      size="sm"
                      value={field.type}
                      onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, type: e.target.value } : f) } : x))}
                    >
                      {FIELD_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                    </Select>
                    <Toggle
                      label="PK"
                      checked={field.pk}
                      onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, pk: e.target.checked } : f) } : x))}
                    />
                    <button type="button" onClick={() => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.filter((_, j) => j !== fidx) } : x))} className={ICON_BTN}>✕</button>
                  </Cluster>
                ))}
                <button
                  type="button"
                  onClick={() => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: [...x.fields, { name: "", type: "String", pk: false }] } : x))}
                  className={ADD_INLINE}
                >
                  + Add Field
                </button>
              </div>
            </Stack>
          </Panel>
        ))}
        <button
          type="button"
          onClick={() => setEntities((p) => [...p, { name: "", fields: [{ name: "id", type: "UUID", pk: true }] }])}
          className={ADD_BLOCK}
        >
          + Add Entity
        </button>
      </Stack>

      {/* Chunk B — Relationships */}
      <Stack gap="sm" className="border-t border-border pt-6">
        <Eyebrow>Relationships</Eyebrow>
        {relationships.map((rel, idx) => (
          <Cluster key={idx} gap="xs">
            <Select
              size="sm"
              value={rel.from}
              onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, from: e.target.value } : r))}
            >
              <option value="">From...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </Select>
            <Select
              size="sm"
              value={rel.type}
              onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, type: e.target.value as Relationship["type"] } : r))}
            >
              {REL_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </Select>
            <Select
              size="sm"
              value={rel.to}
              onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, to: e.target.value } : r))}
            >
              <option value="">To...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </Select>
            <button type="button" onClick={() => setRelationships((p) => p.filter((_, i) => i !== idx))} className={ICON_BTN}>✕</button>
          </Cluster>
        ))}
        <button
          type="button"
          onClick={() => setRelationships((p) => [...p, { from: "", type: "Has Many", to: "" }])}
          className={ADD_INLINE}
        >
          + Add Relationship
        </button>
      </Stack>
    </Stack>
  );
}

// ─── Step 3: Endpoints ────────────────────────────────────────────────────────
function StepEndpoints({ endpoints, setEndpoints, entityNames }: {
  endpoints: Endpoint[];
  setEndpoints: React.Dispatch<React.SetStateAction<Endpoint[]>>;
  entityNames: string[];
}) {
  return (
    <Stack gap="md">
      <Eyebrow>Define API endpoints for your entities</Eyebrow>
      {endpoints.map((ep, idx) => (
        <Panel key={idx}>
          <Cluster gap="xs">
            <Select
              size="sm"
              value={ep.method}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, method: e.target.value as Endpoint["method"] } : x))}
              className={cn("w-20 font-bold",
                ep.method === "GET" && "text-success",
                ep.method === "POST" && "text-accent",
                ep.method === "PUT" && "text-yellow-400",
                (ep.method === "DELETE" || ep.method === "PATCH") && "text-danger",
              )}
            >
              {HTTP_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
            </Select>
            <TextInput
              size="sm"
              value={ep.path}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, path: e.target.value } : x))}
              placeholder="/api/v1/resource"
              className="min-w-[140px] flex-1"
            />
            <Select
              size="sm"
              value={ep.entity}
              onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, entity: e.target.value } : x))}
            >
              <option value="">Entity...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </Select>
            <button type="button" onClick={() => setEndpoints((p) => p.filter((_, i) => i !== idx))} className={ICON_BTN}>✕</button>
          </Cluster>
        </Panel>
      ))}
      <button
        type="button"
        onClick={() => setEndpoints((p) => [...p, { method: "GET", path: "", entity: "" }])}
        className={ADD_BLOCK}
      >
        + Add Endpoint
      </button>
    </Stack>
  );
}

// ─── Step 2: Platform ─────────────────────────────────────────────────────────
function StepPlatformSelection({
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

// ─── Step 5: Tier ─────────────────────────────────────────────────────────────
function StepTier({ selectedTier, setSelectedTier, quota }: {
  selectedTier: Tier;
  setSelectedTier: (t: Tier) => void;
  quota: FreeQuotaStatus | null;
}) {
  const tiers: { id: Tier; name: string; price: string; items: string[]; recommended?: boolean; isFree?: boolean }[] = [
    {
      id: 0, name: "SPARK", price: "Free", isFree: true,
      items: ["ER Canvas", "Generated Next.js UI (view-only)", "Live Micro IDE Preview"],
    },
    { id: 1, name: "BLUEPRINT", price: "$299", items: ["Schema JSON", "API Specifications", "SQL Scripts"] },
    { id: 2, name: "BOILERPLATE", price: "$599", items: ["Blueprint features", "Full Source Code", "Compile Guarantee"], recommended: true },
    { id: 3, name: "INFRASTRUCTURE", price: "$999", items: ["Boilerplate features", "AWS CDK Stack", "Helm Charts", "Deployment Runbook"] },
  ];
  return (
    <div data-testid="advanced-step-5-tier-grid" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      {tiers.map((t) => (
        <button key={t.id} type="button" onClick={() => setSelectedTier(t.id)}
          data-testid={`advanced-tier-option-${t.id}`}
          className={cn("relative rounded-panel border p-5 text-left transition-all duration-300",
            selectedTier === t.id
              ? t.isFree
                ? "border-success/60 bg-success/10 shadow-[0_0_20px_rgba(16,185,129,0.15)]"
                : "border-accent/60 bg-accent/10 shadow-[0_0_20px_rgba(77,166,255,0.15)]"
              : "border-border bg-surface-2/30 hover:border-accent/30"
          )}>
          {t.isFree && (
            <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-success px-2 py-0.5 text-[0.625rem] font-medium text-white whitespace-nowrap">
              Free
            </div>
          )}
          {t.recommended && (
            <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-accent px-2 py-0.5 text-[0.625rem] font-medium text-white whitespace-nowrap">
              Recommended
            </div>
          )}
          <Stack gap="sm">
            <div>
              <h3 className="font-mono text-xs font-bold uppercase tracking-[0.15em] text-ink">{t.name}</h3>
              <p className={cn("mt-1 font-mono text-xl font-bold", t.isFree ? "text-success" : "text-accent")}>{t.price}</p>
              {t.isFree && quota && (
                <p className={cn("mt-0.5 font-mono text-[0.625rem]", quota.remaining === 0 ? "text-yellow-400" : "text-ink-faint")}>
                  {quota.remaining === 0
                    ? `Limit reached — resets ${quota.resetsAtLabel}`
                    : `${quota.remaining} of ${quota.limit} free builds remaining`}
                </p>
              )}
            </div>
            <ul className="flex flex-col gap-1">
              {t.items.map((item) => (
                <li key={item} className="flex items-center gap-2 font-mono text-[0.6875rem] text-ink-muted">
                  <span className="text-success">›</span> {item}
                </li>
              ))}
            </ul>
            {selectedTier === t.id && (
              <div className="flex items-center gap-1.5">
                <CheckCircle2 className={cn("h-3.5 w-3.5", t.isFree ? "text-success" : "text-accent")} />
                <span className={cn("font-mono text-[0.625rem]", t.isFree ? "text-success" : "text-accent")}>Selected</span>
              </div>
            )}
          </Stack>
        </button>
      ))}
    </div>
  );
}

// ─── ReactFlow helpers ────────────────────────────────────────────────────────
function entitiesToNodes(entities: Entity[]): Node[] {
  return entities.filter((e) => e.name).map((e, i) => ({
    id: e.name, position: { x: (i % 3) * 280 + 40, y: Math.floor(i / 3) * 250 + 40 },
    data: {
      label: (
        <div className="min-w-[150px] overflow-hidden rounded-lg border border-accent/60 bg-surface-2 text-left">
          <div className="border-b border-accent/60 bg-accent/10 px-3 py-1.5">
            <span className="font-mono text-xs font-bold uppercase tracking-[0.15em] text-accent">{e.name}</span>
          </div>
          <div className="space-y-0.5 px-3 py-2">
            {e.fields.map((f) => (
              <div key={f.name} className="flex items-center gap-2">
                {f.pk && <span className="rounded border border-yellow-400/40 px-0.5 font-mono text-[9px] text-yellow-400">PK</span>}
                <span className="font-mono text-[0.6875rem] text-ink">{f.name}</span>
                <span className="font-mono text-[10px] text-ink-muted">{f.type}</span>
              </div>
            ))}
          </div>
        </div>
      ),
    },
    type: "default", style: { background: "transparent", border: "none", padding: 0 },
  }));
}

function relsToEdges(relationships: Relationship[]): Edge[] {
  return relationships.filter((r) => r.from && r.to).map((r, i) => ({
    id: `rel-${i}`, source: r.from, target: r.to, label: r.type,
    style: { stroke: "#4da6ff" },
    labelStyle: { fill: "#94a3b8", fontFamily: "monospace", fontSize: 10 },
    labelBgStyle: { fill: "#334155" },
  }));
}

// ─── Main Wizard ──────────────────────────────────────────────────────────────
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

  const [, , onNodesChange] = useNodesState<Node>([]);
  const [, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

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
      <div className="flex h-full flex-col items-center justify-center px-4">
        <div className="w-full max-w-md space-y-6 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full border border-accent/30 bg-accent/10">
            <Loader2 className="h-8 w-8 animate-spin text-accent" />
          </div>
          <h2 className="text-xl font-bold text-ink">Synthesizing Your Platform</h2>
          <p className="text-sm text-ink-muted">{liveStatus ? `Status: ${liveStatus}` : "Queued — starting shortly..."}</p>
          {generationId && (
            <Panel className="space-y-2 text-left">
              <p className="font-mono text-xs uppercase text-ink-faint">Generation ID</p>
              <p className="break-all font-mono text-xs text-accent">{generationId}</p>
              <p className="font-mono text-[10px] uppercase text-ink-faint">Platform</p>
              <p className="font-mono text-xs text-ink">{selectedProjectType}</p>
              <p className="font-mono text-xs text-ink-faint">
                {selectedTier === 0
                  ? "Keep this page open — we’ll launch your live preview when it’s ready."
                  : "Keep this page open — we’ll redirect you when your download is ready."}
              </p>
            </Panel>
          )}
          <BuildLogConsole log={liveBuildLog} title="Live Build Output" className="w-full max-w-2xl overflow-hidden rounded-panel border border-border bg-surface-0/60 text-left" />
          <div className="h-1 w-full rounded-full bg-surface-2"><div className="h-full w-3/5 animate-pulse rounded-full bg-accent" /></div>
        </div>
      </div>
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
                            onClick={() => {
                              // Snapshot for cancel-restore before live patching begins.
                              personalizationSnapshotRef.current = personalization;
                              setShowPersonalizationModal(true);
                            }}
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
                </div>
              )}
              {step === 5 && (
                <div data-testid="advanced-step-5">
                  <StepTier selectedTier={selectedTier} setSelectedTier={setSelectedTier} quota={quota} />
                </div>
              )}
            </Workspace>

            {/* Chunk 3 — Live ER preview (deferred / collapsible) */}
            <ContextPanel title="Live Preview" className="xl:w-[clamp(360px,38%,560px)] xl:shrink-0">
              <div className="h-full min-h-[280px] md:min-h-[360px]">
                <ReactFlow
                  nodes={entitiesToNodes(entities)}
                  edges={relsToEdges(relationships)}
                  onNodesChange={onNodesChange}
                  onEdgesChange={onEdgesChange}
                  onConnect={onConnect}
                  fitView
                  colorMode="dark"
                  panOnDrag
                  zoomOnPinch
                  style={{ background: "#1e293b" }}
                >
                  <Background variant={BackgroundVariant.Dots} gap={24} size={1} color="#334155" />
                  <Controls style={{ background: "#334155", border: "1px solid #475569", borderRadius: "8px" }} />
                </ReactFlow>
              </div>
            </ContextPanel>
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
