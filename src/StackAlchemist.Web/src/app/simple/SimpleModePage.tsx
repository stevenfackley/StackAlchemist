"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
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
import { Logo } from "@/components/logo";

function EntityCard({
  name,
  fields,
}: {
  name: string;
  fields: { name: string; type: string; pk?: boolean }[];
}) {
  return (
    <div className="bg-slate-surface border border-electric/60 min-w-[160px] text-left">
      <div className="bg-electric/10 border-b border-electric/60 px-3 py-1.5">
        <span className="font-mono text-xs font-bold text-blue-400 tracking-widest uppercase">
          {name}
        </span>
      </div>
      <div className="px-3 py-2 space-y-0.5">
        {fields.map((f) => (
          <div key={f.name} className="flex items-center gap-2">
            {f.pk && (
              <span className="font-mono text-[9px] text-yellow-400 border border-yellow-400/40 px-0.5">
                PK
              </span>
            )}
            <span className="font-mono text-[11px] text-white">{f.name}</span>
            <span className="font-mono text-[10px] text-slate-500">{f.type}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

const MOCK_NODES: Node[] = [
  {
    id: "user",
    position: { x: 80, y: 160 },
    data: {
      label: (
        <EntityCard
          name="User"
          fields={[
            { name: "id", type: "UUID", pk: true },
            { name: "email", type: "String" },
            { name: "name", type: "String" },
            { name: "created_at", type: "Timestamp" },
          ]}
        />
      ),
    },
    type: "default",
    style: { background: "transparent", border: "none", padding: 0 },
  },
  {
    id: "plan",
    position: { x: 400, y: 60 },
    data: {
      label: (
        <EntityCard
          name="Plan"
          fields={[
            { name: "id", type: "UUID", pk: true },
            { name: "name", type: "String" },
            { name: "price", type: "Decimal" },
          ]}
        />
      ),
    },
    type: "default",
    style: { background: "transparent", border: "none", padding: 0 },
  },
  {
    id: "subscription",
    position: { x: 400, y: 280 },
    data: {
      label: (
        <EntityCard
          name="Subscription"
          fields={[
            { name: "id", type: "UUID", pk: true },
            { name: "user_id", type: "UUID" },
            { name: "plan_id", type: "UUID" },
            { name: "status", type: "String" },
            { name: "started_at", type: "Timestamp" },
          ]}
        />
      ),
    },
    type: "default",
    style: { background: "transparent", border: "none", padding: 0 },
  },
  {
    id: "checkin",
    position: { x: 700, y: 200 },
    data: {
      label: (
        <EntityCard
          name="CheckIn"
          fields={[
            { name: "id", type: "UUID", pk: true },
            { name: "user_id", type: "UUID" },
            { name: "checked_at", type: "Timestamp" },
          ]}
        />
      ),
    },
    type: "default",
    style: { background: "transparent", border: "none", padding: 0 },
  },
];

const MOCK_EDGES: Edge[] = [
  {
    id: "u-s",
    source: "user",
    target: "subscription",
    label: "1:M",
    style: { stroke: "#3B82F6" },
    labelStyle: { fill: "#94a3b8", fontFamily: "JetBrains Mono", fontSize: 10 },
    labelBgStyle: { fill: "#1E293B" },
  },
  {
    id: "p-s",
    source: "plan",
    target: "subscription",
    label: "1:M",
    style: { stroke: "#3B82F6" },
    labelStyle: { fill: "#94a3b8", fontFamily: "JetBrains Mono", fontSize: 10 },
    labelBgStyle: { fill: "#1E293B" },
  },
  {
    id: "u-c",
    source: "user",
    target: "checkin",
    label: "1:M",
    style: { stroke: "#3B82F6" },
    labelStyle: { fill: "#94a3b8", fontFamily: "JetBrains Mono", fontSize: 10 },
    labelBgStyle: { fill: "#1E293B" },
  },
];

const GEN_STEPS = [
  "Parsing natural language prompt...",
  "Extracting Entities: User, Plan, Subscription, CheckIn",
  "Mapping Relationships: User (1:M) Subscription",
  "Mapping Relationships: Plan (1:M) Subscription",
  "Mapping Relationships: User (1:M) CheckIn",
  "Generating entity-relationship canvas...",
];

export default function SimpleModePage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const prompt = searchParams.get("q") ?? "";

  const [phase, setPhase] = useState<"generating" | "canvas">("generating");
  const [progress, setProgress] = useState(0);
  const [logLines, setLogLines] = useState<string[]>([]);

  const [nodes, , onNodesChange] = useNodesState(MOCK_NODES);
  const [edges, setEdges, onEdgesChange] = useEdgesState(MOCK_EDGES);

  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

  useEffect(() => {
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
  }, []);

  return (
    <div className="min-h-screen flex flex-col bg-void">
      <header className="border-b border-slate-surface bg-void/80 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-4">
          <Logo />
          <span className="text-slate-600 font-mono text-xs">|</span>
          <Link
            href="/"
            className="font-mono text-xs text-slate-400 hover:text-electric transition-colors tracking-widest uppercase"
          >
            &larr; Back
          </Link>
        </div>
      </header>

      <div className="border-b border-slate-surface bg-slate-surface/30 px-4 py-3">
        <div className="max-w-6xl mx-auto">
          <p className="font-mono text-xs text-slate-500 tracking-widest uppercase mb-1">
            Input
          </p>
          <p className="font-mono text-sm text-white">{prompt || "No prompt provided."}</p>
        </div>
      </div>

      <main className="flex-1 flex flex-col">
        {phase === "generating" && (
          <div className="flex-1 flex flex-col items-center justify-center px-4 py-16 space-y-8">
            <div className="w-full max-w-xl space-y-2">
              <div className="flex items-center justify-between font-mono text-xs text-slate-400 uppercase tracking-widest">
                <span>Generating Schema</span>
                <span className="text-electric">{progress}%</span>
              </div>
              <div className="h-1 bg-slate-surface w-full">
                <div
                  className="h-full bg-electric transition-all duration-300"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>

            <div className="w-full max-w-xl bg-slate-surface border border-slate-border p-4 space-y-1">
              {logLines.map((line, i) => (
                <p key={i} className="font-mono text-xs text-emerald">
                  <span className="text-slate-500 mr-2">&rsaquo;</span>
                  {line}
                </p>
              ))}
              {progress < 100 && (
                <p className="font-mono text-xs text-electric animate-pulse">
                  <span className="mr-2">&rsaquo;</span>_
                </p>
              )}
            </div>
          </div>
        )}

        {phase === "canvas" && (
          <div className="flex-1 flex flex-col">
            <div className="border-b border-slate-surface px-4 py-3 bg-void">
              <div className="max-w-6xl mx-auto flex items-center justify-between">
                <p className="font-mono text-xs text-emerald tracking-widest uppercase">
                  &#10003; Schema extracted &mdash; review and edit before proceeding
                </p>
                <div className="flex gap-3">
                  <button className="font-mono text-xs border border-slate-border text-slate-400 hover:border-electric hover:text-electric px-4 py-1.5 uppercase tracking-widest transition-colors">
                    Edit Schema
                  </button>
                  <button
                    onClick={() => router.push("/advanced?step=3")}
                    className="font-mono text-xs bg-electric hover:bg-blue-600 text-white px-4 py-1.5 uppercase tracking-widest transition-colors"
                  >
                    Confirm &amp; Proceed &rarr;
                  </button>
                </div>
              </div>
            </div>

            <div className="flex-1" style={{ minHeight: "500px" }}>
              <ReactFlow
                nodes={nodes}
                edges={edges}
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
                <MiniMap
                  style={{ background: "#1E293B", border: "1px solid #334155", borderRadius: 0 }}
                  nodeColor="#3B82F6"
                />
              </ReactFlow>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
