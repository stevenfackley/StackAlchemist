import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Generation, PaymentStatus, Tier } from "@/lib/types";

const watch = vi.hoisted(() => ({
  enabled: [] as boolean[],
  onUpdate: null as ((row: unknown) => void) | null,
}));

vi.mock("@/lib/hooks/use-generation-status", () => ({
  useGenerationStatus: (options: { enabled?: boolean; onUpdate: (row: unknown) => void }) => {
    watch.enabled.push(options.enabled ?? true);
    watch.onUpdate = options.onUpdate;
  },
}));
vi.mock("@/lib/actions", () => ({ retryGeneration: vi.fn() }));
vi.mock("@/lib/runtime-config", () => ({ isDemoMode: false }));
vi.mock("@/app/generate/[id]/components/generate-header", () => ({ GenerateHeader: () => null }));
vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

import { GenerateClientPage } from "@/app/generate/[id]/GenerateClientPage";

function failedGeneration(payment_status: PaymentStatus | null, overrides: Partial<Generation> = {}): Generation {
  return {
    id: "gen-1",
    user_id: "user-1",
    transaction_id: null,
    project_type: "DotNetNextJs",
    status: "failed",
    mode: "advanced",
    tier: 2 as Tier,
    prompt: "Build an invoicing app",
    schema_json: null,
    download_url: null,
    preview_files_json: null,
    personalization_json: null,
    build_log: null,
    error_message: "boom",
    error_category: "internal",
    attempt_count: 0,
    created_at: "2026-11-11T00:00:00Z",
    updated_at: "2026-11-11T00:00:00Z",
    completed_at: "2026-11-11T00:00:00Z",
    payment_status,
    ...overrides,
  };
}

const watching = () => watch.enabled.at(-1);

describe("GenerateClientPage — watching a failed paid build for its refund", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    watch.enabled.length = 0;
    watch.onUpdate = null;
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("keeps watching a failed build whose refund is expected but not recorded yet", () => {
    // The Engine writes 'failed' first and starts the refund a moment later, so the poll that
    // sees the failure still sees the payment in place.
    render(<GenerateClientPage initialGeneration={failedGeneration("completed")} generationId="gen-1" />);

    expect(watching()).toBe(true);
    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refund_expected");
  });

  it("shows the refund as started, and stops watching, once the next poll sees it", () => {
    render(<GenerateClientPage initialGeneration={failedGeneration("completed")} generationId="gen-1" />);

    act(() => watch.onUpdate!(failedGeneration("refund_pending")));

    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refunded");
    expect(watching()).toBe(false);
  });

  it("gives up watching after three minutes, and still says a refund is expected", () => {
    render(<GenerateClientPage initialGeneration={failedGeneration("completed")} generationId="gen-1" />);

    act(() => vi.advanceTimersByTime(3 * 60 * 1000 - 1));
    expect(watching()).toBe(true);

    act(() => vi.advanceTimersByTime(1));
    expect(watching()).toBe(false);
    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refund_expected");
  });

  it.each([
    ["a refund already recorded", failedGeneration("refund_pending")],
    ["a failure the customer can retry", failedGeneration("completed", { error_category: "network" })],
    ["a free build", failedGeneration(null, { tier: 0 as Tier })],
  ])("does not watch a failed build with %s", (_name, generation) => {
    render(<GenerateClientPage initialGeneration={generation} generationId="gen-1" />);
    expect(watching()).toBe(false);
  });

  it("watches a build that is still in progress, as before", () => {
    render(
      <GenerateClientPage
        initialGeneration={failedGeneration("completed", { status: "building", error_category: null, error_message: null })}
        generationId="gen-1"
      />
    );
    expect(watching()).toBe(true);
  });
});
