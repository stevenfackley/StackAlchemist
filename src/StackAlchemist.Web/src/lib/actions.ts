"use server";

import { createCipheriv, randomBytes, scryptSync } from "node:crypto";
import { revalidatePath } from "next/cache";
import { headers } from "next/headers";
import { getDataStore, type NewGeneration, type ProfileSettingsRow } from "./data";
import { getSessionUser, type SessionUser } from "./session";
import { buildDemoGeneration } from "./demo-data";
import { postToEngine } from "./engine-client";
import { safeReturnTo } from "./proxy-utils";
import { hasEngineConfig, hasDataStoreConfig, hasStripeConfig, isDemoMode } from "./runtime-config";
import type {
  Generation,
  GenerationSummary,
  Tier,
  GenerationSchema,
  SubmitGenerationResponse,
  EngineGenerateRequest,
  ProjectType,
  PersonalizationData,
  ProfileSettings,
  SaveProfileSettingsState,
} from "./types";
import { DEFAULT_PERSONALIZATION } from "./types";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const DEFAULT_MODEL = "claude-sonnet-5-5";
// Mirrors MODEL_OPTIONS in dashboard/ByokSettingsForm.tsx. The Engine re-checks: on OUR Anthropic
// key only claude-sonnet-5-5 and claude-haiku-4-5 run (LlmCredentialResolver); Opus needs the
// user's own Anthropic key. Retired ids stored before a bump are remapped by a Drizzle migration
// (0002) and normalize to DEFAULT_MODEL on read.
const ALLOWED_PROFILE_MODELS = new Set([
  DEFAULT_MODEL,
  "claude-opus-5-5",
  "claude-haiku-4-5",
  "openai/gpt-6.1-sol",
  "openrouter/anthropic/claude-sonnet-5.5",
]);

/**
 * Sanitizes a user-supplied prompt before sending to the LLM.
 * - Caps length to prevent token abuse
 * - Strips null bytes and non-printable control chars (except newlines/tabs)
 * - Blocks obvious prompt-injection patterns
 */
function sanitizePrompt(raw: string, maxLength = 2000): string {
  // Strip null bytes and non-printable control chars (keep \n \r \t)
  let s = raw.replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, "");
  // Collapse excessive whitespace lines
  s = s.replace(/\n{4,}/g, "\n\n\n");
  // Hard cap
  if (s.length > maxLength) s = s.slice(0, maxLength);
  return s.trim();
}

function normalizePreferredModel(model: string | null | undefined) {
  return model && ALLOWED_PROFILE_MODELS.has(model) ? model : DEFAULT_MODEL;
}

function getByokEncryptionSecret() {
  // Finding I3: NEW saves require a DISTINCT BYOK_ENCRYPTION_KEY — deliberately NO fallback to
  // ENGINE_SERVICE_KEY. Stored keys are decrypted engine-side with this same secret; falling back
  // to the engine key would strand every stored BYOK key if that key later rotates. The Engine's
  // ByokKeyProtector keeps a decrypt-time ENGINE_SERVICE_KEY fallback for keys saved before this
  // requirement, but writes must be pinned to BYOK_ENCRYPTION_KEY.
  return process.env.BYOK_ENCRYPTION_KEY ?? "";
}

function encryptApiKeyOverride(apiKey: string) {
  const secret = getByokEncryptionSecret();
  if (secret.length < 32) {
    throw new Error("BYOK_ENCRYPTION_KEY must be configured with at least 32 characters before storing API keys.");
  }

  const key = scryptSync(secret, "stackalchemist-byok-v1", 32);
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", key, iv);
  const encrypted = Buffer.concat([cipher.update(apiKey, "utf8"), cipher.final()]);
  const tag = cipher.getAuthTag();

  return `v1:${iv.toString("base64")}:${tag.toString("base64")}:${encrypted.toString("base64")}`;
}

function parseOptionalApiKey(formData: FormData) {
  const rawValue = formData.get("apiKeyOverride");
  if (typeof rawValue !== "string") {
    return "";
  }

  return rawValue.trim();
}

