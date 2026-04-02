"use client";

import { useCallback, useEffect, useState, useTransition } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
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
import { CheckCircle2, Loader2, AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import { submitAdvancedGeneration } from "@/lib/actions";
import { supabase } from "@/lib/supabase";
import type { Entity, Relationship, Endpoint, Tier, Generation } from "@/lib/types";

const FIELD_TYPES = ["UUID", "String", "Integer", "Decimal", "Boolean", "Timestamp", "Text", "JSON"];
const REL_TYPES: Relationship["type"][] = ["Has Many", "Belongs To", "Has One", "Many To Many"];
const HTTP_METHODS: Endpoint["method"][] = ["GET", "POST", "PUT", "DELETE", "PATCH"];

// ─── Step 1: Entities ─────────────────────────────────────────────────────────
function StepEntities({ entities, setEntities, relationships, setRelationships }: {
  entities: Entity[];
  setEntities: React.Dispatch<React.SetStateAction<Entity[]>>;
  relationships: Relationship[];
  setRelationships: React.Dispatch<React.SetStateAction<Relationship[]>>;
}) {
  const entityNames = entities.map((e) => e.name).filter(Boolean);
  return (
    <div className="space-y-5">
      {entities.map((entity, eidx) => (
        <div key={eidx} className="rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 space-y-3">
          <div className="flex items-center gap-3">
            <label className="font-mono text-xs text-slate-500 uppercase tracking-widest w-20 shrink-0">Entity</label>
            <input value={entity.name} onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, name: e.target.value } : x))}
              placeholder="EntityName"
              className="flex-1 bg-slate-800/60 border border-slate-600/40 rounded-lg px-3 py-1.5 font-mono text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
            />
            <button onClick={() => setEntities((p) => p.filter((_, i) => i !== eidx))} className="font-mono text-xs text-rose-400/60 hover:text-rose-400 px-2 transition-colors">✕</button>
          </div>
          <div className="pl-4 border-l border-slate-600/30 space-y-1.5">
            {entity.fields.map((field, fidx) => (
              <div key={fidx} className="flex items-center gap-2 flex-wrap">
                <input value={field.name} onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, name: e.target.value } : f) } : x))}
                  placeholder="field_name"
                  className="w-28 bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60"
                />
                <select value={field.type} onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, type: e.target.value } : f) } : x))}
                  className="bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-blue-500/60"
                >
                  {FIELD_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                </select>
                <label className="flex items-center gap-1 cursor-pointer">
                  <input type="checkbox" checked={field.pk} onChange={(e) => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.map((f, j) => j === fidx ? { ...f, pk: e.target.checked } : f) } : x))} className="accent-blue-500" />
                  <span className="font-mono text-[10px] text-slate-500">PK</span>
                </label>
                <button onClick={() => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.filter((_, j) => j !== fidx) } : x))} className="font-mono text-[10px] text-rose-400/50 hover:text-rose-400 px-1">✕</button>
              </div>
            ))}
            <button onClick={() => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: [...x.fields, { name: "", type: "String", pk: false }] } : x))}
              className="font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors tracking-widest uppercase">+ Add Field</button>
          </div>
        </div>
      ))}
      <button onClick={() => setEntities((p) => [...p, { name: "", fields: [{ name: "id", type: "UUID", pk: true }] }])}
        className="w-full font-mono text-xs border border-blue-500/30 text-blue-400 hover:bg-blue-500/10 px-4 py-2.5 rounded-xl uppercase tracking-widest transition-colors">
        + Add Entity
      </button>
      <div className="pt-3 border-t border-slate-700/50 space-y-3">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">Relationships</p>
        {relationships.map((rel, idx) => (
          <div key={idx} className="flex items-center gap-2 flex-wrap">
            <select value={rel.from} onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, from: e.target.value } : r))}
              className="bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-blue-500/60">
              <option value="">From...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
            <select value={rel.type} onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, type: e.target.value as Relationship["type"] } : r))}
              className="bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-blue-500/60">
              {REL_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
            <select value={rel.to} onChange={(e) => setRelationships((p) => p.map((r, i) => i === idx ? { ...r, to: e.target.value } : r))}
              className="bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-blue-500/60">
              <option value="">To...</option>
              {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
            <button onClick={() => setRelationships((p) => p.filter((_, i) => i !== idx))} className="font-mono text-[10px] text-rose-400/50 hover:text-rose-400 px-1">✕</button>
          </div>
        ))}
        <button onClick={() => setRelationships((p) => [...p, { from: "", type: "Has Many", to: "" }])}
          className="font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors tracking-widest uppercase">+ Add Relationship</button>
      </div>
    </div>
  );
}

