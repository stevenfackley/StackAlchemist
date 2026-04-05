"use server";

import { headers } from "next/headers";
import { createServerClient } from "./supabase";
import { getServerUser } from "./supabase-server";
import { buildDemoGeneration } from "./demo-data";
import { hasEngineConfig, hasServerSupabaseConfig, hasStripeConfig, isDemoMode } from "./runtime-config";
import type {
  Generation,
  Tier,
  GenerationSchema,
  SubmitGenerationResponse,
  EngineGenerateRequest,
  ProjectType,
  PersonalizationData,
} from "./types";
import { DEFAULT_PERSONALIZATION } from "./types";

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
  tier: Tier,
  projectType: ProjectType = "DotNetNextJs",
  personalization?: PersonalizationData
): Promise<SubmitGenerationResponse> {
  if (!prompt || prompt.trim().length < 10) {
    return { success: false, error: "Please provide a more detailed description (at least 10 characters)." };
  }

  if (isDemoMode || !hasServerSupabaseConfig() || !hasEngineConfig()) {
    const generationId = `demo-simple-${Date.now()}`;
    return {
      success: true,
      generationId,
      redirectUrl: `/generate/${generationId}?demo=1&tier=${tier}`,
    };
  }

  let db;

  try {
    db = createServerClient();
  } catch (error) {
    console.error("[submitSimpleGeneration] Supabase configuration error:", error);
    return { success: false, error: "Server configuration is incomplete. Please contact support." };
  }

  // Attach the authenticated user's ID if they are signed in.
  const user = await getServerUser();

  // 1. Insert generation record
  const { data, error } = await db
    .from("generations")
    .insert({
      mode: "simple",
      tier,
      prompt: prompt.trim(),
      project_type: projectType,
      status: "pending",
      schema_json: null,
      personalization_json: personalization ?? null,
      download_url: null,
      error_message: null,
      attempt_count: 0,
      user_id: user?.id ?? null,
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
      projectType,
      prompt: prompt.trim(),
      personalization: personalization ?? DEFAULT_PERSONALIZATION,
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
   extractSchema
   Called in Simple Mode before generation. Sends the prompt to the Engine
   which calls Claude 3.5 to extract a structured JSON schema.
   Returns the schema so the user can review/edit on the React Flow canvas.
───────────────────────────────────────────────────────────────────────────── */
export async function extractSchema(
  generationId: string,
  prompt: string
): Promise<{ success: true; schema: GenerationSchema } | { success: false; error: string }> {
  if (isDemoMode || !hasEngineConfig()) {
    return { success: true, schema: buildDemoGeneration(generationId).schema_json! };
  }

  try {
    const engineUrl = resolveEngineUrl();
    const res = await fetch(`${engineUrl}/api/extract-schema`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ generationId, prompt: prompt.trim() }),
    });

    if (!res.ok) {
      const body = await res.json().catch(() => ({ error: "Schema extraction failed" }));
      return { success: false, error: body.error || "Schema extraction failed" };
    }

    const data = await res.json();
    return { success: true, schema: data.schema as GenerationSchema };
  } catch (err) {
    console.error("[extractSchema] Failed:", err);
    return { success: false, error: "Failed to reach the generation engine." };
  }
}

