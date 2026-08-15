// Entity types for the generated API. The LLM pass fills the zone below.
// The `export {}` keeps this a module even when the zone is empty — a zero-export
// .ts file is a global script under `isolatedModules` and breaks the type check.
export {};

[[LLM_INJECTION_START: TypeDefinitions]]
{{!--
  The LLM will generate TypeScript interfaces per entity here.
  Expected format per entity:

  export interface {EntityName} {
    id: string;
    {fieldName}: {tsType};
    ...
    createdAt: string;
  }

  export type Create{EntityName}Input = Omit<{EntityName}, "id" | "createdAt">;
--}}
[[LLM_INJECTION_END: TypeDefinitions]]
