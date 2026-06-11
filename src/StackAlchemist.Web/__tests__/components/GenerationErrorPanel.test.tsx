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
  attempt_count: 1,
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
  it("shows attempt count in body", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "build", attempt_count: 2 }}
      />
    );
    expect(screen.getByText(/attempt 2 of 3/i)).toBeInTheDocument();
  });

  it("shows refund note with generation ID at max attempts", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "build", attempt_count: 3 }}
      />
    );
    expect(screen.getByText(/full refund/i)).toBeInTheDocument();
    expect(screen.getByText("gen-abc-123")).toBeInTheDocument();
  });

  it("shows retry button when onRetry provided", () => {
    const onRetry = vi.fn();
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "build" }}
        onRetry={onRetry}
      />
    );
    const btn = screen.getByRole("button", { name: /retry/i });
    fireEvent.click(btn);
    expect(onRetry).toHaveBeenCalledOnce();
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
});

describe("GenerationErrorPanel — internal category", () => {
  it("shows generation ID in support note", () => {
    render(
      <GenerationErrorPanel
        generation={{ ...baseGeneration, error_category: "internal" }}
      />
    );
    expect(screen.getByRole("heading", { name: /unexpected error/i })).toBeInTheDocument();
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
