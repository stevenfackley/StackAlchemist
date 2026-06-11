"use client";

import { useCallback } from "react";
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
import type { Entity, Relationship } from "@/lib/types";
import { ContextPanel } from "@/components/workspace";

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

export function ERPreview({ entities, relationships }: { entities: Entity[]; relationships: Relationship[] }) {
  const [, , onNodesChange] = useNodesState<Node>([]);
  const [, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

  return (
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
  );
}
