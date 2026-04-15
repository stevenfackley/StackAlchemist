import { type Node, type Edge } from "@xyflow/react";

export interface SchemaField {
  name: string;
  type: string;
  pk?: boolean;
}

export interface SchemaEntity {
  id: string;
  name: string;
  fields: SchemaField[];
}

export interface SchemaRelation {
  sourceId: string;
  targetId: string;
  label: string;
}

export const FIELD_TYPES = ["UUID", "String", "Int", "Decimal", "Boolean", "Timestamp", "Text", "JSON"];

export const INITIAL_ENTITIES: SchemaEntity[] = [
  { id: "user", name: "User", fields: [{ name: "id", type: "UUID", pk: true }, { name: "email", type: "String" }, { name: "name", type: "String" }, { name: "created_at", type: "Timestamp" }] },
  { id: "plan", name: "Plan", fields: [{ name: "id", type: "UUID", pk: true }, { name: "name", type: "String" }, { name: "price", type: "Decimal" }] },
  { id: "subscription", name: "Subscription", fields: [{ name: "id", type: "UUID", pk: true }, { name: "user_id", type: "UUID" }, { name: "plan_id", type: "UUID" }, { name: "status", type: "String" }, { name: "started_at", type: "Timestamp" }] },
  { id: "checkin", name: "CheckIn", fields: [{ name: "id", type: "UUID", pk: true }, { name: "user_id", type: "UUID" }, { name: "checked_at", type: "Timestamp" }] },
];

export const INITIAL_RELATIONS: SchemaRelation[] = [
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

function EntityCard({ name, fields }: { name: string; fields: SchemaField[] }) {
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

export function buildNodes(entities: SchemaEntity[]): Node[] {
  return entities.map((e, i) => {
    const pos = ENTITY_POSITIONS[e.id] ?? { x: 80 + i * 320, y: 160 };
    return {
      id: e.id,
      position: pos,
      data: { label: <EntityCard name={e.name} fields={e.fields} /> },
      type: "default",
      style: { background: "transparent", border: "none", padding: 0 },
    };
  });
}

export function buildEdges(relations: SchemaRelation[]): Edge[] {
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

export function mapSchemaToLocal(schema: { entities: Array<{ name: string; fields: Array<{ name: string; type: string; pk?: boolean }> }>; relationships?: Array<{ from: string; to: string; type: string }> }): {
  entities: SchemaEntity[];
  relations: SchemaRelation[];
} {
  const TYPE_MAP: Record<string, string> = {
    uuid: "UUID",
    string: "String",
    integer: "Int",
    int: "Int",
    decimal: "Decimal",
    boolean: "Boolean",
    datetime: "Timestamp",
    timestamp: "Timestamp",
    text: "Text",
    json: "JSON",
  };

  const entities: SchemaEntity[] = schema.entities.map((e) => ({
    id: e.name.toLowerCase().replace(/[^a-z0-9]/g, "_"),
    name: e.name,
    fields: e.fields.map((f) => ({
      name: f.name,
      type: TYPE_MAP[f.type.toLowerCase()] ?? f.type,
      pk: f.pk,
    })),
  }));

  const REL_LABEL: Record<string, string> = {
    "one-to-many": "1:M",
    "Has Many": "1:M",
    "many-to-many": "M:M",
    "Many To Many": "M:M",
    "one-to-one": "1:1",
    "Has One": "1:1",
    "Belongs To": "M:1",
  };

  const entityIds = new Set(entities.map((e) => e.id));
  const relations: SchemaRelation[] = (schema.relationships ?? [])
    .map((r) => {
      const sourceId = r.from.toLowerCase().replace(/[^a-z0-9]/g, "_");
      const targetId = r.to.toLowerCase().replace(/[^a-z0-9]/g, "_");
      if (!entityIds.has(sourceId) || !entityIds.has(targetId)) return null;
      return { sourceId, targetId, label: REL_LABEL[r.type] ?? "1:M" };
    })
    .filter((r): r is SchemaRelation => r !== null);

  return { entities, relations };
}