/* ─────────────────────────────────────────────────────────────────────────────
   getProfileSettings / saveProfileSettings
   Dashboard BYOK persistence. API keys are AES-GCM encrypted before storage.
───────────────────────────────────────────────────────────────────────────── */
export async function getProfileSettings(): Promise<ProfileSettings> {
  const user = await getSessionUser();
  if (!user || !hasDataStoreConfig()) {
    return {
      email: user?.email ?? "",
      hasApiKeyOverride: false,
      preferredModel: DEFAULT_MODEL,
    };
  }

  let profile: ProfileSettingsRow | null = null;
  try {
    profile = await getDataStore().getProfile(user.id);
  } catch (err) {
    console.error("[getProfileSettings] Query error:", err);
  }

  return {
    email: profile?.email ?? user.email ?? "",
    hasApiKeyOverride: Boolean(profile?.api_key_override),
    preferredModel: normalizePreferredModel(profile?.preferred_model),
  };
}

export async function saveProfileSettings(
  _previousState: SaveProfileSettingsState,
  formData: FormData
): Promise<SaveProfileSettingsState> {
  const user = await getSessionUser();
  if (!user) {
    return { status: "error", message: "Sign in before saving API settings." };
  }

  if (!hasDataStoreConfig()) {
    return { status: "error", message: "Server database configuration is incomplete." };
  }

  const preferredModelValue = formData.get("preferredModel");
  const preferredModel = typeof preferredModelValue === "string"
    ? preferredModelValue
    : DEFAULT_MODEL;

  if (!ALLOWED_PROFILE_MODELS.has(preferredModel)) {
    return { status: "error", message: "Choose a supported model before saving." };
  }

  const apiKey = parseOptionalApiKey(formData);
  const clearApiKey = formData.get("clearApiKey") === "true";

  if (apiKey && apiKey.length < 20) {
    return { status: "error", message: "API key looks too short. Paste the full provider key." };
  }

  let apiKeyOverride: string | null | undefined;
  try {
    apiKeyOverride = apiKey ? encryptApiKeyOverride(apiKey) : clearApiKey ? null : undefined;
  } catch (err) {
    console.error("[saveProfileSettings] Encryption error:", err);
    return {
      status: "error",
      message: "BYOK encryption is not configured. Set BYOK_ENCRYPTION_KEY before storing keys.",
    };
  }

  try {
    await getDataStore().upsertProfile({
      id: user.id,
      email: user.email ?? "",
      preferred_model: preferredModel,
      api_key_override: apiKeyOverride,
    });
  } catch (err) {
    console.error("[saveProfileSettings] Upsert error:", err);
    return { status: "error", message: "Failed to save API settings." };
  }

  revalidatePath("/dashboard");

  return {
    status: "success",
    message: apiKey
      ? "API settings saved. The key was encrypted before storage."
      : clearApiKey
        ? "API settings saved. Stored API key cleared."
        : "Preferred model saved.",
  };
}

/* ─────────────────────────────────────────────────────────────────────────────
   submitSimpleGeneration
   Called when a user submits a natural-language prompt (Simple Mode).
   1. Creates a `generation` row in the data store with status=pending
   2. Fires a request to the .NET Engine to kick off the pipeline
   3. Returns the generation ID so the frontend can poll for updates
───────────────────────────────────────────────────────────────────────────── */
const FREE_TIER_MONTHLY_LIMIT = 5;

/**
 * The submit actions start the Engine straight away, so they build the free tier
 * only. A paid tier is started by the Stripe webhook once the payment completes
 * (createPendingGeneration, then createCheckoutSession). The tier is an argument
 * from the browser: without this check, calling a submit action with tier 3 built
 * the paid product for nothing. The Engine refuses it too.
 */
const PAID_TIER_NEEDS_CHECKOUT = "Paid tiers start at checkout. Choose a plan to continue.";

/** UTC start of the current calendar month — matches the quota trigger's
 *  date_trunc('month', now(), 'UTC'). */
function currentMonthStartUtc(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
}

/** UTC start of next calendar month — when the free quota resets. */
function nextMonthStartUtc(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
}

/** Human label for the reset date, e.g. "June 1". */
function nextResetLabel(): string {
  return nextMonthStartUtc().toLocaleDateString("en-US", {
    month: "long",
    day: "numeric",
    timeZone: "UTC",
  });
}

/** Count a user's Spark (tier 0) generations this month, excluding failed runs —
 *  the same predicate as the enforce_free_generation_quota DB trigger. */
