import { NextResponse } from "next/server";

// CSP violation report sink. Receives the `csp-report` body sent by browsers
// when they block a resource that violates our Content-Security-Policy-Report-Only
// header. Logs to stdout only — no DB, no PII retention — so we can eyeball the
// allowlist before flipping CSP to enforce in Batch C.
export async function POST(request: Request) {
  try {
    const body = await request.json().catch(() => null);
    const report = body?.["csp-report"] ?? body;
    if (report) {
      console.warn("[csp-report]", JSON.stringify(report));
    }
  } catch {
    // Swallow: this endpoint must never 500 or browsers drop subsequent reports.
  }
  return new NextResponse(null, { status: 204 });
}

export const runtime = "nodejs";
