/**
 * Tests for the Next.js Route Handler exports.
 * We import the exported GET / POST functions directly and call them — no need
 * to spin up a server or mock the Next.js framework.
 */

import { GET } from "@/app/api/healthz/route";
import { POST } from "@/app/api/csp-report/route";

// ── GET /api/healthz ────────────────────────────────────────────────────────

describe("GET /api/healthz", () => {
  it("returns 200", async () => {
    const res = await GET();
    expect(res.status).toBe(200);
  });

  it("body text is 'ok'", async () => {
    const res = await GET();
    const text = await res.text();
    expect(text).toBe("ok");
  });

  it("has cache-control: no-store", async () => {
    const res = await GET();
    expect(res.headers.get("cache-control")).toBe("no-store");
  });

  it("has x-robots-tag: noindex, nofollow", async () => {
    const res = await GET();
    expect(res.headers.get("x-robots-tag")).toBe("noindex, nofollow");
  });
});

// ── POST /api/csp-report ────────────────────────────────────────────────────

function makeRequest(body: string | null, contentType = "application/json"): Request {
  if (body === null) {
    return new Request("http://localhost/api/csp-report", { method: "POST" });
  }
  return new Request("http://localhost/api/csp-report", {
    method: "POST",
    headers: { "content-type": contentType },
    body,
  });
}

describe("POST /api/csp-report", () => {
  beforeEach(() => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("returns 204 with a valid csp-report body", async () => {
    const payload = JSON.stringify({
      "csp-report": {
        "document-uri": "https://example.com",
        "violated-directive": "script-src",
        "blocked-uri": "https://evil.com/script.js",
      },
    });
    const res = await POST(makeRequest(payload));
    expect(res.status).toBe(204);
  });

  it("returns 204 with a flat JSON body (no csp-report wrapper)", async () => {
    const payload = JSON.stringify({ "blocked-uri": "https://evil.com" });
    const res = await POST(makeRequest(payload));
    expect(res.status).toBe(204);
  });

  it("returns 204 with malformed / non-JSON body", async () => {
    const res = await POST(makeRequest("not json at all", "text/plain"));
    expect(res.status).toBe(204);
  });

  it("returns 204 with an empty body", async () => {
    const res = await POST(makeRequest(null));
    expect(res.status).toBe(204);
  });

  it("returns 204 with empty JSON object", async () => {
    const res = await POST(makeRequest("{}"));
    expect(res.status).toBe(204);
  });

  it("response body is null (no content)", async () => {
    const payload = JSON.stringify({ "csp-report": { "blocked-uri": "x" } });
    const res = await POST(makeRequest(payload));
    // 204 responses have no body
    expect(res.body).toBeNull();
  });
});
