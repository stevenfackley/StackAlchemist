import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { PaidTierPanel } from "@/app/generate/[id]/components/paid-tier-panel";
import type { Generation, Tier } from "@/lib/types";

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

function buildLog(halves: { half: string; label: string; status: string }[], status = "verified"): string {
  return [
    "BUILD SUCCEEDED",
    "=== COMPILE GUARANTEE REPORT ===",
    JSON.stringify({ schemaVersion: 1, status, attemptsUsed: 1, maxAttempts: 3, halves }),
    "=== END COMPILE GUARANTEE REPORT ===",
  ].join("\n");
}

const BOTH_PASSED = [
  { half: "dotnet", label: ".NET", status: "passed" },
  { half: "nextjs", label: "Next.js", status: "passed" },
];

function makeGeneration(overrides: Partial<Generation> = {}): Generation {
  return {
    id: "gen-1",
    user_id: null,
    transaction_id: null,
    project_type: "DotNetNextJs",
    status: "success",
    mode: "advanced",
    tier: 2 as Tier,
    prompt: "Build an invoicing app",
    schema_json: null,
    download_url: "https://r2.test/archive.zip",
    preview_files_json: null,
    personalization_json: null,
    build_log: buildLog(BOTH_PASSED),
    error_message: null,
    error_category: null,
    attempt_count: 1,
    created_at: "2026-08-15T00:00:00Z",
    updated_at: "2026-08-15T00:00:00Z",
    completed_at: "2026-08-15T00:00:00Z",
    ...overrides,
  } as Generation;
}

describe("PaidTierPanel — compile verdict", () => {
  it("names both halves when both actually compiled", () => {
    render(<PaidTierPanel generation={makeGeneration()} />);

    const verdict = screen.getByTestId("compile-verdict");
    expect(verdict).toHaveTextContent(".NET");
    expect(verdict).toHaveTextContent("Next.js");
    expect(verdict).toHaveTextContent("compiled");
  });

  it("does not claim the Next.js half when it was never built", () => {
    // The exact dishonesty this replaces: the panel stamped "Compile Verified" while the
    // pipeline only ever ran `dotnet build`.
    render(
      <PaidTierPanel
        generation={makeGeneration({
          build_log: buildLog([
            { half: "dotnet", label: ".NET", status: "passed" },
            { half: "nextjs", label: "Next.js", status: "not_run" },
          ]),
        })}
      />
    );

    const verdict = screen.getByTestId("compile-verdict");
    expect(verdict).toHaveTextContent("not built");
    expect(verdict).not.toHaveTextContent("Compile Verified");
  });

  it("badges the FastAPI + React stack from the report, not a hardcoded .NET pair", () => {
    // FastAPI + React is a live Tier-2 option, and its report used to describe a ".NET" and
    // a "Next.js" half the customer never chose.
    render(
      <PaidTierPanel
        generation={makeGeneration({
          project_type: "PythonReact",
          build_log: buildLog([
            { half: "python", label: "FastAPI", status: "passed" },
            { half: "react", label: "React", status: "passed" },
          ]),
        })}
      />
    );

    const verdict = screen.getByTestId("compile-verdict");
    expect(verdict).toHaveTextContent("FastAPI");
    expect(verdict).toHaveTextContent("React");
    expect(verdict).not.toHaveTextContent(".NET");
    expect(verdict).not.toHaveTextContent("Next.js");
  });

  it("treats a verdict that names no halves as no record at all", () => {
    // "verified" with nothing to point at is not a verdict; claiming it would be the same
    // unearned badge in a new shape.
    render(<PaidTierPanel generation={makeGeneration({ build_log: buildLog([]) })} />);

    expect(screen.getByTestId("compile-verdict")).toHaveTextContent("Build record unavailable");
    expect(screen.queryByText(/compiled and verified/i)).toBeNull();
  });

  it("says the build record is unavailable rather than assuming verification", () => {
    render(<PaidTierPanel generation={makeGeneration({ build_log: "no report here" })} />);

    expect(screen.getByTestId("compile-verdict")).toHaveTextContent("Build record unavailable");
  });

  it("never shows a compile claim for a Tier 1 Blueprint", () => {
    // Tier 1 has no code and never goes near a compiler, yet the old panel badged it
    // "Compile Verified" like everything else.
    render(<PaidTierPanel generation={makeGeneration({ tier: 1 as Tier, build_log: buildLog(BOTH_PASSED) })} />);

    const verdict = screen.getByTestId("compile-verdict");
    expect(verdict).toHaveTextContent("Packaged");
    expect(verdict).not.toHaveTextContent(".NET");
  });

  it("drops the 'compiled and verified' wording when nothing verified it", () => {
    render(<PaidTierPanel generation={makeGeneration({ build_log: null })} />);

    expect(screen.queryByText(/compiled and verified/i)).toBeNull();
    expect(screen.getByText(/packaged and ready to download/i)).toBeInTheDocument();
  });

  it("keeps the verified wording when the recorded verdict says verified", () => {
    render(<PaidTierPanel generation={makeGeneration()} />);

    expect(screen.getByText(/compiled and verified/i)).toBeInTheDocument();
  });
});
