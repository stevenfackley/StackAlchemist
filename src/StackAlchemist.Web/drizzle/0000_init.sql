-- NOT `CREATE SCHEMA IF NOT EXISTS`: Postgres checks CREATE on the database
-- before it honours IF NOT EXISTS, so that form raises 42501 for the qavren-db
-- role even though the schema is pre-created and owned by it. Guarding on
-- pg_namespace means the statement is never issued when the schema exists.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'stackalchemist') THEN
    EXECUTE 'CREATE SCHEMA "stackalchemist"';
  END IF;
END $$;
--> statement-breakpoint
CREATE TABLE "stackalchemist"."generations" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"user_id" uuid,
	"transaction_id" uuid,
	"mode" text NOT NULL,
	"tier" integer DEFAULT 0 NOT NULL,
	"prompt" text,
	"schema_json" jsonb,
	"status" text DEFAULT 'pending' NOT NULL,
	"download_url" text,
	"preview_files_json" jsonb,
	"build_log" text,
	"error_message" text,
	"attempt_count" integer DEFAULT 0 NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL,
	"completed_at" timestamp with time zone,
	"project_type" text DEFAULT 'DotNetNextJs' NOT NULL,
	"personalization_json" jsonb,
	"input_tokens" integer DEFAULT 0 NOT NULL,
	"output_tokens" integer DEFAULT 0 NOT NULL,
	"model_used" text,
	"error_category" text,
	CONSTRAINT "generations_mode_check" CHECK ("stackalchemist"."generations"."mode" in ('simple', 'advanced')),
	CONSTRAINT "generations_tier_check" CHECK ("stackalchemist"."generations"."tier" >= 0 and "stackalchemist"."generations"."tier" <= 3),
	CONSTRAINT "generations_status_check" CHECK ("stackalchemist"."generations"."status" in ('pending', 'extracting_schema', 'generating_code', 'generating', 'building', 'packing', 'uploading', 'success', 'failed')),
	CONSTRAINT "generations_project_type_check" CHECK ("stackalchemist"."generations"."project_type" in ('DotNetNextJs', 'PythonReact')),
	CONSTRAINT "generations_error_category_check" CHECK ("stackalchemist"."generations"."error_category" is null or "stackalchemist"."generations"."error_category" in ('quota', 'schema', 'build', 'rate_limit', 'network', 'internal'))
);
--> statement-breakpoint
CREATE TABLE "stackalchemist"."profiles" (
	"id" uuid PRIMARY KEY NOT NULL,
	"email" text NOT NULL,
	"api_key_override" text,
	"preferred_model" text DEFAULT 'claude-sonnet-4-6' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "stackalchemist"."stripe_events" (
	"id" text PRIMARY KEY NOT NULL,
	"type" text NOT NULL,
	"processed_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "stackalchemist"."transactions" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"user_id" uuid,
	"stripe_session_id" text,
	"tier" integer NOT NULL,
	"amount" integer DEFAULT 0 NOT NULL,
	"status" text DEFAULT 'pending' NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	"generation_id" uuid,
	"stripe_payment_intent" text,
	"stripe_charge_id" text,
	"last_stripe_event_id" text,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "transactions_stripe_session_id_key" UNIQUE("stripe_session_id"),
	CONSTRAINT "transactions_status_check" CHECK ("stackalchemist"."transactions"."status" in ('pending', 'completed', 'failed', 'refund_pending', 'refunded', 'disputed')),
	CONSTRAINT "transactions_tier_check" CHECK ("stackalchemist"."transactions"."tier" >= 0 and "stackalchemist"."transactions"."tier" <= 3)
);
--> statement-breakpoint
ALTER TABLE "stackalchemist"."generations" ADD CONSTRAINT "generations_user_id_profiles_id_fk" FOREIGN KEY ("user_id") REFERENCES "stackalchemist"."profiles"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "stackalchemist"."generations" ADD CONSTRAINT "generations_transaction_id_transactions_id_fk" FOREIGN KEY ("transaction_id") REFERENCES "stackalchemist"."transactions"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "stackalchemist"."transactions" ADD CONSTRAINT "transactions_user_id_profiles_id_fk" FOREIGN KEY ("user_id") REFERENCES "stackalchemist"."profiles"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "stackalchemist"."transactions" ADD CONSTRAINT "transactions_generation_id_generations_id_fk" FOREIGN KEY ("generation_id") REFERENCES "stackalchemist"."generations"("id") ON DELETE set null ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "idx_generations_user_id" ON "stackalchemist"."generations" USING btree ("user_id");--> statement-breakpoint
CREATE INDEX "idx_generations_status" ON "stackalchemist"."generations" USING btree ("status");--> statement-breakpoint
CREATE INDEX "idx_generations_project_type" ON "stackalchemist"."generations" USING btree ("project_type");--> statement-breakpoint
CREATE INDEX "idx_transactions_user_id" ON "stackalchemist"."transactions" USING btree ("user_id");--> statement-breakpoint
CREATE INDEX "idx_transactions_stripe_session" ON "stackalchemist"."transactions" USING btree ("stripe_session_id");--> statement-breakpoint
CREATE INDEX "idx_transactions_payment_intent" ON "stackalchemist"."transactions" USING btree ("stripe_payment_intent") WHERE "stackalchemist"."transactions"."stripe_payment_intent" is not null;--> statement-breakpoint
CREATE INDEX "idx_transactions_charge" ON "stackalchemist"."transactions" USING btree ("stripe_charge_id") WHERE "stackalchemist"."transactions"."stripe_charge_id" is not null;