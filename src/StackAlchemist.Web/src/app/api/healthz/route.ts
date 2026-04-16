/**
 * Health check endpoint used by Docker healthchecks and Cloudflare Tunnel
 * liveness probes. Always returns 200; intentionally exempt from the
 * test-site Basic Auth middleware so deployments don't break.
 */
export const dynamic = "force-dynamic";

export function GET() {
  return new Response("ok", {
    status: 200,
    headers: {
      "content-type": "text/plain; charset=utf-8",
      "cache-control": "no-store",
      // Belt-and-braces: never let health probes show up in AI/SEO indexes.
      "x-robots-tag": "noindex, nofollow",
    },
  });
}
