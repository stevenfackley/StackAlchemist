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

  // ── What gets logged (#432) ──────────────────────────────────────────────
  // The report's document-uri for /simple?q=<prompt> carries the user's prompt,
  // and other fields (referrer, script-sample, original-policy) are either PII
  // or noise. Only an allowlist is logged, URLs lose their query and fragment,
  // and every field is length-capped.

  function loggedLine(): string {
    const warn = vi.mocked(console.warn);
    expect(warn).toHaveBeenCalledTimes(1);
    const [tag, line] = warn.mock.calls[0];
    expect(tag).toBe("[csp-report]");
    return String(line);
  }

  it("logs the directive, the blocked URL and the document path, never a query string", async () => {
    const payload = JSON.stringify({
      "csp-report": {
        "document-uri": "https://stackalchemist.app/simple?q=my%20secret%20startup%20idea#frag",
        "blocked-uri": "https://evil.example/x.js?token=abc123",
        "effective-directive": "script-src-elem",
        disposition: "report",
      },
    });
    await POST(makeRequest(payload));
    const line = loggedLine();
    expect(JSON.parse(line)).toEqual({
      directive: "script-src-elem",
      blocked: "https://evil.example/x.js",
      document: "/simple",
      disposition: "report",
    });
    expect(line).not.toMatch(/secret|token|abc123|q=|frag/);
  });

  it("drops every field outside the allowlist", async () => {
    const payload = JSON.stringify({
      "csp-report": {
        "document-uri": "https://stackalchemist.app/dashboard",
        "effective-directive": "img-src",
        "blocked-uri": "https://cdn.example/a.png",
        referrer: "https://www.google.com/search?q=private+query",
        "script-sample": "alert(document.cookie)",
        "original-policy": "default-src 'self'; script-src 'self'",
      },
    });
    await POST(makeRequest(payload));
    const line = loggedLine();
    expect(line).not.toMatch(/google|private|alert|cookie|default-src/);
  });

  it("keeps CSP keyword values and reduces data:/blob: URLs to their scheme", async () => {
    await POST(makeRequest(JSON.stringify({ "csp-report": { "effective-directive": "script-src", "blocked-uri": "inline" } })));
    expect(JSON.parse(loggedLine()).blocked).toBe("inline");
    vi.mocked(console.warn).mockClear();
    await POST(makeRequest(JSON.stringify({ "csp-report": { "effective-directive": "img-src", "blocked-uri": "data:image/png;base64,AAAA" } })));
    expect(JSON.parse(loggedLine()).blocked).toBe("data:");
  });

  it("falls back to violated-directive when effective-directive is absent", async () => {
    await POST(makeRequest(JSON.stringify({ "csp-report": { "violated-directive": "style-src", "blocked-uri": "https://x.example/s.css" } })));
    expect(JSON.parse(loggedLine()).directive).toBe("style-src");
  });

  it("caps the length of every logged field", async () => {
    const long = "https://evil.example/" + "a".repeat(5000);
    await POST(makeRequest(JSON.stringify({ "csp-report": { "effective-directive": "x".repeat(5000), "blocked-uri": long } })));
    const logged = JSON.parse(loggedLine()) as Record<string, string>;
    for (const value of Object.values(logged)) expect(value.length).toBeLessThanOrEqual(512);
  });

  it("summarizes a flat (unwrapped) report the same way", async () => {
    await POST(makeRequest(JSON.stringify({
      "document-uri": "https://stackalchemist.app/simple?q=secret",
      "effective-directive": "font-src",
      "blocked-uri": "https://fonts.example/f.woff2?v=2",
    })));
    expect(JSON.parse(loggedLine())).toEqual({
      directive: "font-src",
      blocked: "https://fonts.example/f.woff2",
      document: "/simple",
    });
  });

  it("ignores array bodies (Reporting API format is not used by this policy)", async () => {
    await POST(makeRequest(JSON.stringify([{ type: "csp-violation", body: { documentURL: "https://x/?q=secret" } }])));
    expect(console.warn).not.toHaveBeenCalled();
  });

  it("reduces opaque document and blocked URLs to their scheme instead of logging the payload", async () => {
    await POST(makeRequest(JSON.stringify({
      "csp-report": {
        "document-uri": "data:text/html,<p>my secret prompt</p>",
        "effective-directive": "script-src",
        "blocked-uri": "blob:https://stackalchemist.app/1f2e3d4c",
      },
    })));
    const logged = JSON.parse(loggedLine());
    expect(logged).toEqual({ directive: "script-src", blocked: "blob:", document: "data:" });
    vi.mocked(console.warn).mockClear();
    await POST(makeRequest(JSON.stringify({ "csp-report": { "effective-directive": "img-src", "document-uri": "about:blank", "blocked-uri": "chrome-extension://abcdef/icon.png" } })));
    // Non-special schemes have an opaque ("null") origin in WHATWG URL, so both reduce to the scheme.
    expect(JSON.parse(loggedLine())).toEqual({ directive: "img-src", blocked: "chrome-extension:", document: "about:" });
  });

  it("caps keyword-like fields (disposition, CSP keywords) at 64 characters", async () => {
    await POST(makeRequest(JSON.stringify({ "csp-report": { "effective-directive": "script-src", "blocked-uri": "k".repeat(500), disposition: "d".repeat(500) } })));
    const logged = JSON.parse(loggedLine());
    expect(logged.blocked.length).toBe(64);
    expect(logged.disposition.length).toBe(64);
  });

  it("logs nothing when the report has none of the allowlisted fields", async () => {
    await POST(makeRequest(JSON.stringify({ "csp-report": { referrer: "https://www.google.com/?q=x" } })));
    expect(console.warn).not.toHaveBeenCalled();
  });
});