// ─── Step 2: Endpoints ────────────────────────────────────────────────────────
function StepEndpoints({ endpoints, setEndpoints, entityNames }: {
  endpoints: Endpoint[];
  setEndpoints: React.Dispatch<React.SetStateAction<Endpoint[]>>;
  entityNames: string[];
}) {
  return (
    <div className="space-y-4">
      <p className="font-mono text-xs text-slate-500 tracking-widest uppercase">Define API endpoints for your entities</p>
      {endpoints.map((ep, idx) => (
        <div key={idx} className="flex items-center gap-2 flex-wrap rounded-xl border border-slate-600/30 bg-slate-700/20 p-3">
          <select value={ep.method} onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, method: e.target.value as Endpoint["method"] } : x))}
            className={cn("bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] font-bold focus:outline-none focus:border-blue-500/60 w-20",
              ep.method === "GET" && "text-emerald-400",
              ep.method === "POST" && "text-blue-400",
              ep.method === "PUT" && "text-yellow-400",
              (ep.method === "DELETE" || ep.method === "PATCH") && "text-rose-400"
            )}>
            {HTTP_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
          <input value={ep.path} onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, path: e.target.value } : x))}
            placeholder="/api/v1/resource"
            className="flex-1 bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white placeholder:text-slate-600 focus:outline-none focus:border-blue-500/60 min-w-[140px]"
          />
          <select value={ep.entity} onChange={(e) => setEndpoints((p) => p.map((x, i) => i === idx ? { ...x, entity: e.target.value } : x))}
            className="bg-slate-800/60 border border-slate-600/40 rounded-lg px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-blue-500/60">
            <option value="">Entity...</option>
            {entityNames.map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
          <button onClick={() => setEndpoints((p) => p.filter((_, i) => i !== idx))} className="font-mono text-[10px] text-rose-400/50 hover:text-rose-400 px-1">✕</button>
        </div>
      ))}
      <button onClick={() => setEndpoints((p) => [...p, { method: "GET", path: "", entity: "" }])}
        className="w-full font-mono text-xs border border-blue-500/30 text-blue-400 hover:bg-blue-500/10 px-4 py-2.5 rounded-xl uppercase tracking-widest transition-colors">
        + Add Endpoint
      </button>
    </div>
  );
}

