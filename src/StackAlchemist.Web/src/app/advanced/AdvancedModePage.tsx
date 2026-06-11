"use client";

import { useCallback, useEffect, useState, useTransition } from "react";
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
import { CheckCircle2, Loader2, AlertCircle, Zap } from "lucide-react";
import { cn } from "@/lib/utils";
import { submitAdvancedGeneration, createPendingGeneration, createCheckoutSession } from "@/lib/actions";
import { supabase } from "@/lib/supabase";
import { isDemoMode } from "@/lib/runtime-config";
import { BuildLogConsole } from "@/components/build-log-console";
import { PersonalizationModal } from "@/components/personalization-modal";
import { Button, Cluster, Eyebrow, Label, Panel, Select, Stack, TextInput, Toggle } from "@/components/ui";
import { ContextPanel, StepRail, WizardFooter, Workspace } from "@/components/workspace";
import type { Entity, Relationship, Endpoint, Tier, Generation, ProjectType, PersonalizationData } from "@/lib/types";
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
function StepTier({ selectedTier, setSelectedTier }: { selectedTier: Tier; setSelectedTier: (t: Tier) => void }) {
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

export default function AdvancedModePage() {
  const searchParams = useSearchParams();
  const initialStep = Number(searchParams?.get("step") ?? "1");
  const initialTier = Number(searchParams?.get("tier") ?? "2") as Tier;
  const initialProjectType = (searchParams?.get("projectType") as ProjectType | null) ?? "DotNetNextJs";

  const [step, setStep] = useState(Math.min(Math.max(initialStep, 1), 5));
  const [entities, setEntities] = useState<Entity[]>([{
    name: "Product",
    fields: [
      { name: "id", type: "UUID", pk: true },
      { name: "name", type: "String", pk: false },
      { name: "price", type: "Decimal", pk: false },
    ],
  }]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [endpoints, setEndpoints] = useState<Endpoint[]>([]);
  const [selectedProjectType, setSelectedProjectType] = useState<ProjectType>(initialProjectType);
  const [selectedTier, setSelectedTier] = useState<Tier>(initialTier);
  const [personalization, setPersonalization] = useState<PersonalizationData>(DEFAULT_PERSONALIZATION);
  const [showPersonalizationModal, setShowPersonalizationModal] = useState(false);
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

  // Real-time subscription
  useEffect(() => {
    if (!generationId || !supabase) return;
    const client = supabase;
    // Guards against double-acting (the live event and the post-subscribe fetch
    // can both report success) and against state updates after redirect/unmount.
    let done = false;

    const apply = (row: Pick<Generation, "status" | "download_url" | "error_message" | "build_log">) => {
      if (done) return;
      setLiveStatus(row.status);
      setLiveBuildLog(row.build_log);
      if (row.status === "success" && (row.download_url || selectedTier === 0)) {
        // Spark (free): redirect when preview_files_json is ready (no download_url).
        // Paid tiers: redirect when download_url is ready.
        done = true;
        // Full-page load (NOT router.push): the result page must arrive as a fresh
        // document so its COOP/COEP headers apply and window.crossOriginIsolated is
        // true — required by StackBlitz WebContainers. A soft nav reuses the current
        // (non-isolated) document → StackBlitz errors "without isolation headers".
        window.location.href = `/generate/${generationId}`;
      } else if (row.status === "failed") {
        done = true;
        setErrorMsg(row.error_message ?? "Generation failed.");
        setSubmitPhase("error");
      }
    };

    const channel = client
      .channel(`gen-adv:${generationId}`)
      .on("postgres_changes",
        { event: "UPDATE", schema: "public", table: "generations", filter: `id=eq.${generationId}` },
        (payload) => apply(payload.new as Generation)
      )
      .subscribe((status) => {
        // Race fix: the deterministic Tier-0 render can set status=success BEFORE
        // this WS handshake finishes, and postgres_changes does not replay events
        // missed before SUBSCRIBED — so a fast generation would never redirect.
        // Once the stream is live, fetch the current row once to catch an
        // already-finished generation; later completions still arrive via the
        // live UPDATE handler above.
        if (status !== "SUBSCRIBED") return;
        void (async () => {
          const { data } = await client
            .from("generations")
            .select("status, download_url, error_message, build_log")
            .eq("id", generationId)
            .single();
          if (data) apply(data as Pick<Generation, "status" | "download_url" | "error_message" | "build_log">);
        })();
      });

    return () => {
      done = true;
      client.removeChannel(channel);
    };
  }, [generationId, selectedTier]);

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
      <div className="flex h-full flex-col items-center justify-center px-4">
        <div className="w-full max-w-md space-y-4 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full border border-danger/30 bg-danger/10">
            <AlertCircle className="h-8 w-8 text-danger" />
          </div>
          <h2 className="text-xl font-bold text-ink">Something went wrong</h2>
          <p className="text-sm text-ink-muted">{errorMsg}</p>
          <div className="flex justify-center gap-3">
            <Button variant="ghost" size="sm" onClick={() => { setSubmitPhase("idle"); setErrorMsg(null); }}>
              ← Back
            </Button>
            <Link href="/" className="rounded-full bg-accent px-5 py-2.5 font-mono text-xs uppercase tracking-[0.15em] text-white transition-colors hover:bg-accent/90">
              Start Over
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
      {showPersonalizationModal && (
        <PersonalizationModal
          entityNames={entities.map((e) => e.name).filter(Boolean)}
          onComplete={(data) => { setPersonalization(data); setShowPersonalizationModal(false); }}
          onSkip={() => setShowPersonalizationModal(false)}
        />
      )}
      <div data-testid="advanced-mode-page" className="flex h-full min-h-0 flex-col">
        <div className="flex min-h-0 flex-1">
          {/* Chunk 1 — Step navigation */}
          <StepRail steps={STEPS} current={step} onSelect={setStep} />

          <div className="flex min-h-0 flex-1 flex-col xl:flex-row">
            {/* Chunk 2 — Workspace (active step) */}
            <Workspace>
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
                            onClick={() => setShowPersonalizationModal(true)}
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
                  <StepTier selectedTier={selectedTier} setSelectedTier={setSelectedTier} />
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

        {/* Chunk 4 — Action bar */}
        <WizardFooter
          step={step}
          totalSteps={5}
          backHref="/"
          onPrevious={() => setStep((s) => Math.max(1, s - 1))}
          onNext={() => setStep((s) => Math.min(5, s + 1))}
          onCheckout={handleCheckout}
          checkoutDisabled={isPending}
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
