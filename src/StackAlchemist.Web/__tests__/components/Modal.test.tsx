import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { Modal } from "@/components/ui/modal";

function Harness({ onClose = () => {} }: { onClose?: () => void }) {
  return (
    <Modal onClose={onClose} title="Test Modal" testId="test-modal">
      <button>First</button>
      <button>Last</button>
    </Modal>
  );
}

describe("Modal", () => {
  afterEach(() => {
    document.body.style.overflow = "";
  });

  it("renders with dialog semantics and an accessible name", () => {
    render(<Harness />);
    const dialog = screen.getByRole("dialog", { name: "Test Modal" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });

  it("calls onClose on Escape", async () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose on backdrop mousedown, but not on panel clicks", () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);

    fireEvent.mouseDown(screen.getByTestId("test-modal"));
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.mouseDown(screen.getByTestId("test-modal-backdrop"));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose from the built-in close button", async () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    await userEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("traps Tab focus inside the panel", async () => {
    render(<Harness />);
    const last = screen.getByRole("button", { name: "Last" });
    last.focus();

    await userEvent.tab();
    // Wrapped past the end back to the first focusable (the header close button).
    expect(screen.getByRole("button", { name: "Close" })).toHaveFocus();

    await userEvent.tab({ shift: true });
    expect(last).toHaveFocus();
  });

  it("locks body scroll while mounted and restores on unmount", () => {
    const { unmount } = render(<Harness />);
    expect(document.body.style.overflow).toBe("hidden");
    unmount();
    expect(document.body.style.overflow).toBe("");
  });

  it("keeps scroll locked while a second stacked modal is open", () => {
    const first = render(<Harness />);
    const second = render(<Harness />);
    second.unmount();
    expect(document.body.style.overflow).toBe("hidden");
    first.unmount();
    expect(document.body.style.overflow).toBe("");
  });

  it("restores focus to the opener on unmount", () => {
    function Opener() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button onClick={() => setOpen(true)}>Open</button>
          {open && (
            <Modal onClose={() => setOpen(false)} title="M">
              <p>content</p>
            </Modal>
          )}
        </>
      );
    }
    render(<Opener />);
    const opener = screen.getByRole("button", { name: "Open" });
    opener.focus();
    fireEvent.click(opener);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(opener).toHaveFocus();
  });
});
