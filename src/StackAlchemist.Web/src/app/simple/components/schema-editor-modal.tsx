"use client";

import { Plus, Trash2, X } from "lucide-react";
import { useSchemaEditorDraft } from "../hooks/use-schema-editor-draft";
import { FIELD_TYPES, type SchemaEntity, type SchemaRelation } from "../simple-mode-schema";

export function SchemaEditorModal({
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
  const {
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
  } = useSchemaEditorDraft(entities, relations);

  return (
    <div className="fixed inset-0 z-[100] flex min-h-[100dvh] items-start justify-center bg-black/60 backdrop-blur-sm overflow-y-auto py-4 pb-[env(safe-area-inset-bottom)] md:py-8">
      <div className="w-full max-w-3xl bg-slate-800 border border-slate-600/50 rounded-2xl shadow-2xl mx-4">
        <div className="flex items-center justify-between border-b border-slate-700/50 px-6 py-4">
          <h2 className="font-mono text-sm font-bold text-white tracking-widest uppercase">Edit Schema</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="px-6 py-4 space-y-6 max-h-[70vh] overflow-y-auto">
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
                    (draftRels as SchemaRelation[]).forEach((rel) => {
                      if (rel.sourceId === oldId || rel.targetId === oldId) {
                        updateRelation(draftRels.indexOf(rel), {
                          sourceId: rel.sourceId === oldId ? newId : rel.sourceId,
                          targetId: rel.targetId === oldId ? newId : rel.targetId,
                        });
                      }
                    });
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
