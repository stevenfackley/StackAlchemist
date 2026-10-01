import { NextResponse } from "next/server";

// CSP violation report sink. Receives the `csp-report` body browsers send
// (via `report-uri`) when they block a resource under our
// Content-Security-Policy-Report-Only header. Logs to stdout only — no DB —
// so we can eyeball the allowlist before flipping CSP to enforce.
//
// What is logged is an allowlist, not the report (#432): the report's
// `document-uri` for `/simple?q=<prompt>` carries the user's prompt, and
// `referrer`, `script-sample` and `original-policy` are PII or noise. URLs lose
// their query string and fragment, the document URL keeps only its path, and
// every field is length-capped. nginx already caps this request body at 16k.

const MAX_BODY_CHARS = 16_384;
const MAX_FIELD_CHARS = 512;
const MAX_KEYWORD_CHARS = 64;

type Report = Record<string, unknown>;

/** Origin + path of an absolute URL; `data:`/`blob:` → scheme; CSP keywords ("inline", "eval") pass through, short. */
function stripUrl(value: unknown): string | undefined {
  if (typeof value !== "string" || value === "") return undefined;
  try {
    const url = new URL(value);
    if (url.origin === "null") return url.protocol; // data:, blob:, about:, …
    return (url.origin + url.pathname).slice(0, MAX_FIELD_CHARS);
  } catch {
    return value.split(/[?#]/)[0].slice(0, MAX_KEYWORD_CHARS);
  }
}

/** Path only — the origin is always ours and the query may hold a prompt. */
function pathOnly(value: unknown): string | undefined {
  if (typeof value !== "string" || value === "") return undefined;
  try {
    return new URL(value).pathname.slice(0, MAX_FIELD_CHARS);
  } catch {
    return value.split(/[?#]/)[0].slice(0, MAX_FIELD_CHARS);
  }
}

function text(value: unknown, max = MAX_FIELD_CHARS): string | undefined {
  return typeof value === "string" && value !== "" ? value.slice(0, max) : undefined;
}

function summarize(report: Report): Record<string, string> {
  const summary: Record<string, string | undefined> = {
    directive: text(report["effective-directive"]) ?? text(report["violated-directive"]),
    blocked: stripUrl(report["blocked-uri"]),
    document: pathOnly(report["document-uri"]),
    disposition: text(report.disposition, MAX_KEYWORD_CHARS),
  };
  return Object.fromEntries(
    Object.entries(summary).filter((entry): entry is [string, string] => entry[1] !== undefined)
  );
}

export async function POST(request: Request) {
  try {
    const raw = (await request.text().catch(() => "")).slice(0, MAX_BODY_CHARS);
    let body: unknown = null;
    try {
      body = JSON.parse(raw);
    } catch {
      body = null;
    }
    const report = (body as Report | null)?.["csp-report"] ?? body;
    if (report && typeof report === "object" && !Array.isArray(report)) {
      const summary = summarize(report as Report);
      if (Object.keys(summary).length > 0) {
        console.warn("[csp-report]", JSON.stringify(summary));
      }
    }
  } catch {
    // Swallow: this endpoint must never 500 or browsers drop subsequent reports.
  }
  return new NextResponse(null, { status: 204 });
}

export const runtime = "nodejs";
