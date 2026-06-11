"use client";

import type { Entity, Relationship } from "@/lib/types";
import { Eyebrow, Cluster, Label, Panel, Select, Stack, TextInput, Toggle } from "@/components/ui";
import { ICON_BTN, ADD_BLOCK, ADD_INLINE } from "./styles";

const FIELD_TYPES = ["UUID", "String", "Integer", "Decimal", "Boolean", "Timestamp", "Text", "JSON"];
const REL_TYPES: Relationship["type"][] = ["Has Many", "Belongs To", "Has One", "Many To Many"];

export function StepEntities({ entities, setEntities, relationships, setRelationships }: {
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
                <button type="button" aria-label={`Remove entity ${entity.name || "unnamed"}`} onClick={() => setEntities((p) => p.filter((_, i) => i !== eidx))} className={ICON_BTN}>✕</button>
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
                    <button type="button" aria-label={`Remove field ${field.name || "unnamed"}`} onClick={() => setEntities((p) => p.map((x, i) => i === eidx ? { ...x, fields: x.fields.filter((_, j) => j !== fidx) } : x))} className={ICON_BTN}>✕</button>
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
            <button type="button" aria-label={`Remove relationship ${rel.from} ${rel.type} ${rel.to}`} onClick={() => setRelationships((p) => p.filter((_, i) => i !== idx))} className={ICON_BTN}>✕</button>
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
