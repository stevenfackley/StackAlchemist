// CI dependency-vulnerability gate. Replaces a bare `npm audit
// --audit-level=moderate`, which we cannot use on its own: an advisory with no
// upstream fix (see audit-allowlist.json) would fail every run forever, and the
// usual escape hatch — dropping the audit or running it with --omit=dev — would
// stop gating production dependencies too.
//
// Instead: fail on every moderate-or-worse advisory except the ones explicitly
// allowlisted, and keep those honest by failing when an entry expires, starts
// affecting a production dependency, or no longer matches anything.

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { spawnSync } from "node:child_process";

const THRESHOLD = "moderate";
const SEVERITY_RANK = { info: 0, low: 1, moderate: 2, high: 3, critical: 4 };
const ALLOWLIST_PATH = resolve(process.cwd(), "audit-allowlist.json");

const inActions = Boolean(process.env.GITHUB_ACTIONS);
const problems = [];

/** Emits a GitHub Actions annotation in CI, a plain line locally. */
function fail(message) {
  problems.push(message);
  console.error(inActions ? `::error::${message}` : `ERROR: ${message}`);
}

/**
 * `npm audit` exits non-zero whenever it finds anything, so the exit code says
 * nothing about whether the run itself worked — the parsed payload does.
 */
function runAudit(extraArgs) {
  const args = ["audit", "--json", ...extraArgs];
  const result = spawnSync("npm", args, {
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
    shell: process.platform === "win32",
  });

  let report;
  try {
    report = JSON.parse(result.stdout);
  } catch {
    console.error(result.stderr || result.stdout);
    throw new Error(`\`npm ${args.join(" ")}\` did not return a JSON report.`);
  }

  if (report.error) {
    throw new Error(
      `\`npm ${args.join(" ")}\` failed: ${report.error.summary ?? "unknown error"}`,
    );
  }

  return report;
}

/** GHSA id, taken from the advisory URL and falling back to the numeric source. */
function advisoryId(via) {
  const fromUrl = typeof via.url === "string" ? via.url.split("/").pop() : null;
  return fromUrl || (via.source != null ? String(via.source) : null);
}

/**
 * Collapses the report to one entry per advisory. npm repeats each advisory
 * across every affected package, and lists dependents as plain-string `via`
 * entries; those are consequences of a root advisory, not separate findings.
 */
function collectAdvisories(report) {
  const advisories = new Map();

  for (const vuln of Object.values(report.vulnerabilities ?? {})) {
    for (const via of vuln.via ?? []) {
      if (typeof via !== "object") continue;

      const id = advisoryId(via);
      if (!id) continue;

      const existing = advisories.get(id);
      if (existing) {
        existing.packages.add(vuln.name);
        continue;
      }

      advisories.set(id, {
        id,
        title: via.title ?? "(no title)",
        severity: via.severity ?? "unknown",
        range: via.range ?? "",
        url: via.url ?? "",
        packages: new Set([vuln.name]),
      });
    }
  }

  return advisories;
}

async function readAllowlist() {
  let raw;
  try {
    raw = await readFile(ALLOWLIST_PATH, "utf8");
  } catch (error) {
    if (error.code === "ENOENT") return [];
    throw error;
  }

  const parsed = JSON.parse(raw);
  const entries = parsed.allow ?? [];

  for (const entry of entries) {
    for (const field of ["advisory", "package", "expires", "reason"]) {
      if (!entry[field]) {
        fail(`audit-allowlist.json: entry ${entry.advisory ?? "(unnamed)"} is missing "${field}".`);
      }
    }
    if (entry.scope !== "dev") {
      fail(
        `audit-allowlist.json: entry ${entry.advisory} has scope "${entry.scope ?? "(unset)"}"; ` +
          `only "dev" exceptions are accepted.`,
      );
    }
  }

  return entries;
}

const meetsThreshold = (severity) =>
  (SEVERITY_RANK[severity] ?? SEVERITY_RANK.critical) >= SEVERITY_RANK[THRESHOLD];

const report = runAudit([]);
const prodReport = runAudit(["--omit=dev"]);

const advisories = collectAdvisories(report);
const prodAdvisories = collectAdvisories(prodReport);
const allowlist = await readAllowlist();
const allowed = new Map(allowlist.map((entry) => [entry.advisory, entry]));

// Today at UTC midnight, so an expiry date is compared by day and not by the
// hour the nightly happens to run.
const today = new Date(new Date().toISOString().slice(0, 10));

for (const advisory of advisories.values()) {
  if (!meetsThreshold(advisory.severity)) continue;

  const entry = allowed.get(advisory.id);
  const packages = [...advisory.packages].sort().join(", ");
  const label = `${advisory.id} (${advisory.severity}, ${packages}): ${advisory.title}`;

  if (!entry) {
    fail(`Unresolved advisory ${label}`);
    continue;
  }

  if (prodAdvisories.has(advisory.id)) {
    fail(
      `Advisory ${advisory.id} is allowlisted as dev-only but now affects a production ` +
        `dependency. Fix it — the exception no longer applies.`,
    );
    continue;
  }

  if (!advisory.packages.has(entry.package)) {
    fail(
      `Advisory ${advisory.id} is allowlisted for "${entry.package}" but now affects ` +
        `${packages}. Re-review the exception.`,
    );
    continue;
  }

  const expires = new Date(entry.expires);
  if (Number.isNaN(expires.getTime())) {
    fail(`audit-allowlist.json: entry ${advisory.id} has an unparseable expires "${entry.expires}".`);
    continue;
  }

  if (expires < today) {
    fail(
      `Allowlist entry for ${advisory.id} expired on ${entry.expires}. Re-check whether ` +
        `upstream shipped a fix, then either resolve it or extend the entry deliberately.`,
    );
    continue;
  }

  console.log(`Allowed until ${entry.expires} — ${label}`);
}

// An entry that stops matching means upstream fixed it. Fail so the exception
// gets deleted instead of quietly outliving the problem it was written for.
for (const entry of allowlist) {
  if (!entry.advisory) continue;
  if (!advisories.has(entry.advisory)) {
    fail(
      `Allowlist entry for ${entry.advisory} (${entry.package}) no longer matches any ` +
        `advisory — it is resolved. Remove it from audit-allowlist.json.`,
    );
  }
}

// `metadata.vulnerabilities` carries a "total" key alongside the severities;
// only count keys that are actually severities.
const counts = report.metadata?.vulnerabilities ?? {};
const gated = Object.entries(counts)
  .filter(([severity]) => severity in SEVERITY_RANK && meetsThreshold(severity))
  .reduce((total, [, count]) => total + count, 0);

if (problems.length > 0) {
  console.error(`\nDependency audit failed with ${problems.length} problem(s).`);
  process.exit(1);
}

console.log(
  `\nDependency audit passed: 0 unresolved advisories at ${THRESHOLD} or above. ` +
    `${gated} affected package(s), all allowlisted; 0 in production dependencies.`,
);
