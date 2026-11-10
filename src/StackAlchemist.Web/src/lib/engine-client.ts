import { getEngineServiceKey } from "./runtime-config";

/**
 * The Engine endpoints called from here answer quickly: /api/generate only
 * queues the build, and a checkout is one Stripe call. The limit bounds a hung
 * connection so a server action cannot wait on one indefinitely.
 */
const DEFAULT_TIMEOUT_MS = 15_000;

export interface EngineCallOptions {
  /**
   * The signed-in user the call is made for. The Engine rate-limits per user on
   * it (every call comes from this server, so the IP tells it nothing). Without
   * it the call falls into a window shared by all such calls.
   */
  userId?: string;
  timeoutMs?: number;
}

export function resolveEngineUrl(): string {
  const configuredUrl = process.env.ENGINE_API_URL;
  if (configuredUrl) {
    return configuredUrl;
  }

  if (process.env.NODE_ENV !== "production") {
    return "http://localhost:5000";
  }

  throw new Error("ENGINE_API_URL must be configured in production.");
}

/**
 * POSTs JSON to the Engine with the service key and the caller's user id.
 * Resolves with the response whatever its status; rejects when the Engine
 * cannot be reached or does not answer within the timeout.
 */
export async function postToEngine(
  path: string,
  body: unknown,
  { userId, timeoutMs = DEFAULT_TIMEOUT_MS }: EngineCallOptions = {}
): Promise<Response> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const key = getEngineServiceKey();
  if (key) headers["X-Engine-Key"] = key;
  if (userId) headers["X-Engine-User"] = userId;

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(`${resolveEngineUrl()}${path}`, {
      method: "POST",
      headers,
      body: JSON.stringify(body),
      signal: controller.signal,
    });
  } finally {
    clearTimeout(timer);
  }
}
