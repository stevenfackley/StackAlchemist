import { useState } from "react";
import type { SchemaEntity, SchemaRelation } from "../simple-mode-schema";

export function useSchemaEditorDraft(entities: SchemaEntity[], relations: SchemaRelation[]) {
  const [draft, setDraft] = useState<SchemaEntity[]>(() => JSON.parse(JSON.stringify(entities)));
  const [draftRels, setDraftRels] = useState<SchemaRelation[]>(() => JSON.parse(JSON.stringify(relations)));

  function updateEntity(idx: number, patch: Partial<SchemaEntity>) {
    setDraft((d) => d.map((e, i) => (i === idx ? { ...e, ...patch } : e)));
  }

  function updateField(entityIdx: number, fieldIdx: number, patch: Partial<SchemaEntity["fields"][number]>) {
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

  return {
    draft,
    draftRels,
    updateEntity,
    updateField,
    addField,
    removeField,
    addEntity,
    removeEntity,
    addRelation,
    updateRelation,
    removeRelation,
    setDraft,
    setDraftRels,
  };
}