async function countFreeGenerationsThisMonth(userId: string): Promise<number> {
  try {
    return await getDataStore().countFreeGenerationsThisMonth(userId, currentMonthStartUtc());
  } catch (error) {
    // Fail open: the DB trigger is the authoritative gate, so a transient count
    // error must not block a legitimate build (the trigger still rejects abuse).
    console.error("[countFreeGenerationsThisMonth] count failed:", error);
    return 0;
  }
}

/**
 * Creates a generation row for the signed-in user: the step every creation
 * shares. `tag` names the calling action in the logs and `saveError` is the
 * message that action shows when a write fails.
 */
async function createGenerationRow(
  tag: string,
  row: Omit<NewGeneration, "user_id">,
  saveError: string
): Promise<{ user: SessionUser; generation: Generation } | { error: string }> {
  // Every creation is tied to an account so the per-account free-tier quota can
  // be enforced.
  const user = await getSessionUser();
  if (!user) {
    return { error: "Please sign in to start a build." };
  }

  try {
    // qavren-db has no auth.users trigger creating profiles, and the generations
    // FK needs the row first.
    await getDataStore().ensureProfile({ id: user.id, email: user.email ?? "" });
  } catch (error) {
    console.error(`[${tag}] Profile insert error:`, error);
    return { error: saveError };
  }

  // Free-tier quota pre-check: a friendly message before the DB trigger's hard
  // rejection. The trigger (drizzle/0001_functions.sql) is the authoritative gate.
  if (row.tier === 0) {
    const used = await countFreeGenerationsThisMonth(user.id);
    if (used >= FREE_TIER_MONTHLY_LIMIT) {
      return {
        error: `You've used all ${FREE_TIER_MONTHLY_LIMIT} free builds this month. Upgrade to unlock downloads, or wait until ${nextResetLabel()} when your free builds reset.`,
      };
    }
  }

  try {
    const generation = await getDataStore().insertGeneration({ ...row, user_id: user.id });
    return { user, generation };
  } catch (error) {
    console.error(`[${tag}] Insert error:`, error);
    return { error: saveError };
  }
}

/**
 * Asks the Engine to build a generation. Resolves with null when the Engine
 * could not be reached: the row stays pending, so the caller still succeeds and
 * the build can be retried.
 */
async function requestBuild(
  tag: string,
  payload: EngineGenerateRequest,
  userId: string
): Promise<Response | null> {
  try {
    return await postToEngine("/api/generate", payload, { userId });
  } catch (error) {
    console.error(`[${tag}] Failed to reach engine:`, error);
    return null;
  }
}

/**
 * The free build both submit actions end in: the row, then the Engine. An
 * Engine refusal is logged, not returned: the row exists and the result page
 * shows its state.
 */
async function startFreeGeneration(
  tag: string,
  input: {
    mode: "simple" | "advanced";
    prompt: string;
    schema: GenerationSchema | null;
    projectType: ProjectType;
    personalization?: PersonalizationData;
  },
  saveError: string
): Promise<SubmitGenerationResponse> {
  const { mode, prompt, schema, projectType, personalization } = input;

  const created = await createGenerationRow(
    tag,
    {
      mode,
      tier: 0,
      prompt,
      project_type: projectType,
      schema_json: schema,
      personalization_json: personalization ?? null,
    },
    saveError
  );
  if ("error" in created) {
    return { success: false, error: created.error };
  }

  const generationId = created.generation.id;
  const res = await requestBuild(
    tag,
    {
      generationId,
      mode,
      tier: 0,
      projectType,
      // Advanced builds are described by the schema; the prompt is only its summary.
      ...(schema ? { schema } : { prompt }),
      personalization: personalization ?? DEFAULT_PERSONALIZATION,
    },
    created.user.id
  );
  if (res && !res.ok) {
    console.error(`[${tag}] Engine error:`, await res.text());
  }

  return {
    success: true,
    generationId,
    redirectUrl: `/generate/${generationId}`,
  };
}

export async function submitSimpleGeneration(
  prompt: string,
  tier: Tier,
  projectType: ProjectType = "DotNetNextJs",
  personalization?: PersonalizationData
): Promise<SubmitGenerationResponse> {
  const sanitized = sanitizePrompt(prompt);
  if (sanitized.length < 10) {
    return { success: false, error: "Please provide a more detailed description (at least 10 characters)." };
  }
  prompt = sanitized;

  if (isDemoMode || !hasDataStoreConfig() || !hasEngineConfig()) {
    const generationId = `demo-simple-${Date.now()}`;
    return {
      success: true,
      generationId,
      redirectUrl: `/generate/${generationId}?demo=1&tier=${tier}`,
    };
  }

  if (tier !== 0) {
    return { success: false, error: PAID_TIER_NEEDS_CHECKOUT };
  }

  return startFreeGeneration(
    "submitSimpleGeneration",
    { mode: "simple", prompt, schema: null, projectType, personalization },
    "Failed to create generation record. Please try again."
  );
}

