"use server";

import { createServerClient } from "./supabase";
import type {
  Tier,
  GenerationSchema,
  SubmitGenerationResponse,
  EngineGenerateRequest,
} from "./types";

function resolveEngineUrl() {
  const configuredUrl = process.env.ENGINE_API_URL;
  if (configuredUrl) {
    return configuredUrl;
  }

  if (process.env.NODE_ENV !== "production") {
    return "http://localhost:5000";
  }

  throw new Error("ENGINE_API_URL must be configured in production.");
}

/* ─────────────────────────────────────────────────────────────────────────────
   submitSimpleGeneration
   Called when a user submits a natural-language prompt (Simple Mode).
   1. Creates a `generation` row in Supabase with status=pending
   2. Fires a request to the .NET Engine to kick off the pipeline
   3. Returns the generation ID so the frontend can subscribe to updates
───────────────────────────────────────────────────────────────────────────── */
export async function submitSimpleGeneration(
  prompt: string,
  tier: Tier
): Promise<SubmitGenerationResponse> {
  if (!prompt || prompt.trim().length < 10) {
    return { success: false, error: "Please provide a more detailed description (at least 10 characters)." };
  }

  let db;

  try {
    db = createServerClient();
  } catch (error) {
    console.error("[submitSimpleGeneration] Supabase configuration error:", error);
    return { success: false, error: "Server configuration is incomplete. Please contact support." };
  }

  // 1. Insert generation record
  const { data, error } = await db
    .from("generations")
    .insert({
      mode: "simple",
      tier,
      prompt: prompt.trim(),
      status: "pending",
      schema_json: null,
      download_url: null,
      error_message: null,
      attempt_count: 0,
      user_id: null,
      completed_at: null,
    })
    .select()
    .single();

  if (error || !data) {
    console.error("[submitSimpleGeneration] Supabase insert error:", error);
    return { success: false, error: "Failed to create generation record. Please try again." };
  }

  const generationId = data.id;

  // 2. Notify the .NET Engine
  try {
    const enginePayload: EngineGenerateRequest = {
      generationId,
      mode: "simple",
      tier,
      prompt: prompt.trim(),
    };

    const engineUrl = resolveEngineUrl();
    const engineRes = await fetch(`${engineUrl}/api/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(enginePayload),
    });

    if (!engineRes.ok) {
      const errText = await engineRes.text();
      console.error("[submitSimpleGeneration] Engine error:", errText);
      // Don't fail the user — the generation row exists, engine can be retried
    }
  } catch (fetchErr) {
    console.error("[submitSimpleGeneration] Failed to reach engine:", fetchErr);
    // Log but continue — the UI will show the pending state
  }

  return {
    success: true,
    generationId,
    redirectUrl: `/generate/${generationId}`,
  };
}

/* ─────────────────────────────────────────────────────────────────────────────
   submitAdvancedGeneration
   Called from the Advanced Wizard when the user clicks "Proceed to Checkout".
   Saves the full schema (entities, relationships, endpoints) to Supabase
   and kicks off the Engine pipeline.
───────────────────────────────────────────────────────────────────────────── */
export async function submitAdvancedGeneration(
  schema: GenerationSchema,
  tier: Tier
): Promise<SubmitGenerationResponse> {
  if (!schema.entities || schema.entities.length === 0) {
    return { success: false, error: "Please define at least one entity before proceeding." };
  }

  // Validate entity names
  for (const entity of schema.entities) {
    if (!entity.name.trim()) {
      return { success: false, error: "All entities must have a name." };
    }
  }

  let db;

  try {
    db = createServerClient();
  } catch (error) {
    console.error("[submitAdvancedGeneration] Supabase configuration error:", error);
    return { success: false, error: "Server configuration is incomplete. Please contact support." };
  }

  // Build a human-readable prompt summary from the schema
  const promptSummary =
    schema.entities.map((e) => e.name).join(", ") +
    ` — ${schema.entities.length} entities, ${schema.relationships.length} relationships, ${schema.endpoints.length} endpoints`;

  // 1. Insert generation record with the full schema
  const { data, error } = await db
    .from("generations")
    .insert({
      mode: "advanced",
      tier,
      prompt: promptSummary,
      status: "pending",
      schema_json: schema,
      download_url: null,
      error_message: null,
      attempt_count: 0,
      user_id: null,
      completed_at: null,
    })
    .select()
    .single();

  if (error || !data) {
    console.error("[submitAdvancedGeneration] Supabase insert error:", error);
    return { success: false, error: "Failed to save your schema. Please try again." };
  }

  const generationId = data.id;

  // 2. Notify the .NET Engine with the full schema
  try {
    const enginePayload: EngineGenerateRequest = {
      generationId,
      mode: "advanced",
      tier,
      schema,
    };

    const engineUrl = resolveEngineUrl();
    const engineRes = await fetch(`${engineUrl}/api/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(enginePayload),
    });

    if (!engineRes.ok) {
      const errText = await engineRes.text();
      console.error("[submitAdvancedGeneration] Engine error:", errText);
    }
  } catch (fetchErr) {
    console.error("[submitAdvancedGeneration] Failed to reach engine:", fetchErr);
  }

  return {
    success: true,
    generationId,
    redirectUrl: `/generate/${generationId}`,
  };
}

/* ─────────────────────────────────────────────────────────────────────────────
   getGeneration
   Fetches a single generation record by ID.
   Safe to use in Server Components.
───────────────────────────────────────────────────────────────────────────── */
export async function getGeneration(generationId: string) {
  let db;

  try {
    db = createServerClient();
  } catch (error) {
    console.error("[getGeneration] Supabase configuration error:", error);
    return null;
  }
  const { data, error } = await db
    .from("generations")
    .select("*")
    .eq("id", generationId)
    .single();

  if (error) {
    console.error("[getGeneration] Error:", error);
    return null;
  }

  return data;
}

/* ─────────────────────────────────────────────────────────────────────────────
   retryGeneration
   Asks the Engine to retry a failed generation (up to 3 attempts).
───────────────────────────────────────────────────────────────────────────── */
export async function retryGeneration(
  generationId: string
): Promise<{ success: boolean; error?: string }> {
  let db;

  try {
    db = createServerClient();
  } catch (error) {
    console.error("[retryGeneration] Supabase configuration error:", error);
    return { success: false, error: "Server configuration is incomplete. Please contact support." };
  }

  const { data: gen, error: fetchErr } = await db
    .from("generations")
    .select("*")
    .eq("id", generationId)
    .single();

  if (fetchErr || !gen) {
    return { success: false, error: "Generation not found." };
  }

  if (gen.attempt_count >= 3) {
    return { success: false, error: "Maximum retry attempts (3) reached. Please start a new generation." };
  }

  if (gen.status !== "failed") {
    return { success: false, error: "Only failed generations can be retried." };
  }

  // Reset status
  await db
    .from("generations")
    .update({ status: "pending", error_message: null })
    .eq("id", generationId);

  // Re-fire the engine
  try {
    const enginePayload: EngineGenerateRequest = {
      generationId,
      mode: gen.mode,
      tier: gen.tier as Tier,
      prompt: gen.prompt ?? undefined,
      schema: gen.schema_json ?? undefined,
    };

    const engineUrl = resolveEngineUrl();
    await fetch(`${engineUrl}/api/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(enginePayload),
    });
  } catch (err) {
    console.error("[retryGeneration] Engine call failed:", err);
  }

  return { success: true };
}
