// Per-entity TypeScript interfaces. Field names mirror the .NET PascalCase
// records for exact wire-format match; ids are strings (Guid serializes as string).

{{#each Entities}}
export interface {{Name}} {
  id: string;
  {{#each Fields}}{{#unless IsPrimaryKey}}
  {{NameLower}}: string;
  {{/unless}}{{/each}}
  createdAt: string;
  updatedAt: string;
}

export type Create{{Name}}Input = Omit<{{Name}}, "id" | "createdAt" | "updatedAt">;

{{/each}}

[[LLM_INJECTION_START: TypeRefinements]]
// LLM fills: per-field type narrowing where the schema's `string` lexicon
// is too coarse — e.g. emails, URLs, dates as Date | string, numeric coercion.
// One typed override per field that needs it; leave alone if no refinement needed.
[[LLM_INJECTION_END: TypeRefinements]]