/* ─────────────────────────────────────────────────────────────────────────────
   submitAdvancedGeneration
   Called from the Advanced Wizard when the user clicks "Proceed to Checkout".
   Saves the full schema (entities, relationships, endpoints) to the data
   store and kicks off the Engine pipeline.
───────────────────────────────────────────────────────────────────────────── */
export async function submitAdvancedGeneration(
  schema: GenerationSchema,
  tier: Tier,
  projectType: ProjectType = "DotNetNextJs",
  personalization?: PersonalizationData
): Promise<SubmitGenerationResponse> {
  // Input validation runs BEFORE the demo short-circuit so a malformed schema
  // is rejected the same way in demo mode as in a live generation.
  if (!schema.entities || schema.entities.length === 0) {
    return { success: false, error: "Please define at least one entity before proceeding." };
  }

  for (const entity of schema.entities) {
    if (!entity.name.trim()) {
      return { success: false, error: "All entities must have a name." };
    }
  }

  if (isDemoMode || !hasDataStoreConfig() || !hasEngineConfig()) {
    const generationId = `demo-advanced-${Date.now()}`;
    return {
      success: true,
      generationId,
      redirectUrl: `/generate/${generationId}?demo=1&tier=${tier}`,
    };
  }

  if (tier !== 0) {
    return { success: false, error: PAID_TIER_NEEDS_CHECKOUT };
  }

  // Build a human-readable prompt summary from the schema
  const promptSummary =
    schema.entities.map((e) => e.name).join(", ") +
    ` — ${schema.entities.length} entities, ${schema.relationships.length} relationships, ${schema.endpoints.length} endpoints`;

  return startFreeGeneration(
    "submitAdvancedGeneration",
    { mode: "advanced", prompt: promptSummary, schema, projectType, personalization },
    "Failed to save your schema. Please try again."
  );
}

/* ─────────────────────────────────────────────────────────────────────────────
   getFreeQuotaStatus
   How many of the 5 monthly free builds an account has left. Surfaced on the
   home page and dashboard. Falls back to a full quota when there's no account
   / no data store (demo) — the UI just shows "5 of 5" and the real gate (the DB
   trigger) never fires for those paths anyway.
───────────────────────────────────────────────────────────────────────────── */
export interface FreeQuotaStatus {
  limit: number;
  used: number;
  remaining: number;
  resetsAt: string; // ISO — first of next month, UTC
  resetsAtLabel: string; // e.g. "June 1"
}

export async function getFreeQuotaStatus(): Promise<FreeQuotaStatus> {
  const full: FreeQuotaStatus = {
    limit: FREE_TIER_MONTHLY_LIMIT,
    used: 0,
    remaining: FREE_TIER_MONTHLY_LIMIT,
    resetsAt: nextMonthStartUtc().toISOString(),
    resetsAtLabel: nextResetLabel(),
  };

  if (isDemoMode || !hasDataStoreConfig()) {
    return full;
  }

  const user = await getSessionUser();
  if (!user) {
    return full;
  }

  const used = await countFreeGenerationsThisMonth(user.id);
  return {
    ...full,
    used,
    remaining: Math.max(0, FREE_TIER_MONTHLY_LIMIT - used),
  };
}

/* ─────────────────────────────────────────────────────────────────────────────
   getGeneration
   Fetches one generation by ID for the signed-in user: owner-only (decided
   2026-09-30). The /generate/[id] page and the status watcher's polling both
   read through here, and as a server action it is callable from any page with
   any id, so the session is the only input it trusts. Signed out, someone
   else's id, or an unknown id: all null, the same "not found" page.
   Safe to use in Server Components.
───────────────────────────────────────────────────────────────────────────── */
export async function getGeneration(generationId: string, demoTier?: Tier) {
  const isExplicitDemoFlow = generationId.startsWith("demo-");

  if (isExplicitDemoFlow && (isDemoMode || !hasDataStoreConfig())) {
    const tier = demoTier ?? 0;
    return buildDemoGeneration(generationId, tier);
  }

  if (!hasDataStoreConfig()) {
    return null;
  }

  try {
    const user = await getSessionUser();
    if (!user) return null;
    return await getDataStore().getGenerationForUser(generationId, user.id);
  } catch (error) {
    console.error("[getGeneration] Error:", error);
    return null;
  }
}

