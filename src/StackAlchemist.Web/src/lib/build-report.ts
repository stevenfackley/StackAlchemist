/**
 * Reads the Compile Guarantee verdict the Engine appends to `generations.build_log`.
 *
 * The delivery page used to print "Compile Verified" as a hardcoded string — on every paid
 * generation, including Tier 1 Blueprints that never go near a compiler, and including
 * archives whose Next.js half was never built at all. This module is what replaces that
 * claim with the recorded outcome.
 *
 * The Engine writes the block in `BuildReportWriter.ToLogSummary`; the same data, in full,
 * ships as `build-report.json` inside the customer's archive. Field names are documented in
 * docs/advanced-docs/compile-guarantee.md and are a published contract.
 *
 * FOLLOW-UP: the verdict is carried in `build_log` rather than a dedicated `build_report`
 * jsonb column purely to avoid coupling this change to a Supabase migration — the prod
 * deploy workflow hard-blocks when a push touches supabase/migrations and
 * PROD_SUPABASE_DB_URL is unset. When that column lands, read it here and keep this parser
 * as the fallback for rows generated before it.
 */

export const BUILD_REPORT_BEGIN_MARKER = "=== COMPILE GUARANTEE REPORT ===";
export const BUILD_REPORT_END_MARKER = "=== END COMPILE GUARANTEE REPORT ===";

/** Only `passed` is evidence of compilation. See compile-guarantee.md. */
export type BuildHalfStatus = "passed" | "failed" | "skipped" | "not_run";

export interface BuildHalfVerdict {
  /** `dotnet` | `nextjs` */
  half: string;
  /** Display name, e.g. ".NET" / "Next.js". */
  label: string;
  status: BuildHalfStatus;
}

export interface BuildReportSummary {
  schemaVersion: number;
  status: "verified" | "failed";
  attemptsUsed: number;
  maxAttempts: number;
  halves: BuildHalfVerdict[];
}

const HALF_STATUSES: readonly string[] = ["passed", "failed", "skipped", "not_run"];

/**
 * Extracts the verdict from a build log, or `null` if there isn't one.
 *
 * `null` is the honest answer for every generation produced before the report existed, and
 * for any log whose block is malformed. Callers must render "not recorded" in that case —
 * never fall back to assuming success, which is the bug this replaces.
 */
export function parseBuildReportSummary(buildLog: string | null | undefined): BuildReportSummary | null {
  if (!buildLog) return null;

  // A retried generation can stream the block more than once; the last one is the verdict
  // for the code that actually shipped.
  const begin = buildLog.lastIndexOf(BUILD_REPORT_BEGIN_MARKER);
  if (begin === -1) return null;

  const payloadStart = begin + BUILD_REPORT_BEGIN_MARKER.length;
  const end = buildLog.indexOf(BUILD_REPORT_END_MARKER, payloadStart);
  const payload = end === -1 ? buildLog.slice(payloadStart) : buildLog.slice(payloadStart, end);

  let parsed: unknown;
  try {
    parsed = JSON.parse(payload.trim());
  } catch {
    return null;
  }

  return toSummary(parsed);
}

/** Convenience: the halves that genuinely compiled, in report order. */
export function verifiedHalves(summary: BuildReportSummary | null): BuildHalfVerdict[] {
  return summary?.halves.filter((half) => half.status === "passed") ?? [];
}

function toSummary(value: unknown): BuildReportSummary | null {
  if (typeof value !== "object" || value === null) return null;
  const record = value as Record<string, unknown>;

  const status = record.status;
  if (status !== "verified" && status !== "failed") return null;

  const halves = Array.isArray(record.halves)
    ? record.halves.map(toHalfVerdict).filter((half): half is BuildHalfVerdict => half !== null)
    : [];

  return {
    schemaVersion: numberOr(record.schemaVersion, 0),
    status,
    attemptsUsed: numberOr(record.attemptsUsed, 0),
    maxAttempts: numberOr(record.maxAttempts, 0),
    halves,
  };
}

function toHalfVerdict(value: unknown): BuildHalfVerdict | null {
  if (typeof value !== "object" || value === null) return null;
  const record = value as Record<string, unknown>;

  const { half, label, status } = record;
  if (typeof half !== "string" || typeof status !== "string") return null;
  // An unrecognised status is not "probably fine" — refuse it rather than badge it.
  if (!HALF_STATUSES.includes(status)) return null;

  return {
    half,
    label: typeof label === "string" && label.length > 0 ? label : half,
    status: status as BuildHalfStatus,
  };
}

function numberOr(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}
