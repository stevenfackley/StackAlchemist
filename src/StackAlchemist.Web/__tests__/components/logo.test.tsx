import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Logo } from "../../src/components/logo";

describe("Logo", () => {
  it("defaults to the wordmark variant linking home", () => {
    render(<Logo />);
    const link = screen.getByRole("link", { name: /stack.*alchemist/i });
    expect(link).toHaveAttribute("href", "/");
    const img = screen.getByRole("img", { name: "Stack Alchemist" });
    expect(img).toHaveAttribute("width", "34");
    expect(img).toHaveAttribute("height", "34");
  });

  it("renders the mono variant with the compact monospace lockup", () => {
    const { container } = render(<Logo variant="mono" size={28} />);
    const img = screen.getByRole("img", { name: "Stack Alchemist" });
    expect(img).toHaveAttribute("width", "28");
    expect(img).toHaveAttribute("height", "28");
    const wordmarkSpan = container.querySelector("a > span");
    expect(wordmarkSpan).toHaveClass("font-mono");
    expect(wordmarkSpan?.textContent).toBe("STACK ALCHEMIST");
  });

  it("hides the wordmark text below sm only when hideWordmarkOnMobile is set", () => {
    const { container: shown } = render(<Logo />);
    const wordmarkSpan = shown.querySelector("a > span");
    expect(wordmarkSpan).not.toHaveClass("hidden");

    const { container: hidden } = render(<Logo hideWordmarkOnMobile />);
    const hiddenSpan = hidden.querySelector("a > span");
    expect(hiddenSpan).toHaveClass("hidden", "sm:block");
  });

  it("forwards a custom className alongside the variant's own layout classes", () => {
    render(<Logo className="shrink-0" />);
    const link = screen.getByRole("link", { name: /stack.*alchemist/i });
    expect(link).toHaveClass("shrink-0");
  });
});