/* ─────────────────────────────────────────────────────────────────────────────
   retryGeneration
   Asks the Engine to retry a failed generation (up to 3 attempts).
───────────────────────────────────────────────────────────────────────────── */
export async function retryGeneration(
  generationId: string
): Promise<{ success: boolean; error?: string }> {
  if (isDemoMode || !hasDataStoreConfig() || !hasEngineConfig()) {
    return { success: true };
  }

  // Require authentication + ownership: this action re-fires the Engine (real
  // LLM spend), and the store has no row-level security — without this check
  // anyone holding a generation UUID could retry another user's build.
  const user = await getSessionUser();
  if (!user) {
    return { success: false, error: "Please sign in to retry a build." };
  }

  let gen: Generation | null;
  try {
    // Scoped to the caller in the query: someone else's id reads as not found.
    gen = await getDataStore().getGenerationForUser(generationId, user.id);
  } catch (error) {
    console.error("[retryGeneration] Lookup error:", error);
    return { success: false, error: "Generation not found." };
  }

  if (!gen) {
    return { success: false, error: "Generation not found." };
  }

  // Defence in depth on top of the scoped read, with the same message as
  // not-found so the response is not an existence oracle for other users' ids.
  if (gen.user_id !== user.id) {
    return { success: false, error: "Generation not found." };
  }

  if (gen.attempt_count >= 3) {
    return { success: false, error: "Maximum retry attempts (3) reached. Please start a new generation." };
  }

  if (gen.status !== "failed") {
    return { success: false, error: "Only failed generations can be retried." };
  }

  // Reset status. The UPDATE is an atomic compare-and-set scoped to the owner's
  // failed row, so it protects the write and the spend: the Engine enqueues
  // unconditionally, and of two concurrent retries only one flips the row.
  try {
    const reset = await getDataStore().resetForRetry(generationId, user.id);
    if (!reset) {
      return { success: false, error: "Only failed generations can be retried." };
    }
  } catch (err) {
    console.error("[retryGeneration] Status reset failed:", err);
    return { success: false, error: "Only failed generations can be retried." };
  }

  // Re-fire the engine
  const res = await requestBuild(
    "retryGeneration",
    {
      generationId,
      mode: gen.mode,
      tier: gen.tier as Tier,
      projectType: gen.project_type ?? "DotNetNextJs",
      prompt: gen.prompt ?? undefined,
      schema: gen.schema_json ?? undefined,
      personalization: gen.personalization_json ?? DEFAULT_PERSONALIZATION,
    },
    user.id
  );
  // 402: a paid build with no completed payment behind it (refunded, disputed).
  // The Engine has failed the row again with this same message.
  if (res?.status === 402) {
    const body = await res.json().catch(() => ({}));
    return {
      success: false,
      error: typeof body.error === "string" ? body.error : "This build has no completed payment.",
    };
  }

  return { success: true };
}

