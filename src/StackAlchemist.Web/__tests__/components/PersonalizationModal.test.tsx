import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { DEFAULT_PERSONALIZATION } from "@/lib/types";
import { PersonalizationModal } from "@/components/personalization-modal";

afterEach(() => { document.body.style.overflow = ""; });

const noop = () => {};
const baseProps = {
  entityNames: ["User", "Order"],
  initialData: DEFAULT_PERSONALIZATION,
  onChange: noop,
  onComplete: noop,
  onCancel: noop,
};

describe("PersonalizationModal a11y", () => {
  it("renders with role=dialog and aria-modal", () => {
    render(<PersonalizationModal {...baseProps} />);
    const dialog = screen.getByRole("dialog", { name: /make it yours/i });
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });

  it("project name input is labelled", () => {
    render(<PersonalizationModal {...baseProps} />);
    expect(screen.getByLabelText(/what.*project.*called/i)).toBeInTheDocument();
  });

  it("tagline input is labelled", () => {
    render(<PersonalizationModal {...baseProps} />);
    expect(screen.getByLabelText(/tagline/i)).toBeInTheDocument();
  });

  it("Escape calls onCancel when modal is clean", async () => {
    const onCancel = vi.fn();
    render(<PersonalizationModal {...baseProps} onCancel={onCancel} />);
    await userEvent.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("focus returns to the opener after modal closes via Escape", () => {
    function Opener() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button onClick={() => setOpen(true)}>Open</button>
          {open && (
            <PersonalizationModal
              {...baseProps}
              onCancel={() => setOpen(false)}
              onComplete={() => setOpen(false)}
            />
          )}
        </>
      );
    }
    render(<Opener />);
    const opener = screen.getByRole("button", { name: "Open" });
    opener.focus();
    fireEvent.click(opener);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(opener).toHaveFocus();
  });
});