/* ─────────────────────────────────────────────────────────────────────────────
   submitAdvancedGeneration
   Called from the Advanced Wizard when the user clicks "Proceed to Checkout".
   Saves the full schema (entities, relationships, endpoints) to Supabase
   and kicks off the Engine pipeline.
───────────────────────────────────────────────────────────────────────────── */
export async function submitAdvancedGeneration(
  schema: GenerationSchema,
  tier: Tier,
  projectType: ProjectType = "DotNetNextJs",
  personalization?: PersonalizationData
): Promise<SubmitGenerationResponse> {
  if (!schema.entities || schema.entities.length === 0) {
    return { success: false, error: "Please define at least one entity before proceeding." };
  }

  if (isDemoMode || !hasServerSupabaseConfig() || !hasEngineConfig()) {
    const generationId = `demo-advanced-${Date.now()}`;
    return {
      success: true,
      generationId,
      redirectUrl: `/generate/${generationId}?demo=1&tier=${tier}`,
    };
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

  // Attach the authenticated user's ID if they are signed in.
  const user = await getServerUser();

  // 1. Insert generation record with the full schema
  const { data, error } = await db
    .from("generations")
    .insert({
      mode: "advanced",
      tier,
      prompt: promptSummary,
      project_type: projectType,
      status: "pending",
      schema_json: schema,
      personalization_json: personalization ?? null,
      download_url: null,
      error_message: null,
      attempt_count: 0,
      user_id: user?.id ?? null,
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
      projectType,
      schema,
      personalization: personalization ?? DEFAULT_PERSONALIZATION,
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
export async function getGeneration(generationId: string, demoTier?: Tier) {
  if (isDemoMode || !hasServerSupabaseConfig()) {
    const tier = demoTier ?? 0;
    return buildDemoGeneration(generationId, tier);
  }

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
  if (isDemoMode || !hasServerSupabaseConfig() || !hasEngineConfig()) {
    return { success: true };
  }

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
      projectType: gen.project_type ?? "DotNetNextJs",
      prompt: gen.prompt ?? undefined,
      schema: gen.schema_json ?? undefined,
      personalization: gen.personalization_json ?? DEFAULT_PERSONALIZATION,
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

/* ─────────────────────────────────────────────────────────────────────────────
   createPendingGeneration
   Creates a generation row in Supabase with status=pending but does NOT fire
   the Engine.  Used by the paid-tier checkout flow — the Engine is triggered
   later by the Stripe webhook (checkout.session.completed).
───────────────────────────────────────────────────────────────────────────── */
export async function createPendingGeneration(
  mode: "simple" | "advanced",
  tier: Tier,
  prompt?: string,
  schema?: GenerationSchema,
  projectType: ProjectType = "DotNetNextJs",
  personalization?: PersonalizationData
): Promise<{ success: true; generationId: string } | { success: false; error: string }> {
  if (isDemoMode || !hasServerSupabaseConfig()) {
    return { success: true, generationId: `demo-pending-${Date.now()}` };
  }

  let db;
  try {
    db = createServerClient();
  } catch (err) {
    console.error("[createPendingGeneration] Supabase config error:", err);
    return { success: false, error: "Server configuration is incomplete. Please contact support." };
  }

  const promptSummary =
    schema
      ? schema.entities.map((e) => e.name).join(", ") +
        ` — ${schema.entities.length} entities`
      : (prompt ?? "").trim();

  // Attach the authenticated user's ID if they are signed in.
  const user = await getServerUser();

  const { data, error } = await db
    .from("generations")
    .insert({
      mode,
      tier,
      prompt: promptSummary || null,
      project_type: projectType,
      status: "pending",
      schema_json: schema ?? null,
      personalization_json: personalization ?? null,
      download_url: null,
      error_message: null,
      attempt_count: 0,
      user_id: user?.id ?? null,
      completed_at: null,
    })
    .select()
    .single();

  if (error || !data) {
    console.error("[createPendingGeneration] Insert error:", error);
    return { success: false, error: "Failed to create generation record. Please try again." };
  }

  return { success: true, generationId: data.id };
}

/* ─────────────────────────────────────────────────────────────────────────────
   createCheckoutSession
   Calls the .NET Engine which uses Stripe.net to create a hosted Checkout
   Session for the given tier.  Returns the Stripe-hosted URL for redirect.
   Only valid for tiers 1, 2, and 3 — Tier 0 (Spark) is free.
───────────────────────────────────────────────────────────────────────────── */
export async function createCheckoutSession(
  generationId: string,
  tier: Tier,
  prompt?: string,
  projectType: ProjectType = "DotNetNextJs",
  mode: "simple" | "advanced" = "advanced"
): Promise<{ success: true; sessionUrl: string } | { success: false; error: string }> {
  if (tier === 0) {
    return { success: false, error: "Tier 0 (Spark) is free — no checkout required." };
  }

  // Demo / no-Stripe fallback: skip payment and redirect to generate page directly.
  if (isDemoMode || !hasStripeConfig() || !hasEngineConfig()) {
    return { success: true, sessionUrl: `/generate/${generationId}?demo=1&tier=${tier}` };
  }

  // Resolve the origin for success / cancel URLs.
  const headersList = await headers();
  const host = headersList.get("host") ?? "localhost:3000";
  const proto =
    process.env.NODE_ENV === "production" ||
    headersList.get("x-forwarded-proto") === "https"
      ? "https"
      : "http";
  const origin = `${proto}://${host}`;

  try {
    const cancelPath =
      mode === "simple"
        ? `/simple?q=${encodeURIComponent(prompt ?? "")}&tier=${tier}`
        : `/advanced?step=4&tier=${tier}&projectType=${projectType}`;

    const engineUrl = resolveEngineUrl();
    const res = await fetch(`${engineUrl}/api/stripe/create-session`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        generationId,
        tier,
        projectType,
        prompt: prompt ?? "",
        successUrl: `${origin}/generate/${generationId}?session_id={CHECKOUT_SESSION_ID}`,
        cancelUrl: `${origin}${cancelPath}`,
      }),
    });

    if (!res.ok) {
      const body = await res.json().catch(() => ({ error: "Checkout session creation failed." }));
      console.error("[createCheckoutSession] Engine error:", body);
      return { success: false, error: body.error ?? "Failed to create checkout session." };
    }

    const data = await res.json();
    return { success: true, sessionUrl: data.url };
  } catch (err) {
    console.error("[createCheckoutSession] Fetch failed:", err);
    return { success: false, error: "Failed to reach the payment service. Please try again." };
  }
}

/* ─────────────────────────────────────────────────────────────────────────────
   getMyGenerations
   Returns all generations linked to the currently authenticated user, ordered
   newest-first.  Used by the /dashboard page.
   Returns an empty array when the visitor is anonymous or Supabase is not
   configured.
───────────────────────────────────────────────────────────────────────────── */
export async function getMyGenerations(): Promise<Generation[]> {
  const user = await getServerUser();
  if (!user) return [];

  if (!hasServerSupabaseConfig()) return [];

  let db;
  try {
    db = createServerClient();
  } catch (err) {
    console.error("[getMyGenerations] Supabase config error:", err);
    return [];
  }

  const { data, error } = await db
    .from("generations")
    .select("*")
    .eq("user_id", user.id)
    .order("created_at", { ascending: false })
    .limit(50);

  if (error) {
    console.error("[getMyGenerations] Query error:", error);
    return [];
  }

  return (data ?? []) as Generation[];
}
