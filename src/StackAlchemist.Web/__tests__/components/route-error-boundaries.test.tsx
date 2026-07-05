import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import DashboardError from "../../src/app/dashboard/error";
import SimpleError from "../../src/app/simple/error";
import AdvancedError from "../../src/app/advanced/error";
import GenerationError from "../../src/app/generate/[id]/error";

/**
 * Contract for app-flow route error boundaries: internal exception text must
 * never reach the user; the error is logged for telemetry; the digest (when
 * present) is surfaced as a support reference; reset is wired to the button.
 */
const BOUNDARIES = [
  ["dashboard", DashboardError],
  ["simple", SimpleError],
  ["advanced", AdvancedError],
  ["generate/[id]", GenerationError],
] as const;

const INTERNAL_DETAIL = "ECONNREFUSED supabase.internal:5432 (service_role)";

describe.each(BOUNDARIES)("%s error boundary", (_name, Boundary) => {
  let consoleSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    consoleSpy.mockRestore();
    cleanup();
  });

  it("never renders the raw error message", () => {
    const error = Object.assign(new Error(INTERNAL_DETAIL), { digest: undefined });
    render(<Boundary error={error} reset={() => {}} />);
    expect(screen.queryByText(new RegExp("ECONNREFUSED"))).not.toBeInTheDocument();
    expect(screen.queryByText(new RegExp("service_role"))).not.toBeInTheDocument();
  });

  it("logs the error for telemetry", () => {
    const error = Object.assign(new Error(INTERNAL_DETAIL), { digest: undefined });
    render(<Boundary error={error} reset={() => {}} />);
    expect(consoleSpy).toHaveBeenCalledWith(error);
  });

  it("shows the digest as a support reference when present", () => {
    const error = Object.assign(new Error(INTERNAL_DETAIL), { digest: "abc123def" });
    render(<Boundary error={error} reset={() => {}} />);
    expect(screen.getByText(/abc123def/)).toBeInTheDocument();
  });

  it("omits the reference line when there is no digest", () => {
    const error = Object.assign(new Error(INTERNAL_DETAIL), { digest: undefined });
    render(<Boundary error={error} reset={() => {}} />);
    expect(screen.queryByText(/Reference:/i)).not.toBeInTheDocument();
  });

  it("wires the action button to reset", () => {
    const error = Object.assign(new Error(INTERNAL_DETAIL), { digest: undefined });
    const reset = vi.fn();
    render(<Boundary error={error} reset={reset} />);
    fireEvent.click(screen.getByRole("button"));
    expect(reset).toHaveBeenCalledTimes(1);
  });
});
