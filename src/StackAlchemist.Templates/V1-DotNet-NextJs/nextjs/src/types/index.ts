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
