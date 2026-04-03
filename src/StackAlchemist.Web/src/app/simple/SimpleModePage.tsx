"use client";

import { useCallback, useEffect, useMemo, useState, useTransition } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  addEdge,
  useNodesState,
  useEdgesState,
  type Node,
  type Edge,
  type Connection,
  BackgroundVariant,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { CheckCircle2, AlertCircle, Loader2, Plus, Trash2, X } from "lucide-react";
import { submitSimpleGeneration } from "@/lib/actions";
import { supabase } from "@/lib/supabase";
import type { Generation, Tier } from "@/lib/types";

// ─── Schema Data Model ───────────────────────────────────────────────────────
interface SchemaField {
  name: string;
  type: string;
  pk?: boolean;
}

interface SchemaEntity {
  id: string;
  name: string;
  fields: SchemaField[];
}

interface SchemaRelation {
  sourceId: string;
  targetId: string;
  label: string;
}

const FIELD_TYPES = ["UUID", "String", "Int", "Decimal", "Boolean", "Timestamp", "Text", "JSON"];

const INITIAL_ENTITIES: SchemaEntity[] = [
  { id: "user", name: "User", fields: [{ name: "id", type: "UUID", pk: true }, { name: "email", type: "String" }, { name: "name", type: "String" }, { name: "created_at", type: "Timestamp" }] },
  { id: "plan", name: "Plan", fields: [{ name: "id", type: "UUID", pk: true }, { name: "name", type: "String" }, { name: "price", type: "Decimal" }] },
  { id: "subscription", name: "Subscription", fields: [{ name: "id", type: "UUID", pk: true }, { name: "user_id", type: "UUID" }, { name: "plan_id", type: "UUID" }, { name: "status", type: "String" }, { name: "started_at", type: "Timestamp" }] },
  { id: "checkin", name: "CheckIn", fields: [{ name: "id", type: "UUID", pk: true }, { name: "user_id", type: "UUID" }, { name: "checked_at", type: "Timestamp" }] },
];

const INITIAL_RELATIONS: SchemaRelation[] = [
  { sourceId: "user", targetId: "subscription", label: "1:M" },
  { sourceId: "plan", targetId: "subscription", label: "1:M" },
  { sourceId: "user", targetId: "checkin", label: "1:M" },
];

const ENTITY_POSITIONS: Record<string, { x: number; y: number }> = {
  user: { x: 80, y: 160 },
  plan: { x: 400, y: 60 },
  subscription: { x: 400, y: 280 },
  checkin: { x: 700, y: 200 },
};

function buildNodes(entities: SchemaEntity[]): Node[] {
  let nextX = 80;
  return entities.map((e, i) => {
    const pos = ENTITY_POSITIONS[e.id] ?? { x: nextX + i * 320, y: 160 };
    return {
      id: e.id,
      position: pos,
      data: { label: <EntityCard name={e.name} fields={e.fields} /> },
      type: "default",
      style: { background: "transparent", border: "none", padding: 0 },
    };
  });
}

function buildEdges(relations: SchemaRelation[]): Edge[] {
  return relations.map((r) => ({
    id: `${r.sourceId}-${r.targetId}`,
    source: r.sourceId,
    target: r.targetId,
    label: r.label,
    style: { stroke: "#3B82F6" },
    labelStyle: { fill: "#94a3b8", fontFamily: "monospace", fontSize: 10 },
    labelBgStyle: { fill: "#334155" },
  }));
}

