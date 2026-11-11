import { getDataStore } from "@/lib/data";
import { postToEngine } from "@/lib/engine-client";
import { hasDataStoreConfig, hasEngineConfig, isDemoMode } from "@/lib/runtime-config";
import { getSessionUser } from "@/lib/session";

/**
 * GET /api/download/{generationId}: sends the owner of a delivered paid build
 * to a download link made for this click.
 *
 * The link the Engine stores on the row is signed once and stops working after
 * seven days, the longest a presigned link can last. The Download buttons used
 * to point straight at it, so a customer who came back in week two got a dead
 * link under a page that still said "Ready". The archive is still in the
 * bucket; this route asks the Engine to sign it again.
 */

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const STORED_LINK_LIFETIME_MS = 7 * 24 * 60 * 60 * 1000;

/** A relative Location, so the redirect never depends on the Host the proxy saw. */
function redirectTo(location: string): Response {
  return new Response(null, {
    status: 302,
    headers: { Location: location, "Cache-Control": "no-store", "Referrer-Policy": "no-referrer" },
  });
}

function unavailable(status: 404 | 503): Response {
  const message =
    status === 404
      ? "This download is not available."
      : "The download could not be prepared. Please try again in a moment.";
  return new Response(message, {
    status,
    headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store" },
  });
}

export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }): Promise<Response> {
  const { id } = await params;

  if (isDemoMode || !hasDataStoreConfig() || !hasEngineConfig()) {
    return redirectTo("/demo-download.zip");
  }

  // Checked first: the id goes into a redirect and into the Engine's object key.
  if (!UUID.test(id)) {
    return unavailable(404);
  }

  const user = await getSessionUser();
  if (!user) {
    return redirectTo(`/login?returnTo=${encodeURIComponent(`/generate/${id}`)}`);
  }

  let generation;
  try {
    // Owner-scoped in the query: someone else's id reads as not found.
    generation = await getDataStore().getGenerationForUser(id, user.id);
  } catch (error) {
    console.error("[download] Lookup error:", error);
    return unavailable(503);
  }

  if (!generation || generation.status !== "success" || generation.tier === 0) {
    return unavailable(404);
  }

  try {
    const res = await postToEngine("/api/download-url", { generationId: id }, { userId: user.id });
    if (res.ok) {
      const body: unknown = await res.json().catch(() => null);
      const url = (body as { url?: unknown } | null)?.url;
      if (typeof url === "string" && url.startsWith("https://")) {
        return redirectTo(url);
      }
    }
    console.error("[download] Engine did not return a link:", res.status);
  } catch (error) {
    console.error("[download] Failed to reach engine:", error);
  }

  // The Engine could not sign a new link (it restarts on every deploy). The link
  // stored at delivery still works for its first seven days, which is when most
  // downloads happen.
  const completedAt = generation.completed_at ? Date.parse(generation.completed_at) : NaN;
  if (generation.download_url && Date.now() - completedAt < STORED_LINK_LIFETIME_MS) {
    return redirectTo(generation.download_url);
  }

  return unavailable(503);
}
