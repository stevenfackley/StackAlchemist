import { describe, it, expect } from "vitest";
import {
  BUILD_REPORT_BEGIN_MARKER,
  BUILD_REPORT_END_MARKER,
  parseBuildReportSummary,
  verifiedHalves,
} from "@/lib/build-report";

function log(summary: unknown, prefix = "Build succeeded.\n"): string {
  return `${prefix}${BUILD_REPORT_BEGIN_MARKER}\n${JSON.stringify(summary)}\n${BUILD_REPORT_END_MARKER}`;
}

const verified = {
  schemaVersion: 1,
  status: "verified",
  attemptsUsed: 1,
  maxAttempts: 3,
  halves: [
    { half: "dotnet", label: ".NET", status: "passed" },
    { half: "nextjs", label: "Next.js", status: "passed" },
  ],
};

describe("parseBuildReportSummary", () => {
  it("reads the verdict the Engine appends to build_log", () => {
    const summary = parseBuildReportSummary(log(verified));

    expect(summary).not.toBeNull();
    expect(summary!.status).toBe("verified");
    expect(summary!.attemptsUsed).toBe(1);
    expect(summary!.halves.map((h) => h.label)).toEqual([".NET", "Next.js"]);
  });

  it("uses the LAST block when a retried build streamed more than one", () => {
    // The archive holds the code from the final attempt, so the final verdict is the
    // one that describes what the customer received.
    const failedFirst = { ...verified, status: "failed", attemptsUsed: 1 };
    const combined = `${log(failedFirst)}\n${log({ ...verified, attemptsUsed: 2 })}`;

    const summary = parseBuildReportSummary(combined);

    expect(summary!.status).toBe("verified");
    expect(summary!.attemptsUsed).toBe(2);
  });

  it("reports a failed frontend half rather than flattening it to an overall failure", () => {
    const summary = parseBuildReportSummary(
      log({
        ...verified,
        status: "failed",
        halves: [
          { half: "dotnet", label: ".NET", status: "passed" },
          { half: "nextjs", label: "Next.js", status: "failed" },
        ],
      })
    );

    expect(summary!.halves.find((h) => h.half === "nextjs")!.status).toBe("failed");
    expect(verifiedHalves(summary).map((h) => h.half)).toEqual(["dotnet"]);
  });

  it("carries whatever halves the stack has, without an allowlist of stack names", () => {
    // A FastAPI + React generation reports `python` / `react`. Nothing in the parser or the
    // panel may assume .NET + Next.js — assuming it is what shipped those customers a report
    // about a stack they never chose.
    const summary = parseBuildReportSummary(
      log({
        ...verified,
        halves: [
          { half: "python", label: "FastAPI", status: "passed" },
          { half: "react", label: "React", status: "failed" },
        ],
      })
    );

    expect(summary!.halves.map((h) => h.label)).toEqual(["FastAPI", "React"]);
    expect(verifiedHalves(summary).map((h) => h.half)).toEqual(["python"]);
  });

  it.each([
    ["no log at all", null],
    ["a log with no report block", "Build succeeded.\nBUILD SUCCEEDED"],
    ["a malformed JSON payload", `${BUILD_REPORT_BEGIN_MARKER}\n{ not json\n${BUILD_REPORT_END_MARKER}`],
    ["an unrecognised top-level status", log({ ...verified, status: "probably-fine" })],
  ])("returns null for %s rather than assuming success", (_label, input) => {
    // The whole point: absence of evidence must never render as verified.
    expect(parseBuildReportSummary(input as string | null)).toBeNull();
  });

  it("drops halves whose status is not a value it understands", () => {
    const summary = parseBuildReportSummary(
      log({
        ...verified,
        halves: [
          { half: "dotnet", label: ".NET", status: "passed" },
          { half: "nextjs", label: "Next.js", status: "probably-compiled" },
        ],
      })
    );

    expect(summary!.halves.map((h) => h.half)).toEqual(["dotnet"]);
  });

  it("tolerates a truncated log that lost its closing marker", () => {
    // build_log is appended in chunks and can be read mid-write.
    const truncated = `${BUILD_REPORT_BEGIN_MARKER}\n${JSON.stringify(verified)}`;

    expect(parseBuildReportSummary(truncated)!.status).toBe("verified");
  });

  it("falls back to the wire name when a half has no label", () => {
    const summary = parseBuildReportSummary(
      log({ ...verified, halves: [{ half: "dotnet", status: "passed" }] })
    );

    expect(summary!.halves[0].label).toBe("dotnet");
  });
});