// ─── Entity Card Node ─────────────────────────────────────────────────────────
function EntityCard({ name, fields }: { name: string; fields: { name: string; type: string; pk?: boolean }[] }) {
  return (
    <div className="bg-slate-700 border border-blue-500/60 min-w-[160px] text-left rounded-lg overflow-hidden">
      <div className="bg-blue-500/10 border-b border-blue-500/60 px-3 py-1.5">
        <span className="font-mono text-xs font-bold text-blue-400 tracking-widest uppercase">{name}</span>
      </div>
      <div className="px-3 py-2 space-y-0.5">
        {fields.map((f) => (
          <div key={f.name} className="flex items-center gap-2">
            {f.pk && (
              <span className="font-mono text-[9px] text-yellow-400 border border-yellow-400/40 px-0.5 rounded">PK</span>
            )}
            <span className="font-mono text-[11px] text-white">{f.name}</span>
            <span className="font-mono text-[10px] text-slate-400">{f.type}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Schema Editor Modal ──────────────────────────────────────────────────────
function SchemaEditorModal({
  entities,
  relations,
  onSave,
  onClose,
}: {
  entities: SchemaEntity[];
  relations: SchemaRelation[];
  onSave: (entities: SchemaEntity[], relations: SchemaRelation[]) => void;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState<SchemaEntity[]>(() => JSON.parse(JSON.stringify(entities)));
  const [draftRels, setDraftRels] = useState<SchemaRelation[]>(() => JSON.parse(JSON.stringify(relations)));

  function updateEntity(idx: number, patch: Partial<SchemaEntity>) {
    setDraft((d) => d.map((e, i) => (i === idx ? { ...e, ...patch } : e)));
  }

  function updateField(entityIdx: number, fieldIdx: number, patch: Partial<SchemaField>) {
    setDraft((d) =>
      d.map((e, i) =>
        i === entityIdx
          ? { ...e, fields: e.fields.map((f, j) => (j === fieldIdx ? { ...f, ...patch } : f)) }
          : e
      )
    );
  }

  function addField(entityIdx: number) {
    setDraft((d) =>
      d.map((e, i) =>
        i === entityIdx ? { ...e, fields: [...e.fields, { name: "new_field", type: "String" }] } : e
      )
    );
  }

  function removeField(entityIdx: number, fieldIdx: number) {
    setDraft((d) =>
      d.map((e, i) =>
        i === entityIdx ? { ...e, fields: e.fields.filter((_, j) => j !== fieldIdx) } : e
      )
    );
  }

  function addEntity() {
    const id = `entity_${Date.now()}`;
    setDraft((d) => [...d, { id, name: "NewEntity", fields: [{ name: "id", type: "UUID", pk: true }] }]);
  }

  function removeEntity(idx: number) {
    const removed = draft[idx];
    setDraft((d) => d.filter((_, i) => i !== idx));
    setDraftRels((r) => r.filter((rel) => rel.sourceId !== removed.id && rel.targetId !== removed.id));
  }

  function addRelation() {
    if (draft.length < 2) return;
    setDraftRels((r) => [...r, { sourceId: draft[0].id, targetId: draft[1].id, label: "1:M" }]);
  }

  function updateRelation(idx: number, patch: Partial<SchemaRelation>) {
    setDraftRels((r) => r.map((rel, i) => (i === idx ? { ...rel, ...patch } : rel)));
  }

  function removeRelation(idx: number) {
    setDraftRels((r) => r.filter((_, i) => i !== idx));
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-start justify-center bg-black/60 backdrop-blur-sm overflow-y-auto py-8">
      <div className="w-full max-w-3xl bg-slate-800 border border-slate-600/50 rounded-2xl shadow-2xl mx-4">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-700/50 px-6 py-4">
          <h2 className="font-mono text-sm font-bold text-white tracking-widest uppercase">Edit Schema</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="px-6 py-4 space-y-6 max-h-[70vh] overflow-y-auto">
          {/* Entities */}
          {draft.map((entity, ei) => (
            <div key={entity.id} className="rounded-xl border border-slate-600/40 bg-slate-700/30 overflow-hidden">
              <div className="flex items-center gap-3 px-4 py-3 bg-slate-700/50 border-b border-slate-600/30">
                <input
                  value={entity.name}
                  onChange={(e) => {
                    const newName = e.target.value;
                    const newId = newName.toLowerCase().replace(/[^a-z0-9]/g, "_");
                    const oldId = entity.id;
                    updateEntity(ei, { name: newName, id: newId });
                    setDraftRels((r) =>
                      r.map((rel) => ({
                        ...rel,
                        sourceId: rel.sourceId === oldId ? newId : rel.sourceId,
                        targetId: rel.targetId === oldId ? newId : rel.targetId,
                      }))
                    );
                  }}
                  className="flex-1 bg-transparent font-mono text-sm font-bold text-blue-400 tracking-widest uppercase outline-none border-b border-transparent focus:border-blue-500/50"
                />
                <button onClick={() => removeEntity(ei)} className="text-slate-500 hover:text-rose-400 transition-colors" title="Remove entity">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
              <div className="px-4 py-3 space-y-2">
                {entity.fields.map((field, fi) => (
                  <div key={fi} className="flex items-center gap-2">
                    <label className="flex items-center gap-1 shrink-0">
                      <input
                        type="checkbox"
                        checked={!!field.pk}
                        onChange={(e) => updateField(ei, fi, { pk: e.target.checked })}
                        className="accent-yellow-400 h-3 w-3"
                      />
                      <span className="font-mono text-[9px] text-yellow-400">PK</span>
                    </label>
                    <input
                      value={field.name}
                      onChange={(e) => updateField(ei, fi, { name: e.target.value })}
                      className="flex-1 bg-slate-600/30 font-mono text-xs text-white px-2 py-1 rounded border border-slate-600/30 focus:border-blue-500/50 outline-none"
                    />
                    <select
                      value={field.type}
                      onChange={(e) => updateField(ei, fi, { type: e.target.value })}
                      className="bg-slate-600/30 font-mono text-xs text-slate-300 px-2 py-1 rounded border border-slate-600/30 focus:border-blue-500/50 outline-none"
                    >
                      {FIELD_TYPES.map((t) => (
                        <option key={t} value={t}>{t}</option>
                      ))}
                    </select>
                    <button onClick={() => removeField(ei, fi)} className="text-slate-500 hover:text-rose-400 transition-colors" title="Remove field">
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ))}
                <button
                  onClick={() => addField(ei)}
                  className="flex items-center gap-1.5 font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors mt-1"
                >
                  <Plus className="h-3 w-3" /> Add Field
                </button>
              </div>
            </div>
          ))}

          <button
            onClick={addEntity}
            className="w-full flex items-center justify-center gap-2 font-mono text-xs text-blue-400 hover:text-blue-300 border border-dashed border-blue-500/30 hover:border-blue-500/50 rounded-xl py-3 transition-colors"
          >
            <Plus className="h-4 w-4" /> Add Entity
          </button>

          {/* Relations */}
          <div className="space-y-3">
            <h3 className="font-mono text-xs text-slate-400 tracking-widest uppercase">Relationships</h3>
            {draftRels.map((rel, ri) => (
              <div key={ri} className="flex items-center gap-2">
                <select
                  value={rel.sourceId}
                  onChange={(e) => updateRelation(ri, { sourceId: e.target.value })}
                  className="flex-1 bg-slate-600/30 font-mono text-xs text-slate-300 px-2 py-1.5 rounded border border-slate-600/30 outline-none"
                >
                  {draft.map((ent) => (
                    <option key={ent.id} value={ent.id}>{ent.name}</option>
                  ))}
                </select>
                <select
                  value={rel.label}
                  onChange={(e) => updateRelation(ri, { label: e.target.value })}
                  className="bg-slate-600/30 font-mono text-xs text-slate-300 px-2 py-1.5 rounded border border-slate-600/30 outline-none"
                >
                  <option value="1:1">1:1</option>
                  <option value="1:M">1:M</option>
                  <option value="M:M">M:M</option>
                </select>
                <select
                  value={rel.targetId}
                  onChange={(e) => updateRelation(ri, { targetId: e.target.value })}
                  className="flex-1 bg-slate-600/30 font-mono text-xs text-slate-300 px-2 py-1.5 rounded border border-slate-600/30 outline-none"
                >
                  {draft.map((ent) => (
                    <option key={ent.id} value={ent.id}>{ent.name}</option>
                  ))}
                </select>
                <button onClick={() => removeRelation(ri)} className="text-slate-500 hover:text-rose-400 transition-colors">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            ))}
            <button
              onClick={addRelation}
              disabled={draft.length < 2}
              className="flex items-center gap-1.5 font-mono text-[10px] text-blue-400 hover:text-blue-300 transition-colors disabled:opacity-40"
            >
              <Plus className="h-3 w-3" /> Add Relationship
            </button>
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 border-t border-slate-700/50 px-6 py-4">
          <button
            onClick={onClose}
            className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-5 py-2 rounded-full uppercase tracking-widest transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={() => onSave(draft, draftRels)}
            className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-5 py-2 rounded-full uppercase tracking-widest transition-colors"
          >
            Apply Changes
          </button>
        </div>
      </div>
    </div>
  );
}

const GEN_STEPS = [
  "Parsing natural language prompt...",
  "Identifying domain entities...",
  "Mapping relationships between entities...",
  "Designing API surface area...",
  "Validating schema consistency...",
  "Generating entity-relationship canvas...",
];

// ─── Tier Selector ─────────────────────────────────────────────────────────────
const TIERS: { id: Tier; name: string; price: string; tagline: string }[] = [
  { id: 1, name: "Blueprint", price: "$299", tagline: "Schema + API docs only" },
  { id: 2, name: "Boilerplate", price: "$599", tagline: "Full source code + Compile Guarantee" },
  { id: 3, name: "Infrastructure", price: "$999", tagline: "Everything + IaC + Runbook" },
];

export default function SimpleModePage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const prompt = searchParams.get("q") ?? "";

  // ─── UI State ───────────────────────────────────────────────────────────────
  const [phase, setPhase] = useState<"generating" | "canvas" | "tier" | "submitting" | "submitted" | "error">("generating");
  const [progress, setProgress] = useState(0);
  const [logLines, setLogLines] = useState<string[]>([]);
  const [selectedTier, setSelectedTier] = useState<Tier>(2);
  const [generationId, setGenerationId] = useState<string | null>(null);
  const [liveStatus, setLiveStatus] = useState<Generation["status"] | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const [showSchemaEditor, setShowSchemaEditor] = useState(false);

  // ─── Schema State ──────────────────────────────────────────────────────────
  const [schemaEntities, setSchemaEntities] = useState<SchemaEntity[]>(INITIAL_ENTITIES);
  const [schemaRelations, setSchemaRelations] = useState<SchemaRelation[]>(INITIAL_RELATIONS);

  // ─── ReactFlow State (derived from schema) ─────────────────────────────────
  const derivedNodes = useMemo(() => buildNodes(schemaEntities), [schemaEntities]);
  const derivedEdges = useMemo(() => buildEdges(schemaRelations), [schemaRelations]);
  const [nodes, setNodes, onNodesChange] = useNodesState(derivedNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(derivedEdges);
  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

  // Sync ReactFlow when schema changes
  useEffect(() => { setNodes(derivedNodes); }, [derivedNodes, setNodes]);
  useEffect(() => { setEdges(derivedEdges); }, [derivedEdges, setEdges]);

  function handleSchemaEditorSave(entities: SchemaEntity[], relations: SchemaRelation[]) {
    setSchemaEntities(entities);
    setSchemaRelations(relations);
    setShowSchemaEditor(false);
  }

  // ─── Simulate parsing progress ─────────────────────────────────────────────
  useEffect(() => {
    if (phase !== "generating") return;
    let step = 0;
    const interval = setInterval(() => {
      if (step < GEN_STEPS.length) {
        setLogLines((l) => [...l, GEN_STEPS[step]]);
        setProgress(Math.round(((step + 1) / GEN_STEPS.length) * 100));
        step++;
      } else {
        clearInterval(interval);
        setTimeout(() => setPhase("canvas"), 400);
      }
    }, 450);
    return () => clearInterval(interval);
  }, [phase]);

  // ─── Supabase real-time subscription once generation is submitted ───────────
  useEffect(() => {
    if (!generationId || !supabase) return;
    const client = supabase;

    const channel = client
      .channel(`generation:${generationId}`)
      .on(
        "postgres_changes",
        { event: "UPDATE", schema: "public", table: "generations", filter: `id=eq.${generationId}` },
        (payload) => {
          const updated = payload.new as Generation;
          setLiveStatus(updated.status);
          if (updated.status === "success") {
            if (updated.download_url) {
              router.push(`/generate/${generationId}`);
            }
          } else if (updated.status === "failed") {
            setErrorMsg(updated.error_message ?? "Generation failed. Please try again.");
            setPhase("error");
          }
        }
      )
      .subscribe();

    return () => {
      client.removeChannel(channel);
    };
  }, [generationId, router]);

  // ─── Submit to Supabase + Engine ────────────────────────────────────────────
  function handleProceed() {
    startTransition(async () => {
      setPhase("submitting");
      const result = await submitSimpleGeneration(prompt, selectedTier);
      if (result.success) {
        setGenerationId(result.generationId);
        setPhase("submitted");
      } else {
        setErrorMsg(result.error);
        setPhase("error");
      }
    });
  }

  // ─── Status label ───────────────────────────────────────────────────────────
  function statusLabel(s: Generation["status"] | null) {
    switch (s) {
      case "pending": return "Queued — waiting to start...";
      case "extracting_schema": return "Extracting schema from prompt...";
      case "generating_code": return "Synthesizing code with Claude 3.5 Sonnet...";
      case "building": return "Running dotnet build (Compile Guarantee)...";
      case "success": return "Build verified! Preparing download...";
      case "failed": return "Build failed — triggering auto-correction...";
      default: return "Processing...";
    }
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-800">
      {/* Header */}
      <header className="border-b border-slate-600/30 bg-slate-800/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Link href="/" className="flex items-center gap-2.5 transition-opacity hover:opacity-80">
            <Image src="/logo.svg" alt="Stack Alchemist" width={28} height={28} className="drop-shadow-[0_0_6px_rgba(59,130,246,0.4)]" />
            <span className="font-mono text-sm font-medium tracking-widest text-slate-200 hidden sm:block">
              STACK <span className="text-blue-400">AL</span>CHEMIST
            </span>
          </Link>
          <span className="text-slate-600 font-mono text-xs">|</span>
          <Link href="/" className="font-mono text-xs text-slate-400 hover:text-blue-400 transition-colors tracking-widest uppercase">
            &larr; Back
          </Link>
        </div>
      </header>

      {/* Prompt banner */}
      <div className="border-b border-slate-700/50 bg-slate-700/20 px-4 py-3">
        <div className="max-w-6xl mx-auto">
          <p className="font-mono text-xs text-slate-500 tracking-widest uppercase mb-1">Prompt</p>
          <p className="font-mono text-sm text-white">{prompt || "No prompt provided."}</p>
        </div>
      </div>

      <main className="flex-1 flex flex-col">
        {/* ── Phase: Generating ─────────────────────────────────────────────── */}
        {phase === "generating" && (
          <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
            <div className="w-full max-w-xl space-y-2">
              <div className="flex items-center justify-between font-mono text-xs text-slate-400 uppercase tracking-widest">
                <span>Extracting Schema</span>
                <span className="text-blue-400">{progress}%</span>
              </div>
              <div className="h-1 bg-slate-700 w-full rounded-full overflow-hidden">
                <div
                  className="h-full bg-blue-500 transition-all duration-300 rounded-full"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>

            <div className="w-full max-w-xl rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 space-y-1.5">
              {logLines.map((line, i) => (
                <p key={i} className="font-mono text-xs text-emerald-400">
                  <span className="text-slate-500 mr-2">&rsaquo;</span>
                  {line}
                </p>
              ))}
              {progress < 100 && (
                <p className="font-mono text-xs text-blue-400 animate-pulse">
                  <span className="mr-2">&rsaquo;</span>_
                </p>
              )}
            </div>
          </div>
        )}

        {/* ── Phase: Canvas ─────────────────────────────────────────────────── */}
        {phase === "canvas" && (
          <div className="flex-1 flex flex-col">
            <div className="border-b border-slate-700/50 px-4 py-3 bg-slate-800">
              <div className="max-w-6xl mx-auto flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
                  <p className="font-mono text-xs text-emerald-400 tracking-widest uppercase">
                    Schema extracted — review and confirm before proceeding
                  </p>
                </div>
                <div className="flex gap-3 shrink-0">
                  <button
                    onClick={() => setShowSchemaEditor(true)}
                    className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors"
                  >
                    Edit Schema
                  </button>
                  <button
                    onClick={() => setPhase("tier")}
                    className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-4 py-1.5 rounded-full uppercase tracking-widest transition-colors"
                  >
                    Confirm &amp; Select Tier &rarr;
                  </button>
                </div>
              </div>
            </div>

            <div className="flex-1" style={{ minHeight: "500px" }}>
              <ReactFlow
                nodes={nodes} edges={edges}
                onNodesChange={onNodesChange} onEdgesChange={onEdgesChange}
                onConnect={onConnect}
                fitView colorMode="dark"
                style={{ background: "#1e293b" }}
              >
                <Background variant={BackgroundVariant.Dots} gap={24} size={1} color="#334155" />
                <Controls style={{ background: "#334155", border: "1px solid #475569", borderRadius: "8px" }} />
                <MiniMap style={{ background: "#334155", border: "1px solid #475569", borderRadius: "8px" }} nodeColor="#3B82F6" />
              </ReactFlow>
            </div>
          </div>
        )}

        {/* ── Phase: Tier Selection ─────────────────────────────────────────── */}
        {phase === "tier" && (
          <div className="flex-1 flex flex-col items-center justify-center px-4 py-16">
            <div className="w-full max-w-3xl space-y-6">
              <div className="text-center mb-8">
                <div className="flex items-center justify-center gap-3 mb-3">
                  <div className="h-px w-12 bg-gradient-to-r from-transparent via-blue-500/60 to-transparent" />
                  <span className="font-mono text-xs tracking-[0.3em] text-blue-400 uppercase">Step 2</span>
                  <div className="h-px w-12 bg-gradient-to-r from-blue-500/60 via-transparent to-transparent" />
                </div>
                <h2 className="text-2xl font-bold text-white mb-2">Choose Your Tier</h2>
                <p className="text-slate-400 text-sm">One-time payment. No subscriptions. You own the architecture forever.</p>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                {TIERS.map((t) => (
                  <button
                    key={t.id}
                    onClick={() => setSelectedTier(t.id)}
                    className={`relative rounded-xl border p-5 text-left space-y-2 transition-all duration-300 ${
                      selectedTier === t.id
                        ? "border-blue-500/60 bg-blue-500/10 shadow-[0_0_20px_rgba(59,130,246,0.15)]"
                        : "border-slate-600/30 bg-slate-700/20 hover:border-blue-500/30"
                    }`}
                  >
                    {t.id === 2 && (
                      <div className="absolute -top-2.5 left-1/2 -translate-x-1/2 rounded-full bg-blue-500 px-2 py-0.5 text-[10px] font-medium text-white whitespace-nowrap">
                        Recommended
                      </div>
                    )}
                    <div className="font-mono text-xs font-bold tracking-widest text-white uppercase">{t.name}</div>
                    <div className="text-xl font-bold text-blue-400">{t.price}</div>
                    <div className="text-xs text-slate-400">{t.tagline}</div>
                    {selectedTier === t.id && (
                      <div className="flex items-center gap-1.5 mt-1">
                        <CheckCircle2 className="h-3.5 w-3.5 text-blue-400" />
                        <span className="font-mono text-[10px] text-blue-400">Selected</span>
                      </div>
                    )}
                  </button>
                ))}
              </div>

              <div className="flex flex-col sm:flex-row gap-4 pt-4">
                <button
                  onClick={() => setPhase("canvas")}
                  className="flex-1 font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 py-3 rounded-full uppercase tracking-widest transition-colors"
                >
                  &larr; Review Schema
                </button>
                <button
                  onClick={handleProceed}
                  disabled={isPending}
                  className="flex-1 font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white py-3 rounded-full uppercase tracking-widest transition-colors disabled:opacity-60 flex items-center justify-center gap-2"
                >
                  {isPending ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Processing...</> : "Proceed to Checkout \u2192"}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ── Phase: Submitting / Submitted ─────────────────────────────────── */}
        {(phase === "submitting" || phase === "submitted") && (
          <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
            <div className="w-full max-w-xl space-y-6 text-center">
              <div className="flex items-center justify-center">
                <div className="h-16 w-16 rounded-full bg-blue-500/10 border border-blue-500/30 flex items-center justify-center">
                  <Loader2 className="h-8 w-8 text-blue-400 animate-spin" />
                </div>
              </div>
              <div>
                <h2 className="text-xl font-bold text-white mb-2">Synthesizing Your Platform</h2>
                <p className="text-slate-400 text-sm leading-relaxed">
                  {statusLabel(liveStatus)}
                </p>
              </div>

              {generationId && (
                <div className="rounded-xl border border-slate-600/30 bg-slate-700/20 p-4 text-left space-y-2">
                  <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">Generation ID</p>
                  <p className="font-mono text-xs text-blue-400 break-all">{generationId}</p>
                  <p className="font-mono text-xs text-slate-500">
                    Keep this page open — we&apos;ll redirect you when your download is ready.
                  </p>
                </div>
              )}

              <div className="w-full bg-slate-700 rounded-full h-1 overflow-hidden">
                <div className="h-full bg-blue-500 animate-pulse rounded-full" style={{ width: "60%" }} />
              </div>
            </div>
          </div>
        )}

        {/* ── Phase: Error ──────────────────────────────────────────────────── */}
        {phase === "error" && (
          <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-6">
            <div className="w-full max-w-md text-center space-y-4">
              <div className="flex items-center justify-center">
                <div className="h-16 w-16 rounded-full bg-rose-500/10 border border-rose-500/30 flex items-center justify-center">
                  <AlertCircle className="h-8 w-8 text-rose-400" />
                </div>
              </div>
              <h2 className="text-xl font-bold text-white">Something went wrong</h2>
              <p className="text-slate-400 text-sm leading-relaxed">{errorMsg ?? "An unexpected error occurred."}</p>
              <div className="flex flex-col sm:flex-row gap-3 justify-center">
                <button
                  onClick={() => { setPhase("tier"); setErrorMsg(null); }}
                  className="font-mono text-xs border border-slate-600/50 text-slate-400 hover:border-blue-500/40 hover:text-blue-400 px-5 py-2.5 rounded-full uppercase tracking-widest transition-colors"
                >
                  &larr; Try Again
                </button>
                <Link
                  href="/"
                  className="font-mono text-xs bg-blue-500 hover:bg-blue-400 text-white px-5 py-2.5 rounded-full uppercase tracking-widest transition-colors text-center"
                >
                  Start Over
                </Link>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Schema Editor Modal */}
      {showSchemaEditor && (
        <SchemaEditorModal
          entities={schemaEntities}
          relations={schemaRelations}
          onSave={handleSchemaEditorSave}
          onClose={() => setShowSchemaEditor(false)}
        />
      )}
    </div>
  );
}
