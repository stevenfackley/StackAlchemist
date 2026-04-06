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
// 0 = Spark (free): view-only micro IDE preview, no download
export type Tier = 0 | 1 | 2 | 3;

// ─── Platform ────────────────────────────────────────────────────────────────
export type ProjectType = "DotNetNextJs" | "PythonReact";

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
  transaction_id: string | null;
  project_type: ProjectType;
  status: GenerationStatus;
  mode: "simple" | "advanced";
  tier: Tier;
  prompt: string | null;
  schema_json: GenerationSchema | null;
  download_url: string | null;
  /** Tier 0 (Spark/free) only: generated file contents keyed by relative path.
   *  e.g. { "src/app/page.tsx": "...", "package.json": "..." }
   *  Populated by the engine instead of uploading a zip to R2.
   */
  preview_files_json: Record<string, string> | null;
  /** Personalization choices stored at generation time */
  personalization_json: PersonalizationData | null;
  /** Streaming build output from the compile worker */
  build_log: string | null;
  error_message: string | null;
  attempt_count: number;
  created_at: string;
  updated_at: string;
  completed_at: string | null;
}

// ─── Profile Record ─────────────────────────────────────────────────────────
export interface Profile {
  id: string;
  email: string;
  api_key_override: string | null;
  preferred_model: string;
  created_at: string;
}

// ─── Transaction Record ─────────────────────────────────────────────────────
export type TransactionStatus = "pending" | "completed" | "failed" | "refunded";

export interface Transaction {
  id: string;
  user_id: string | null;
  stripe_session_id: string | null;
  tier: Tier;
  amount: number;
  status: TransactionStatus;
  created_at: string;
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
          transaction_id?: string | null;
          project_type?: ProjectType;
          status?: GenerationStatus;
          mode: "simple" | "advanced";
          tier: Tier;
          prompt?: string | null;
          schema_json?: GenerationSchema | null;
          download_url?: string | null;
          preview_files_json?: Record<string, string> | null;
          personalization_json?: PersonalizationData | null;
          build_log?: string | null;
          error_message?: string | null;
          attempt_count?: number;
          created_at?: string;
          updated_at?: string;
          completed_at?: string | null;
        };
        Update: Partial<Generation>;
        Relationships: [];
      };
      profiles: {
        Row: Profile;
        Insert: {
          id: string;
          email: string;
          api_key_override?: string | null;
          preferred_model?: string;
          created_at?: string;
        };
        Update: Partial<Profile>;
        Relationships: [];
      };
      transactions: {
        Row: Transaction;
        Insert: {
          id?: string;
          user_id?: string | null;
          stripe_session_id?: string | null;
          tier: Tier;
          amount: number;
          status?: TransactionStatus;
          created_at?: string;
        };
        Update: Partial<Transaction>;
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

// ─── Personalization ──────────────────────────────────────────────────────────
export interface ColorPalette {
  id: string;
  name: string;
  primary: string;
  secondary: string;
  accent: string;
  background: string;
  surface: string;
}

export interface PersonalizationFeatureFlags {
  authMethod: "jwt" | "cookie" | "oauth" | "none";
  softDelete: boolean;
  auditTimestamps: boolean;
  includeSwagger: boolean;
  includeDockerCompose: boolean;
}

export interface PersonalizationData {
  businessDescription: string;
  projectName?: string;
  tagline?: string;
  colorScheme: ColorPalette;
  /** entity name → what it represents in this business */
  domainContext: Record<string, string>;
  featureFlags: PersonalizationFeatureFlags;
}

export const COLOR_PALETTES: ColorPalette[] = [
  { id: "corporate-blue",  name: "Corporate Blue",  primary: "#2563EB", secondary: "#1D4ED8", accent: "#60A5FA", background: "#0F172A", surface: "#1E293B" },
  { id: "warm-startup",    name: "Warm Startup",    primary: "#EA580C", secondary: "#C2410C", accent: "#FB923C", background: "#1C1008", surface: "#2D1B0E" },
  { id: "dark-hacker",     name: "Dark Hacker",     primary: "#16A34A", secondary: "#15803D", accent: "#4ADE80", background: "#020B04", surface: "#071A0A" },
  { id: "earthy-minimal",  name: "Earthy Minimal",  primary: "#D97706", secondary: "#B45309", accent: "#FCD34D", background: "#1C1007", surface: "#2D1F0A" },
  { id: "bold-saas",       name: "Bold SaaS",       primary: "#7C3AED", secondary: "#6D28D9", accent: "#A78BFA", background: "#0D0520", surface: "#1A0B33" },
  { id: "electric-teal",   name: "Electric Teal",   primary: "#0D9488", secondary: "#0F766E", accent: "#2DD4BF", background: "#011111", surface: "#042525" },
];

export const DEFAULT_PERSONALIZATION: PersonalizationData = {
  businessDescription: "",
  projectName: "",
  tagline: "",
  colorScheme: COLOR_PALETTES[0],
  domainContext: {},
  featureFlags: {
    authMethod: "jwt",
    softDelete: false,
    auditTimestamps: true,
    includeSwagger: true,
    includeDockerCompose: true,
  },
};

// ─── Simple Mode Payload ──────────────────────────────────────────────────────
export interface SimpleModePayload {
  prompt: string;
  tier: Tier;
}

// ─── Advanced Mode Payload ────────────────────────────────────────────────────
export interface AdvancedModePayload {
  schema: GenerationSchema;
  tier: Tier;
  projectType: ProjectType;
  personalization?: PersonalizationData;
}

// ─── Engine API types ─────────────────────────────────────────────────────────
export interface EngineGenerateRequest {
  generationId: string;
  mode: "simple" | "advanced";
  tier: Tier;
  projectType?: ProjectType;
  prompt?: string;
  schema?: GenerationSchema;
  personalization?: PersonalizationData;
}

export interface EngineGenerateResponse {
  jobId: string;
  status: GenerationStatus;
  projectType: ProjectType;
}