/* ─────────────────────────────────────────────────────────────────────────────
   createPendingGeneration
   Creates a generation row in the data store with status=pending but does NOT fire
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
  if (isDemoMode || !hasDataStoreConfig()) {
    return { success: true, generationId: `demo-pending-${Date.now()}` };
  }

  const promptSummary =
    schema
      ? schema.entities.map((e) => e.name).join(", ") +
        ` — ${schema.entities.length} entities`
      : (prompt ?? "").trim();

  const created = await createGenerationRow(
    "createPendingGeneration",
    {
      mode,
      tier,
      prompt: promptSummary || null,
      project_type: projectType,
      schema_json: schema ?? null,
      personalization_json: personalization ?? null,
    },
    "Failed to create generation record. Please try again."
  );
  if ("error" in created) {
    return { success: false, error: created.error };
  }

  return { success: true, generationId: created.generation.id };
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
  mode: "simple" | "advanced" = "advanced",
  cancelPath?: string
): Promise<{ success: true; sessionUrl: string } | { success: false; error: string }> {
  if (tier === 0) {
    return { success: false, error: "Tier 0 (Spark) is free — no checkout required." };
  }

  // Demo / no-Stripe fallback: skip payment and redirect to generate page directly.
  if (isDemoMode || !hasStripeConfig() || !hasEngineConfig()) {
    return { success: true, sessionUrl: `/generate/${generationId}?demo=1&tier=${tier}` };
  }

  // Owner-only, like the result page an upgrade starts from: a checkout acts on
  // an existing generation row, and a server action takes that id from the
  // browser. Someone else's id reads exactly like an unknown one.
  const user = await getSessionUser();
  if (!user) {
    return { success: false, error: "Please sign in to continue to checkout." };
  }
  // Without a store the row cannot be verified, so refuse. Only a misconfigured
  // deploy gets here (a dev setup without one is in demo mode, handled above).
  if (!hasDataStoreConfig()) {
    console.error("[createCheckoutSession] No data store configured: cannot verify the generation's owner.");
    return { success: false, error: "Generation not found." };
  }
  let owned: Generation | null = null;
  try {
    owned = await getDataStore().getGenerationForUser(generationId, user.id);
  } catch (error) {
    console.error("[createCheckoutSession] Lookup error:", error);
  }
  if (!owned) {
    return { success: false, error: "Generation not found." };
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
    // An explicit cancelPath wins (e.g. the try-before-buy upgrade returns to the
    // existing /generate/[id] result page — NOT back to /simple, which would
    // spawn a brand-new free build). Otherwise fall back to the mode default.
    const modeDefault =
      mode === "simple"
        ? `/simple?q=${encodeURIComponent(prompt ?? "")}&tier=${tier}`
        : `/advanced?step=4&tier=${tier}&projectType=${projectType}`;
    // cancelPath comes from the browser and is appended to the origin, so it must
    // be a path on this site: "@evil.example" or ".evil.example" would otherwise
    // turn the cancel link on our Stripe checkout page into someone else's host.
    const resolvedCancelPath = safeReturnTo(cancelPath, modeDefault);

    // Stripe answers within its own limits, which are longer than the default here.
    const res = await postToEngine(
      "/api/stripe/create-session",
      {
        generationId,
        tier,
        projectType,
        prompt: prompt ?? "",
        successUrl: `${origin}/generate/${generationId}?session_id={CHECKOUT_SESSION_ID}`,
        cancelUrl: `${origin}${resolvedCancelPath}`,
      },
      { userId: user.id, timeoutMs: 30_000 }
    );

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
   Returns an empty array when the visitor is anonymous or no data store is
   configured.
───────────────────────────────────────────────────────────────────────────── */
export async function getMyGenerations(
  page = 1,
  pageSize = 20
): Promise<{ generations: GenerationSummary[]; total: number }> {
  const empty = { generations: [] as GenerationSummary[], total: 0 };

  const user = await getSessionUser();
  if (!user) return empty;
  if (!hasDataStoreConfig()) return empty;

  const safePage = Number.isFinite(page) && page > 0 ? Math.floor(page) : 1;
  // Same treatment for the size: an integer in [1, 100], default 20, so a caller
  // can't hand NaN or 10_000 to the store's limit.
  const safeSize = Number.isFinite(pageSize) ? Math.min(100, Math.max(1, Math.floor(pageSize))) : 20;
  const offset = (safePage - 1) * safeSize;

  // The store returns the full match count alongside the page window, so the
  // caller can render pagination without a second query.
  try {
    return await getDataStore().listMyGenerations(user.id, offset, safeSize);
  } catch (err) {
    console.error("[getMyGenerations] Query error:", err);
    return empty;
  }
}

/**
 * Aggregate generation counts for the dashboard stat cards. Independent of the
 * paged list so the totals are accurate regardless of which page is shown
 * (previously the stats were derived from a 50-row-capped list).
 */
export async function getGenerationStats(): Promise<{
  total: number;
  completed: number;
  inProgress: number;
}> {
  const empty = { total: 0, completed: 0, inProgress: 0 };

  const user = await getSessionUser();
  if (!user || !hasDataStoreConfig()) return empty;

  try {
    return await getDataStore().generationStats(user.id);
  } catch (err) {
    console.error("[getGenerationStats] Count error:", err);
    return empty;
  }
}
