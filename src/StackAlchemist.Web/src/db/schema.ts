import { sql } from "drizzle-orm";
import {
  check, index, integer, jsonb, pgSchema, text, timestamp, uuid, type AnyPgColumn,
} from "drizzle-orm/pg-core";
import type {
  GenerationErrorCategory, GenerationSchema, GenerationStatus, PersonalizationData, ProjectType, Tier,
} from "@/lib/types";

export const sa = pgSchema("stackalchemist");

const tz = { withTimezone: true, mode: "string" } as const;

export const profiles = sa.table("profiles", {
  // Supabase user id until phase C, then the Keycloak `sub`. Plain uuid, no FK.
  id: uuid("id").primaryKey(),
  email: text("email").notNull(),
  api_key_override: text("api_key_override"),
  preferred_model: text("preferred_model").notNull().default("claude-sonnet-4-6"),
  created_at: timestamp("created_at", tz).notNull().defaultNow(),
});

export const generations = sa.table(
  "generations",
  {
    id: uuid("id").primaryKey().default(sql`gen_random_uuid()`),
    user_id: uuid("user_id").references(() => profiles.id, { onDelete: "set null" }),
    transaction_id: uuid("transaction_id").references((): AnyPgColumn => transactions.id, { onDelete: "set null" }),
    mode: text("mode").$type<"simple" | "advanced">().notNull(),
    tier: integer("tier").$type<Tier>().notNull().default(0),
    prompt: text("prompt"),
    schema_json: jsonb("schema_json").$type<GenerationSchema>(),
    status: text("status").$type<GenerationStatus>().notNull().default("pending"),
    download_url: text("download_url"),
    preview_files_json: jsonb("preview_files_json").$type<Record<string, string>>(),
    build_log: text("build_log"),
    error_message: text("error_message"),
    attempt_count: integer("attempt_count").notNull().default(0),
    created_at: timestamp("created_at", tz).notNull().defaultNow(),
    updated_at: timestamp("updated_at", tz).notNull().defaultNow(),
    completed_at: timestamp("completed_at", tz),
    project_type: text("project_type").$type<ProjectType>().notNull().default("DotNetNextJs"),
    personalization_json: jsonb("personalization_json").$type<PersonalizationData>(),
    input_tokens: integer("input_tokens").notNull().default(0),
    output_tokens: integer("output_tokens").notNull().default(0),
    model_used: text("model_used"),
    error_category: text("error_category").$type<GenerationErrorCategory>(),
  },
  (t) => [
    check("generations_mode_check", sql`${t.mode} in ('simple', 'advanced')`),
    check("generations_tier_check", sql`${t.tier} >= 0 and ${t.tier} <= 3`),
    check("generations_status_check", sql`${t.status} in ('pending', 'extracting_schema', 'generating_code', 'generating', 'building', 'packing', 'uploading', 'success', 'failed')`),
    check("generations_project_type_check", sql`${t.project_type} in ('DotNetNextJs', 'PythonReact')`),
    check("generations_error_category_check", sql`${t.error_category} is null or ${t.error_category} in ('quota', 'schema', 'build', 'rate_limit', 'network', 'internal')`),
    index("idx_generations_user_id").on(t.user_id),
    index("idx_generations_status").on(t.status),
    index("idx_generations_project_type").on(t.project_type),
  ],
);

export const transactions = sa.table(
  "transactions",
  {
    id: uuid("id").primaryKey().default(sql`gen_random_uuid()`),
    user_id: uuid("user_id").references(() => profiles.id, { onDelete: "set null" }),
    stripe_session_id: text("stripe_session_id").unique("transactions_stripe_session_id_key"),
    tier: integer("tier").notNull(),
    amount: integer("amount").notNull().default(0),
    status: text("status").notNull().default("pending"),
    created_at: timestamp("created_at", tz).notNull().defaultNow(),
    generation_id: uuid("generation_id").references((): AnyPgColumn => generations.id, { onDelete: "set null" }),
    stripe_payment_intent: text("stripe_payment_intent"),
    stripe_charge_id: text("stripe_charge_id"),
    last_stripe_event_id: text("last_stripe_event_id"),
    updated_at: timestamp("updated_at", tz).notNull().defaultNow(),
  },
  (t) => [
    check("transactions_status_check", sql`${t.status} in ('pending', 'completed', 'failed', 'refund_pending', 'refunded', 'disputed')`),
    check("transactions_tier_check", sql`${t.tier} >= 0 and ${t.tier} <= 3`),
    index("idx_transactions_user_id").on(t.user_id),
    index("idx_transactions_stripe_session").on(t.stripe_session_id),
    index("idx_transactions_payment_intent").on(t.stripe_payment_intent).where(sql`${t.stripe_payment_intent} is not null`),
    index("idx_transactions_charge").on(t.stripe_charge_id).where(sql`${t.stripe_charge_id} is not null`),
  ],
);

export const stripe_events = sa.table("stripe_events", {
  id: text("id").primaryKey(),
  type: text("type").notNull(),
  processed_at: timestamp("processed_at", tz).notNull().defaultNow(),
});

export type GenerationRow = typeof generations.$inferSelect;
export type NewGenerationRow = typeof generations.$inferInsert;
export type ProfileRow = typeof profiles.$inferSelect;
