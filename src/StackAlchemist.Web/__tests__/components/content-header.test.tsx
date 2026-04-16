import { describe, it, expect } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { ContentHeader } from "../../src/components/content-header";

describe("ContentHeader", () => {
  it("renders all primary nav links with expected hrefs", () => {
    render(<ContentHeader />);
    const nav = screen.getByRole("navigation");
    const expected: Array<[string, string]> = [
      ["Pricing", "/pricing"],
      ["Blog", "/blog"],
      ["About", "/about"],
      ["Build", "/"],
      ["Login", "/login"],
    ];
    for (const [label, href] of expected) {
      const link = within(nav).getByRole("link", { name: new RegExp(`^${label}$`, "i") });
      expect(link).toHaveAttribute("href", href);
    }
  });

  it("is presentational — no server data dependencies", () => {
    // Smoke: renders without throwing, no async props required.
    expect(() => render(<ContentHeader />)).not.toThrow();
  });
});