// ─── Step 3: Tier ─────────────────────────────────────────────────────────────
function StepTier({ selectedTier, setSelectedTier }: { selectedTier: Tier; setSelectedTier: (t: Tier) => void }) {
  const tiers: { id: Tier; name: string; price: string; items: string[]; recommended?: boolean }[] = [
    { id: 1, name: "BLUEPRINT", price: "$299", items: ["Schema JSON", "API Specifications", "SQL Scripts"] },
    { id: 2, name: "BOILERPLATE", price: "$599", items: ["Blueprint features", "Full Source Code", "Compile Guarantee"], recommended: true },
    { id: 3, name: "INFRASTRUCTURE", price: "$999", items: ["Boilerplate features", "AWS CDK Stack", "Helm Charts", "Deployment Runbook"] },
  ];
  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
      {tiers.map((t) => (
        <button key={t.id} onClick={() => setSelectedTier(t.id)}
          className={cn("relative rounded-xl border p-5 text-left space-y-3 transition-all duration-300",
            selectedTier === t.id
              ? "border-blue-500/60 bg-blue-500/10 shadow-[0_0_20px_rgba(59,130,246,0.15)]"
              : "border-slate-600/30 bg-slate-700/20 hover:border-blue-500/30"
          )}>
          {t.recommended && (
            <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-blue-500 px-2 py-0.5 text-[10px] font-medium text-white whitespace-nowrap">
              Recommended
            </div>
          )}
          <div>
            <h3 className="font-mono text-xs font-bold tracking-widest text-white uppercase">{t.name}</h3>
            <p className="font-mono text-xl font-bold text-blue-400 mt-1">{t.price}</p>
          </div>
          <ul className="space-y-1">
            {t.items.map((item) => (
              <li key={item} className="font-mono text-[11px] text-slate-400 flex items-center gap-2">
                <span className="text-emerald-400">›</span> {item}
              </li>
            ))}
          </ul>
          {selectedTier === t.id && (
            <div className="flex items-center gap-1.5">
              <CheckCircle2 className="h-3.5 w-3.5 text-blue-400" />
              <span className="font-mono text-[10px] text-blue-400">Selected</span>
            </div>
          )}
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
        <div className="bg-slate-700 border border-blue-500/60 min-w-[150px] text-left rounded-lg overflow-hidden">
          <div className="bg-blue-500/10 border-b border-blue-500/60 px-3 py-1.5">
            <span className="font-mono text-xs font-bold text-blue-400 tracking-widest uppercase">{e.name}</span>
          </div>
          <div className="px-3 py-2 space-y-0.5">
            {e.fields.map((f) => (
              <div key={f.name} className="flex items-center gap-2">
                {f.pk && <span className="font-mono text-[9px] text-yellow-400 border border-yellow-400/40 px-0.5 rounded">PK</span>}
                <span className="font-mono text-[11px] text-white">{f.name}</span>
                <span className="font-mono text-[10px] text-slate-400">{f.type}</span>
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
    style: { stroke: "#3B82F6" },
    labelStyle: { fill: "#94a3b8", fontFamily: "monospace", fontSize: 10 },
    labelBgStyle: { fill: "#334155" },
  }));
}

// ─── Main Wizard ──────────────────────────────────────────────────────────────
const STEPS = ["Define Entities", "Configure API", "Select Tier & Pay"];

export default function AdvancedModePage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const initialStep = Number(searchParams.get("step") ?? "1");
  const initialTier = Number(searchParams.get("tier") ?? "2") as Tier;

  const [step, setStep] = useState(Math.min(Math.max(initialStep, 1), 3));
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
  const [selectedTier, setSelectedTier] = useState<Tier>(initialTier);
  const [submitPhase, setSubmitPhase] = useState<"idle" | "submitting" | "submitted" | "error">("idle");
  const [generationId, setGenerationId] = useState<string | null>(null);
  const [liveStatus, setLiveStatus] = useState<Generation["status"] | null>(null);
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
    const channel = client
      .channel(`gen-adv:${generationId}`)
      .on("postgres_changes",
        { event: "UPDATE", schema: "public", table: "generations", filter: `id=eq.${generationId}` },
        (payload) => {
          const updated = payload.new as Generation;
          setLiveStatus(updated.status);
          if (updated.status === "success" && updated.download_url) {
            router.push(`/generate/${generationId}`);
          } else if (updated.status === "failed") {
            setErrorMsg(updated.error_message ?? "Generation failed.");
            setSubmitPhase("error");
          }
        }
      ).subscribe();
    return () => { client.removeChannel(channel); };
  }, [generationId, router]);

  function handleCheckout() {
    startTransition(async () => {
      setSubmitPhase("submitting");
      const result = await submitAdvancedGeneration(
        { entities, relationships, endpoints },
        selectedTier
      );
      if (result.success) {
        setGenerationId(result.generationId);
        setSubmitPhase("submitted");
      } else {
        setErrorMsg(result.error);
        setSubmitPhase("error");
      }
    });
  }

  if (submitPhase === "submitting" || submitPhase === "submitted") {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-slate-800 px-4">
        <div className="w-full max-w-md text-center space-y-6">
          <div className="h-16 w-16 rounded-full bg-blue-500/10 border border-blue-500/30 flex items-center justify-center mx-auto">
            <Loader2 className="h-8 w-8 text-blue-400 animate-spin" />
          </div>
          <h2 className="text-xl font-bold text-white">Synthesizing Your Platform</h2>
          <p className="text-slate-400 text-sm">{liveStatus ? `Status: ${liveStatus}` : "Queued — starting shortly..."}</p>
          {generationId && (
            <div className="rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 text-left">
              <p className="font-mono text-xs text-slate-500 uppercase mb-1">Generation ID</p>
              <p className="font-mono text-xs text-blue-400 break-all">{generationId}</p>
            </div>
          )}
          <div className="w-full bg-slate-700 rounded-full h-1"><div className="h-full bg-blue-500 animate-pulse rounded-full w-3/5" /></div>
        </div>
      </div>
    );
  }

  if (submitPhase === "error") {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-slate-800 px-4">
        <div className="w-full max-w-md text-center space-y-4">
          <div className="h-16 w-16 rounded-full bg-rose-500/10 border border-rose-500/30 flex items-center justify-center mx-auto">
            <AlertCircle className="h-8 w-8 text-rose-400" />
          </div>
          <h2 className="text-xl font-bold text-white">Something went wrong</h2>
          <p className="text-slate-400 text-sm">{errorMsg}</p>
          <div className="flex gap-3 justify-center">
            <button onClick={() => { setSubmitPhase("idle"); setErrorMsg(null); }}
              className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-5 py-2.5 rounded-full uppercase tracking-widest transition-colors">
              &larr; Back
            </button>
            <Link href="/" className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-5 py-2.5 rounded-full uppercase tracking-widest transition-colors">
              Start Over
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="h-screen flex flex-col bg-slate-800 overflow-hidden">
      {/* Header */}
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50 shrink-0">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4 overflow-x-auto">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80 shrink-0">
            <Image src="/logo.svg" alt="Stack Alchemist" width={28} height={28} className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]" />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-blue-400">AL</span>CHEMIST
            </span>
          </Link>
          <span className="text-slate-600 font-mono text-xs shrink-0">|</span>
          <div className="flex items-center gap-1 overflow-x-auto">
            {STEPS.map((s, i) => (
              <div key={s} className="flex items-center gap-1 shrink-0">
                {i > 0 && <span className="font-mono text-xs text-slate-600 mx-1">&rarr;</span>}
                <button onClick={() => setStep(i + 1)}
                  className={cn("font-mono text-xs tracking-widest uppercase transition-colors px-2 py-0.5 whitespace-nowrap",
                    step === i + 1 ? "text-blue-400 border-b border-blue-400" : step > i + 1 ? "text-emerald-400" : "text-slate-500"
                  )}>
                  {i + 1}. {s}
                </button>
              </div>
            ))}
          </div>
        </div>
      </header>

      <main className="flex flex-1 min-h-0">
        {/* Left panel */}
        <div className="w-full lg:w-1/2 border-r border-slate-600/30 overflow-y-auto p-5">
          {step === 1 && <StepEntities entities={entities} setEntities={setEntities} relationships={relationships} setRelationships={setRelationships} />}
          {step === 2 && <StepEndpoints endpoints={endpoints} setEndpoints={setEndpoints} entityNames={entities.map((e) => e.name).filter(Boolean)} />}
          {step === 3 && <StepTier selectedTier={selectedTier} setSelectedTier={setSelectedTier} />}
        </div>

        {/* Right panel: Live preview (hidden on mobile) */}
        <div className="hidden lg:flex flex-col w-1/2">
          <div className="border-b border-slate-600/30 px-4 py-2 bg-slate-800/60">
            <p className="font-mono text-xs text-slate-500 tracking-widest uppercase">Live Preview</p>
          </div>
          <div className="flex-1" style={{ minHeight: "400px" }}>
            <ReactFlow
              nodes={entitiesToNodes(entities)} edges={relsToEdges(relationships)}
              onNodesChange={onNodesChange} onEdgesChange={onEdgesChange}
              onConnect={onConnect} fitView colorMode="dark"
              style={{ background: "#1e293b" }}
            >
              <Background variant={BackgroundVariant.Dots} gap={24} size={1} color="#334155" />
              <Controls style={{ background: "#334155", border: "1px solid #475569", borderRadius: "8px" }} />
            </ReactFlow>
          </div>
        </div>
      </main>

      {/* Footer nav */}
      <footer className="border-t border-slate-600/30 px-5 py-3 shrink-0">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          {step > 1 ? (
            <button onClick={() => setStep((s) => Math.max(1, s - 1))}
              className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors">
              &larr; Previous
            </button>
          ) : (
            <Link href="/" className="font-mono text-xs text-slate-400 hover:text-blue-400 tracking-widest uppercase transition-colors">
              &larr; Back
            </Link>
          )}
          {step < 3 ? (
            <button onClick={() => setStep((s) => Math.min(3, s + 1))}
              className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors">
              Next &rarr;
            </button>
          ) : (
            <button onClick={handleCheckout} disabled={isPending}
              className="font-mono text-xs bg-emerald-500 hover:bg-emerald-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors disabled:opacity-60 flex items-center gap-2">
              {isPending ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Processing...</> : "Proceed to Checkout →"}
            </button>
          )}
        </div>
      </footer>
    </div>
  );
}
