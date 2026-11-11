import { render, screen, fireEvent } from "@testing-library/react";
import { vi } from "vitest";
import { GenerationErrorPanel } from "@/components/generation-error-panel";
import type { GenerationErrorCategory } from "@/lib/types";

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

const baseGeneration = {
  id: "gen-abc-123",
  error_message: null,
  retry_count: 1,
  error_category: null as GenerationErrorCategory | null,
};

describe("GenerationErrorPanel — null category fallback", () => {
  it("shows raw error_message when category is null", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_message: "Something exploded" }}
      />
    );
    expect(screen.getByText("Something exploded")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /something went wrong/i })).toBeInTheDocument();
  });

  it("shows default copy when category and message are both null", () => {
    render(<GenerationErrorPanel generation={baseGeneration} />);
    expect(screen.getByText(/unexpected error occurred/i)).toBeInTheDocument();
  });

  it("forwards testId to root element", () => {
    render(<GenerationErrorPanel testId="my-panel" errorMessage="oops" />);
    expect(screen.getByTestId("my-panel")).toBeInTheDocument();
  });
});

describe("GenerationErrorPanel — quota category", () => {
  it("shows correct title and upgrade link", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "quota" }}
      />
    );
    expect(screen.getByRole("heading", { name: /free build limit reached/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /view paid tiers/i })).toBeInTheDocument();
  });

  it("does NOT show retry button for quota", () => {
    const onRetry = vi.fn();
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "quota" }}
        onRetry={onRetry}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });
});

describe("GenerationErrorPanel — build category", () => {
  it("says the code did not compile after the automatic corrections, not a retry count", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "build", retry_count: 0, tier: 2, payment_status: "refund_pending" }}
      />
    );
    expect(screen.getByRole("heading", { name: /did not compile/i })).toBeInTheDocument();
    expect(screen.getByText(/after 3 automatic correction attempts/i)).toBeInTheDocument();
    expect(screen.queryByText(/attempt \d of 3/i)).toBeNull();
  });

  it("a paid compile failure is refunded, so no Retry is offered even before the refund is recorded", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "build", tier: 2, payment_status: "completed" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refund_expected");
  });
});

describe("GenerationErrorPanel — what happened to the payment", () => {
  const paid = { ...baseGeneration, tier: 2 as const, retry_count: 0 };

  it("refund started: says so plainly, tells the customer to do nothing, and offers no Retry", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "internal", payment_status: "refund_pending" }}
        onRetry={vi.fn()}
      />
    );
    const note = screen.getByTestId("payment-outcome");
    expect(note).toHaveAttribute("data-outcome", "refunded");
    expect(note).toHaveTextContent(/full refund for this build has been started/i);
    expect(note).toHaveTextContent(/don't need to do anything/i);
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
    expect(screen.getByRole("link", { name: /start a new build/i })).toBeInTheDocument();
  });

  it("a retryable failure after a refund offers no Retry: the Engine would refuse it", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "network", payment_status: "refunded" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refunded");
  });

  it("refund not recorded yet: says one is coming, when to worry, and gives the generation ID", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "internal", payment_status: "completed" }}
        onRetry={vi.fn()}
      />
    );
    const note = screen.getByTestId("payment-outcome");
    expect(note).toHaveAttribute("data-outcome", "refund_expected");
    expect(note).toHaveTextContent(/starts automatically/i);
    expect(note).toHaveTextContent(/no email arrives within an hour/i);
    expect(note).toHaveTextContent("gen-abc-123");
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });

  it("an old failure whose refund never went out: does not promise it is on its way, says we owe it", () => {
    render(
      <GenerationErrorPanel
        generation={{
          ...paid, error_category: "internal", payment_status: "completed",
          updated_at: "2026-10-01T00:00:00Z",
        }}
        onRetry={vi.fn()}
      />
    );
    const note = screen.getByTestId("payment-outcome");
    expect(note).toHaveAttribute("data-outcome", "refund_overdue");
    expect(note).toHaveTextContent(/we owe you a refund/i);
    expect(note).toHaveTextContent(/has not gone out/i);
    expect(note).not.toHaveTextContent(/starts automatically/i);
    expect(note).toHaveTextContent("gen-abc-123");
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });

  it("payment in place and retries left: Retry, says retrying is free, and counts what is left", () => {
    const onRetry = vi.fn();
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "network", retry_count: 1, payment_status: "completed" }}
        onRetry={onRetry}
      />
    );
    const note = screen.getByTestId("payment-outcome");
    expect(note).toHaveAttribute("data-outcome", "kept");
    expect(note).toHaveTextContent(/retrying costs nothing/i);
    expect(note).toHaveTextContent(/refunded automatically/i);
    expect(screen.getByTestId("retries-left")).toHaveTextContent("2 of 3 retries left");
    fireEvent.click(screen.getByRole("button", { name: /^retry$/i }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("the last retry is counted in the singular", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "network", retry_count: 2, payment_status: "completed" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByTestId("retries-left")).toHaveTextContent("1 of 3 retry left");
  });

  it("retries used up on a paid build: no Retry, says so, and says the refund is coming", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "network", retry_count: 3, payment_status: "completed" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
    expect(screen.getByTestId("retries-used-up")).toHaveTextContent("You have used all 3 retries for this build.");
    expect(screen.getByTestId("payment-outcome")).toHaveAttribute("data-outcome", "refund_expected");
  });

  it("no completed payment: says the build was not started, not that it failed", () => {
    render(
      <GenerationErrorPanel
        generation={{
          ...paid, error_category: "internal", payment_status: null,
          error_message: "This build has no completed payment, so it was not started.",
        }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByRole("heading", { name: /this build was not started/i })).toBeInTheDocument();
    expect(screen.queryByText(/error on our side/i)).toBeNull();
    const note = screen.getByTestId("payment-outcome");
    expect(note).toHaveAttribute("data-outcome", "unpaid");
    expect(note).toHaveTextContent(/if you were charged/i);
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });

  it("a disputed payment: no Retry, and says why", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...paid, error_category: "network", payment_status: "disputed" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByTestId("payment-outcome")).toHaveTextContent(/under dispute/i);
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });

  it("a free build shows no payment note", () => {
    render(
      <GenerationErrorPanel generation={{ ...baseGeneration, tier: 0, error_category: "network" }} onRetry={vi.fn()} />
    );
    expect(screen.queryByTestId("payment-outcome")).toBeNull();
  });
});

