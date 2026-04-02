"use client";

import { useCallback, useState } from "react";
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
import { Logo } from "@/components/logo";
import { cn } from "@/lib/utils";

/* ---------- Types ---------- */
interface Field {
  name: string;
  type: string;
  pk: boolean;
}
interface Entity {
  name: string;
  fields: Field[];
}
interface Relationship {
  from: string;
  type: string;
  to: string;
}
interface Endpoint {
  method: string;
  path: string;
  entity: string;
}

const FIELD_TYPES = ["UUID", "String", "Integer", "Decimal", "Boolean", "Timestamp", "Text"];
const REL_TYPES = ["Has Many", "Belongs To", "Has One", "Many To Many"];
const HTTP_METHODS = ["GET", "POST", "PUT", "DELETE"];

/* ---------- Step 1: Define Entities ---------- */
function StepEntities({
  entities,
  setEntities,
  relationships,
  setRelationships,
}: {
  entities: Entity[];
  setEntities: React.Dispatch<React.SetStateAction<Entity[]>>;
  relationships: Relationship[];
  setRelationships: React.Dispatch<React.SetStateAction<Relationship[]>>;
}) {
  function addEntity() {
    setEntities((prev) => [
      ...prev,
      { name: "", fields: [{ name: "id", type: "UUID", pk: true }] },
    ]);
  }
  function removeEntity(idx: number) {
    setEntities((prev) => prev.filter((_, i) => i !== idx));
  }
  function updateEntityName(idx: number, name: string) {
    setEntities((prev) => prev.map((e, i) => (i === idx ? { ...e, name } : e)));
  }
  function addField(eidx: number) {
    setEntities((prev) =>
      prev.map((e, i) =>
        i === eidx
          ? { ...e, fields: [...e.fields, { name: "", type: "String", pk: false }] }
          : e
      )
    );
  }
  function removeField(eidx: number, fidx: number) {
    setEntities((prev) =>
      prev.map((e, i) =>
        i === eidx ? { ...e, fields: e.fields.filter((_, j) => j !== fidx) } : e
      )
    );
  }
  function updateField(eidx: number, fidx: number, patch: Partial<Field>) {
    setEntities((prev) =>
      prev.map((e, i) =>
        i === eidx
          ? { ...e, fields: e.fields.map((f, j) => (j === fidx ? { ...f, ...patch } : f)) }
          : e
      )
    );
  }
  function addRelationship() {
    setRelationships((prev) => [...prev, { from: "", type: "Has Many", to: "" }]);
  }
  function removeRelationship(idx: number) {
    setRelationships((prev) => prev.filter((_, i) => i !== idx));
  }
  function updateRelationship(idx: number, patch: Partial<Relationship>) {
    setRelationships((prev) => prev.map((r, i) => (i === idx ? { ...r, ...patch } : r)));
  }

  const entityNames = entities.map((e) => e.name).filter(Boolean);

  return (
    <div className="space-y-6">
      {entities.map((entity, eidx) => (
        <div key={eidx} className="border border-slate-border bg-slate-surface/40 p-4 space-y-3">
          <div className="flex items-center gap-3">
            <label className="font-mono text-xs text-slate-500 uppercase tracking-widest w-24 shrink-0">
              Entity
            </label>
            <input
              value={entity.name}
              onChange={(e) => updateEntityName(eidx, e.target.value)}
              placeholder="EntityName"
              className="flex-1 bg-void border border-slate-border px-3 py-1.5 font-mono text-xs text-white placeholder:text-slate-600 focus:outline-none focus:border-electric"
            />
            <button
              onClick={() => removeEntity(eidx)}
              className="font-mono text-xs text-rose hover:text-red-400 transition-colors px-2"
            >
              &#10005;
            </button>
          </div>

          <div className="pl-4 border-l border-slate-border space-y-1">
            {entity.fields.map((field, fidx) => (
              <div key={fidx} className="flex items-center gap-2">
                <input
                  value={field.name}
                  onChange={(e) => updateField(eidx, fidx, { name: e.target.value })}
                  placeholder="field_name"
                  className="w-32 bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white placeholder:text-slate-600 focus:outline-none focus:border-electric"
                />
                <select
                  value={field.type}
                  onChange={(e) => updateField(eidx, fidx, { type: e.target.value })}
                  className="bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-electric"
                >
                  {FIELD_TYPES.map((t) => (
                    <option key={t} value={t}>{t}</option>
                  ))}
                </select>
                <label className="flex items-center gap-1 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={field.pk}
                    onChange={(e) => updateField(eidx, fidx, { pk: e.target.checked })}
                    className="accent-blue-500"
                  />
                  <span className="font-mono text-[10px] text-slate-500">PK</span>
                </label>
                <button
                  onClick={() => removeField(eidx, fidx)}
                  className="font-mono text-[10px] text-rose/60 hover:text-red-400 px-1"
                >
                  &#10005;
                </button>
              </div>
            ))}
            <button
              onClick={() => addField(eidx)}
              className="font-mono text-[10px] text-electric hover:text-blue-400 transition-colors tracking-widest uppercase"
            >
              + Add Field
            </button>
          </div>
        </div>
      ))}

      <button
        onClick={addEntity}
        className="font-mono text-xs border border-electric/40 text-electric hover:bg-electric/10 px-4 py-2 uppercase tracking-widest transition-colors w-full"
      >
        + Add Entity
      </button>

      <div className="border-t border-slate-surface pt-4 space-y-3">
        <p className="font-mono text-xs text-slate-500 uppercase tracking-widest">
          Relationships
        </p>
        {relationships.map((rel, idx) => (
          <div key={idx} className="flex items-center gap-2">
            <select
              value={rel.from}
              onChange={(e) => updateRelationship(idx, { from: e.target.value })}
              className="bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-electric"
            >
              <option value="">From...</option>
              {entityNames.map((n) => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
            <select
              value={rel.type}
              onChange={(e) => updateRelationship(idx, { type: e.target.value })}
              className="bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-electric"
            >
              {REL_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
            <select
              value={rel.to}
              onChange={(e) => updateRelationship(idx, { to: e.target.value })}
              className="bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-electric"
            >
              <option value="">To...</option>
              {entityNames.map((n) => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
            <button
              onClick={() => removeRelationship(idx)}
              className="font-mono text-[10px] text-rose/60 hover:text-red-400 px-1"
            >
              &#10005;
            </button>
          </div>
        ))}
        <button
          onClick={addRelationship}
          className="font-mono text-[10px] text-electric hover:text-blue-400 transition-colors tracking-widest uppercase"
        >
          + Add Relationship
        </button>
      </div>
    </div>
  );
}

/* ---------- Step 2: Configure API Endpoints ---------- */
function StepEndpoints({
  endpoints,
  setEndpoints,
  entityNames,
}: {
  endpoints: Endpoint[];
  setEndpoints: React.Dispatch<React.SetStateAction<Endpoint[]>>;
  entityNames: string[];
}) {
  function addEndpoint() {
    setEndpoints((prev) => [...prev, { method: "GET", path: "", entity: "" }]);
  }
  function removeEndpoint(idx: number) {
    setEndpoints((prev) => prev.filter((_, i) => i !== idx));
  }
  function updateEndpoint(idx: number, patch: Partial<Endpoint>) {
    setEndpoints((prev) => prev.map((ep, i) => (i === idx ? { ...ep, ...patch } : ep)));
  }

  return (
    <div className="space-y-4">
      <p className="font-mono text-xs text-slate-500 tracking-widest uppercase">
        Define API endpoints for your entities
      </p>
      {endpoints.map((ep, idx) => (
        <div key={idx} className="flex items-center gap-2 border border-slate-border bg-slate-surface/40 p-3">
          <select
            value={ep.method}
            onChange={(e) => updateEndpoint(idx, { method: e.target.value })}
            className={cn(
              "bg-void border border-slate-border px-2 py-1 font-mono text-[11px] font-bold focus:outline-none focus:border-electric w-20",
              ep.method === "GET" && "text-emerald",
              ep.method === "POST" && "text-blue-400",
              ep.method === "PUT" && "text-yellow-400",
              ep.method === "DELETE" && "text-rose"
            )}
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </select>
          <input
            value={ep.path}
            onChange={(e) => updateEndpoint(idx, { path: e.target.value })}
            placeholder="/api/v1/resource"
            className="flex-1 bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white placeholder:text-slate-600 focus:outline-none focus:border-electric"
          />
          <select
            value={ep.entity}
            onChange={(e) => updateEndpoint(idx, { entity: e.target.value })}
            className="bg-void border border-slate-border px-2 py-1 font-mono text-[11px] text-white focus:outline-none focus:border-electric"
          >
            <option value="">Entity...</option>
            {entityNames.map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
          <button
            onClick={() => removeEndpoint(idx)}
            className="font-mono text-[10px] text-rose/60 hover:text-red-400 px-1"
          >
            &#10005;
          </button>
        </div>
      ))}
      <button
        onClick={addEndpoint}
        className="font-mono text-xs border border-electric/40 text-electric hover:bg-electric/10 px-4 py-2 uppercase tracking-widest transition-colors w-full"
      >
        + Add Endpoint
      </button>
    </div>
  );
}

/* ---------- Step 3: Select Tier ---------- */
function StepTier({
  selectedTier,
  setSelectedTier,
}: {
  selectedTier: number;
  setSelectedTier: React.Dispatch<React.SetStateAction<number>>;
}) {
  const tiers = [
    { id: 1, name: "BLUEPRINT", price: "$299", items: ["Schema JSON", "API Specifications", "SQL Scripts"] },
    { id: 2, name: "BOILERPLATE", price: "$599", items: ["Blueprint features", "Full Source Code", "Compile Guarantee"], recommended: true },
    { id: 3, name: "INFRASTRUCTURE", price: "$999", items: ["Boilerplate features", "AWS CDK Stack", "Helm Charts", "Deployment Runbook"] },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      {tiers.map((t) => (
        <button
          key={t.id}
          onClick={() => setSelectedTier(t.id)}
          className={cn(
            "border p-5 text-left space-y-3 transition-colors",
            selectedTier === t.id
              ? "border-electric bg-electric/5"
              : "border-slate-border hover:border-electric/40"
          )}
        >
          {"recommended" in t && t.recommended && (
            <span className="font-mono text-[10px] text-electric tracking-widest uppercase">
              &#9733; Recommended
            </span>
          )}
          <div>
            <h3 className="font-mono text-xs font-bold tracking-widest text-white uppercase">{t.name}</h3>
            <p className="font-mono text-xl font-bold text-electric mt-1">{t.price}</p>
          </div>
          <ul className="space-y-1">
            {t.items.map((item) => (
              <li key={item} className="font-mono text-[11px] text-slate-400 flex items-center gap-2">
                <span className="text-emerald">&#8250;</span> {item}
              </li>
            ))}
          </ul>
        </button>
      ))}
    </div>
  );
}

/* ---------- Entity -> React Flow nodes ---------- */
function entitiesToNodes(entities: Entity[]): Node[] {
  return entities
    .filter((e) => e.name)
    .map((e, i) => ({
      id: e.name,
      position: { x: (i % 3) * 280 + 40, y: Math.floor(i / 3) * 250 + 40 },
      data: {
        label: (
          <div className="bg-slate-surface border border-electric/60 min-w-[150px] text-left">
            <div className="bg-electric/10 border-b border-electric/60 px-3 py-1.5">
              <span className="font-mono text-xs font-bold text-blue-400 tracking-widest uppercase">
                {e.name}
              </span>
            </div>
            <div className="px-3 py-2 space-y-0.5">
              {e.fields.map((f) => (
                <div key={f.name} className="flex items-center gap-2">
                  {f.pk && (
                    <span className="font-mono text-[9px] text-yellow-400 border border-yellow-400/40 px-0.5">PK</span>
                  )}
                  <span className="font-mono text-[11px] text-white">{f.name}</span>
                  <span className="font-mono text-[10px] text-slate-500">{f.type}</span>
                </div>
              ))}
            </div>
          </div>
        ),
      },
      type: "default",
      style: { background: "transparent", border: "none", padding: 0 },
    }));
}

function relsToEdges(relationships: Relationship[]): Edge[] {
  return relationships
    .filter((r) => r.from && r.to)
    .map((r, i) => ({
      id: `rel-${i}`,
      source: r.from,
      target: r.to,
      label: r.type,
      style: { stroke: "#3B82F6" },
      labelStyle: { fill: "#94a3b8", fontFamily: "JetBrains Mono", fontSize: 10 },
      labelBgStyle: { fill: "#1E293B" },
    }));
}

/* ---------- Main Wizard ---------- */
const STEPS = ["Define Entities", "Configure API", "Select Tier & Pay"];

export default function AdvancedModePage() {
  const searchParams = useSearchParams();
  const initialStep = Number(searchParams.get("step") ?? "1");

  const [step, setStep] = useState(Math.min(Math.max(initialStep, 1), 3));
  const [entities, setEntities] = useState<Entity[]>([
    {
      name: "Product",
      fields: [
        { name: "id", type: "UUID", pk: true },
        { name: "name", type: "String", pk: false },
        { name: "price", type: "Decimal", pk: false },
      ],
    },
  ]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [endpoints, setEndpoints] = useState<Endpoint[]>([]);
  const [selectedTier, setSelectedTier] = useState(2);

  const [, , onNodesChange] = useNodesState<Node>([]);
  const [, setEdges, onEdgesChange] = useEdgesState<Edge>([]);

  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

  return (
    <div className="min-h-screen flex flex-col bg-void">
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Logo />
          <span className="text-slate-600 font-mono text-xs">|</span>
          <div className="flex items-center gap-1">
            {STEPS.map((s, i) => (
              <div key={s} className="flex items-center gap-1">
                {i > 0 && <span className="font-mono text-xs text-slate-600 mx-1">&rarr;</span>}
                <button
                  onClick={() => setStep(i + 1)}
                  className={cn(
                    "font-mono text-xs tracking-widest uppercase transition-colors px-2 py-0.5",
                    step === i + 1
                      ? "text-electric border-b border-electric"
                      : step > i + 1
                        ? "text-emerald"
                        : "text-slate-500"
                  )}
                >
                  {i + 1}. {s}
                </button>
              </div>
            ))}
          </div>
        </div>
      </header>

      <main className="flex-1 flex">
        <div className="w-1/2 border-r border-slate-surface overflow-y-auto p-6">
          {step === 1 && (
            <StepEntities
              entities={entities}
              setEntities={setEntities}
              relationships={relationships}
              setRelationships={setRelationships}
            />
          )}
          {step === 2 && (
            <StepEndpoints
              endpoints={endpoints}
              setEndpoints={setEndpoints}
              entityNames={entities.map((e) => e.name).filter(Boolean)}
            />
          )}
          {step === 3 && (
            <StepTier selectedTier={selectedTier} setSelectedTier={setSelectedTier} />
          )}
        </div>

        <div className="w-1/2 flex flex-col">
          <div className="border-b border-slate-surface px-4 py-2">
            <p className="font-mono text-xs text-slate-500 tracking-widest uppercase">
              Live Preview
            </p>
          </div>
          <div className="flex-1" style={{ minHeight: "400px" }}>
            <ReactFlow
              nodes={entitiesToNodes(entities)}
              edges={relsToEdges(relationships)}
              onNodesChange={onNodesChange}
              onEdgesChange={onEdgesChange}
              onConnect={onConnect}
              fitView
              colorMode="dark"
              style={{ background: "#0F172A" }}
            >
              <Background variant={BackgroundVariant.Dots} gap={24} size={1} color="#1E293B" />
              <Controls
                style={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 0 }}
              />
            </ReactFlow>
          </div>
        </div>
      </main>

      <footer className="border-t border-slate-surface px-6 py-3">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          {step > 1 ? (
            <button
              onClick={() => setStep((s) => Math.max(1, s - 1))}
              className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-4 py-1.5 uppercase tracking-widest transition-colors"
            >
              &larr; Previous
            </button>
          ) : (
            <Link
              href="/"
              className="font-mono text-xs text-slate-400 hover:text-electric tracking-widest uppercase transition-colors"
            >
              &larr; Back
            </Link>
          )}
          {step < 3 ? (
            <button
              onClick={() => setStep((s) => Math.min(3, s + 1))}
              className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-4 py-1.5 uppercase tracking-widest transition-colors"
            >
              Next &rarr;
            </button>
          ) : (
            <button className="font-mono text-xs bg-emerald hover:bg-green-600 text-white px-4 py-1.5 uppercase tracking-widest transition-colors">
              Proceed to Checkout &rarr;
            </button>
          )}
        </div>
      </footer>
    </div>
  );
}
