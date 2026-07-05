import { describe, it, expect } from "vitest";
import { render, screen, within, fireEvent } from "@testing-library/react";
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

  describe("mobile menu", () => {
    it("starts closed with an accessible hamburger toggle", () => {
      render(<ContentHeader />);
      const toggle = screen.getByRole("button", { name: /open menu/i });
      expect(toggle).toHaveAttribute("aria-expanded", "false");
      // Only the desktop set of links exists while the menu is closed.
      expect(screen.getAllByRole("link", { name: /^pricing$/i })).toHaveLength(1);
    });

    it("opens on toggle click, duplicating nav links into the mobile panel", () => {
      render(<ContentHeader />);
      fireEvent.click(screen.getByRole("button", { name: /open menu/i }));
      const toggle = screen.getByRole("button", { name: /close menu/i });
      expect(toggle).toHaveAttribute("aria-expanded", "true");
      expect(screen.getAllByRole("link", { name: /^pricing$/i })).toHaveLength(2);
      expect(screen.getAllByRole("link", { name: /^login$/i })).toHaveLength(2);
    });

    it("closes when a mobile link is clicked", () => {
      render(<ContentHeader />);
      fireEvent.click(screen.getByRole("button", { name: /open menu/i }));
      const [, mobilePricing] = screen.getAllByRole("link", { name: /^pricing$/i });
      fireEvent.click(mobilePricing);
      expect(screen.getAllByRole("link", { name: /^pricing$/i })).toHaveLength(1);
      expect(screen.getByRole("button", { name: /open menu/i })).toHaveAttribute(
        "aria-expanded",
        "false"
      );
    });

    it("closes on Escape", () => {
      render(<ContentHeader />);
      fireEvent.click(screen.getByRole("button", { name: /open menu/i }));
      fireEvent.keyDown(document, { key: "Escape" });
      expect(screen.getAllByRole("link", { name: /^pricing$/i })).toHaveLength(1);
    });
  });
});