describe("GenerationErrorPanel — encouragement to retry", () => {
  it("says retrying usually helps only while a Retry is on offer", () => {
    const { unmount } = render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, tier: 0, error_category: "network", retry_count: 0 }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByText(/retrying usually resolves this/i)).toBeInTheDocument();
    unmount();

    // Every retry used: telling the customer that retrying helps would contradict the next line.
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, tier: 0, error_category: "network", retry_count: 3 }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByText(/retrying usually resolves this/i)).toBeNull();
    expect(screen.getByText(/network or timeout problem interrupted the build/i)).toBeInTheDocument();
  });

  it("does not suggest a second try on a paid build that has been refunded", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, tier: 2, error_category: "schema", payment_status: "refunded" }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByText(/second try/i)).toBeNull();
  });
});

describe("GenerationErrorPanel — retries on a free build", () => {
  it("counts the retries left under the button", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, tier: 0, error_category: "schema", retry_count: 0 }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByTestId("retries-left")).toHaveTextContent("3 of 3 retries left");
  });

  it("with every retry used, hides Retry and says to start a new build", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, tier: 0, error_category: "schema", retry_count: 3 }}
        onRetry={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
    expect(screen.getByTestId("retries-used-up")).toHaveTextContent(
      "You have used all 3 retries for this build. Start a new one to try again."
    );
    expect(screen.getByRole("link", { name: /start a new build/i })).toBeInTheDocument();
  });
});

describe("GenerationErrorPanel — retry state", () => {
  it("shows spinner and 'Retrying...' when isRetrying=true", () => {
    render(
      <GenerationErrorPanel
        errorMessage="fail"
        onRetry={vi.fn()}
        isRetrying={true}
      />
    );
    expect(screen.getByRole("button", { name: /retrying/i })).toBeDisabled();
    expect(screen.queryByRole("button", { name: /^retry$/i })).toBeNull();
  });

  it("says why the last retry did not start, and keeps the Retry button", () => {
    render(
      <GenerationErrorPanel
        errorMessage="fail"
        onRetry={vi.fn()}
        retryError="This build has no completed payment."
      />
    );
    expect(screen.getByRole("alert")).toHaveTextContent("This build has no completed payment.");
    expect(screen.getByRole("button", { name: /^retry$/i })).toBeEnabled();
  });

  it("hides the previous retry's message while the next retry is running", () => {
    render(
      <GenerationErrorPanel errorMessage="fail" onRetry={vi.fn()} isRetrying={true} retryError="Please sign in to retry a build." />
    );
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

describe("GenerationErrorPanel — internal category", () => {
  it("shows generation ID in support note", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "internal" }}
      />
    );
    expect(screen.getByRole("heading", { name: /something went wrong on our side/i })).toBeInTheDocument();
    expect(screen.getByText("gen-abc-123")).toBeInTheDocument();
  });

  it("does NOT show retry button for internal errors", () => {
    const onRetry = vi.fn();
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "internal" }}
        onRetry={onRetry}
      />
    );
    expect(screen.queryByRole("button", { name: /retry/i })).toBeNull();
  });
});

describe("GenerationErrorPanel — rate_limit / network categories", () => {
  it.each(["rate_limit", "network"] as const)(
    "%s shows retry button when onRetry provided",
    (category) => {
      const onRetry = vi.fn();
      render(
        <GenerationErrorPanel
          generation={{ ...baseGeneration, error_category: category }}
          onRetry={onRetry}
        />
      );
      expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument();
    }
  );
});

describe("GenerationErrorPanel — standalone props (simple/advanced pages)", () => {
  it("renders with errorMessage prop alone", () => {
    render(<GenerationErrorPanel errorMessage="No prompt provided." />);
    expect(screen.getByText("No prompt provided.")).toBeInTheDocument();
  });

  it("without a row there is nothing to count: Retry and Start Over, no retry counter, no payment note", () => {
    const onRetry = vi.fn();
    render(<GenerationErrorPanel errorMessage="No prompt provided." onRetry={onRetry} />);
    fireEvent.click(screen.getByRole("button", { name: /^retry$/i }));
    expect(onRetry).toHaveBeenCalledOnce();
    expect(screen.getByRole("link", { name: /start over/i })).toBeInTheDocument();
    expect(screen.queryByTestId("retries-left")).toBeNull();
    expect(screen.queryByTestId("payment-outcome")).toBeNull();
  });

  it("prefers generation object over standalone props", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_message: "from generation" }}
        errorMessage="from prop"
      />
    );
    expect(screen.getByText("from generation")).toBeInTheDocument();
    expect(screen.queryByText("from prop")).toBeNull();
  });
});
