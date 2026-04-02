/* ─────────────────────────────────────────────────────────────────────────────
   StackAlchemist – Shared Types
   These mirror the Supabase `public` schema. Keep in sync with migrations.
───────────────────────────────────────────────────────────────────────────── */

// ─── Generation Status ───────────────────────────────────────────────────────
export type GenerationStatus =
  | "pending"
  | "extracting_schema"
  | "generating_code"
  | "building"
  | "success"
  | "failed";

// ─── Tier ────────────────────────────────────────────────────────────────────
export type Tier = 1 | 2 | 3;

// ─── Schema Types ────────────────────────────────────────────────────────────
export interface Field {
  name: string;
  type: string;
  pk: boolean;
  nullable?: boolean;
  default?: string;
}

export interface Entity {
  name: string;
  fields: Field[];
}

export interface Relationship {
  from: string;
  type: "Has Many" | "Belongs To" | "Has One" | "Many To Many";
  to: string;
}

export interface Endpoint {
  method: "GET" | "POST" | "PUT" | "DELETE" | "PATCH";
  path: string;
  entity: string;
  description?: string;
}

export interface GenerationSchema {
  entities: Entity[];
  relationships: Relationship[];
  endpoints: Endpoint[];
}

// ─── Generation Record ───────────────────────────────────────────────────────
export interface Generation {
  id: string;
  user_id: string | null;
  status: GenerationStatus;
  mode: "simple" | "advanced";
  tier: Tier;
  prompt: string | null;
  schema_json: GenerationSchema | null;
  download_url: string | null;
  error_message: string | null;
  attempt_count: number;
  created_at: string;
  updated_at: string;
  completed_at: string | null;
}

// ─── Supabase Database Type (used for createClient<Database>) ────────────────
// Supabase v2 TypeScript client requires Relationships[], CompositeTypes, etc.
// Missing Relationships causes the Insert type to resolve to `never`.
export interface Database {
  public: {
    Tables: {
      generations: {
        Row: Generation;
        Insert: {
          id?: string;
          user_id?: string | null;
          status?: GenerationStatus;
          mode: "simple" | "advanced";
          tier: Tier;
          prompt?: string | null;
          schema_json?: GenerationSchema | null;
          download_url?: string | null;
          error_message?: string | null;
          attempt_count?: number;
          created_at?: string;
          updated_at?: string;
          completed_at?: string | null;
        };
        Update: Partial<Generation>;
        Relationships: [];
      };
    };
    Views: Record<never, never>;
    Functions: Record<never, never>;
    Enums: Record<never, never>;
    CompositeTypes: Record<never, never>;
  };
}

// ─── Server Action Result Types ───────────────────────────────────────────────
export interface SubmitGenerationResult {
  success: true;
  generationId: string;
  redirectUrl: string;
}

export interface SubmitGenerationError {
  success: false;
  error: string;
}

export type SubmitGenerationResponse = SubmitGenerationResult | SubmitGenerationError;

// ─── Simple Mode Payload ──────────────────────────────────────────────────────
export interface SimpleModePayload {
  prompt: string;
  tier: Tier;
}

// ─── Advanced Mode Payload ────────────────────────────────────────────────────
export interface AdvancedModePayload {
  schema: GenerationSchema;
  tier: Tier;
}

// ─── Engine API types ─────────────────────────────────────────────────────────
export interface EngineGenerateRequest {
  generationId: string;
  mode: "simple" | "advanced";
  tier: Tier;
  prompt?: string;
  schema?: GenerationSchema;
}

export interface EngineGenerateResponse {
  jobId: string;
  status: GenerationStatus;
}
